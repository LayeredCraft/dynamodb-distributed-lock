using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.Xunit3;
using AwesomeAssertions;
using DynamoDb.DistributedLock.Retry;
using DynamoDb.DistributedLock.Tests.TestKit.Attributes;

namespace DynamoDb.DistributedLock.Tests.Retry;

public class ExponentialBackoffRetryPolicyTests
{
    [Fact]
    public void Constructor_WhenOptionsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new ExponentialBackoffRetryPolicy(null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("options");
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenOperationIsNull_ShouldThrowArgumentNullException(
        ExponentialBackoffRetryPolicy sut)
    {
        var act = async () => await sut.ExecuteAsync<int>(null!, _ => true);

        var exception = await act.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("operation");
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenShouldRetryIsNull_ShouldThrowArgumentNullException(
        ExponentialBackoffRetryPolicy sut)
    {
        var act = async () => await sut.ExecuteAsync<int>(_ => Task.FromResult(42), null!);

        var exception = await act.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("shouldRetry");
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenOperationSucceedsOnFirstAttempt_ShouldReturnResult(
        RetryOptions options,
        string expectedResult)
    {
        var sut = new ExponentialBackoffRetryPolicy(options);
        var operationCalled = 0;

        var result = await sut.ExecuteAsync(
            _ =>
            {
                operationCalled++;
                return Task.FromResult(expectedResult);
            },
            _ => true);

        result.Should().Be(expectedResult);
        operationCalled.Should().Be(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenOperationFailsButShouldNotRetry_ShouldThrowImmediately(
        RetryOptions options)
    {
        options.MaxAttempts = 3;
        var sut = new ExponentialBackoffRetryPolicy(options);
        var operationCalled = 0;
        var expectedException = new InvalidOperationException("Test exception");

        var act = async () => await sut.ExecuteAsync<int>(
            _ =>
            {
                operationCalled++;
                throw expectedException;
            },
            _ => false); // Should not retry

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().Be(expectedException);
        operationCalled.Should().Be(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenOperationFailsAndShouldRetry_ShouldRetryUpToMaxAttempts(
        RetryOptions options)
    {
        options.MaxAttempts = 3;
        options.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test
        var sut = new ExponentialBackoffRetryPolicy(options);
        var operationCalled = 0;
        var expectedException = new InvalidOperationException("Test exception");

        var act = async () => await sut.ExecuteAsync<int>(
            _ =>
            {
                operationCalled++;
                throw expectedException;
            },
            _ => true); // Always retry

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().Be(expectedException);
        operationCalled.Should().Be(3);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenOperationSucceedsAfterRetries_ShouldReturnResult(
        RetryOptions options,
        string expectedResult)
    {
        options.MaxAttempts = 3;
        options.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test
        var sut = new ExponentialBackoffRetryPolicy(options);
        var operationCalled = 0;

        var result = await sut.ExecuteAsync(
            _ =>
            {
                operationCalled++;
                if (operationCalled < 3)
                    throw new InvalidOperationException("Retry me");
                return Task.FromResult(expectedResult);
            },
            _ => true);

        result.Should().Be(expectedResult);
        operationCalled.Should().Be(3);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ExecuteAsync_WhenCancellationRequested_ShouldThrowOperationCanceledException(
        RetryOptions options)
    {
        options.MaxAttempts = 3;
        options.BaseDelay = TimeSpan.FromMilliseconds(100);
        var sut = new ExponentialBackoffRetryPolicy(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await sut.ExecuteAsync<int>(
            async ct =>
            {
                await Task.Delay(200, ct); // This should be cancelled
                return 42;
            },
            _ => true,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}