using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AutoFixture.Xunit3;
using AwesomeAssertions;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Retry;
using DynamoDb.DistributedLock.Tests.TestKit.Attributes;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DynamoDb.DistributedLock.Tests.Retry;

public class RetryIntegrationTests
{
    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenRetryDisabled_ShouldNotRetryOnFailure(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = false;
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConditionalCheckFailedException("Lock exists"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        await dynamo.Received(1).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenRetryEnabledAndEventuallySucceeds_ShouldReturnTrue(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        var callCount = 0;
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount < 3)
                    throw new ConditionalCheckFailedException("Lock exists");
                return new PutItemResponse();
            });

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        await dynamo.Received(3).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenRetryEnabledButMaxAttemptsReached_ShouldReturnFalse(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 2;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConditionalCheckFailedException("Lock exists"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        await dynamo.Received(2).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenRetryEnabledWithThrottling_ShouldRetryOnProvisionedThroughputExceeded(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        var callCount = 0;
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount < 3)
                    throw new ProvisionedThroughputExceededException("Throttled");
                return new PutItemResponse();
            });

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        await dynamo.Received(3).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenRetryEnabledButNonRetriableException_ShouldThrowImmediately(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;

        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("Non-retriable exception"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act & Assert
        var act = async () => await sut.AcquireLockAsync(resourceId, ownerId);
        await act.Should().ThrowAsync<ArgumentException>();

        await dynamo.Received(1).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockHandleAsync_WhenRetryEnabledAndSucceeds_ShouldReturnHandle(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        var callCount = 0;
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount < 2)
                    throw new ConditionalCheckFailedException("Lock exists");
                return new PutItemResponse();
            });

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockHandleAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ResourceId.Should().Be(resourceId);
        result.OwnerId.Should().Be(ownerId);
        result.IsAcquired.Should().BeTrue();
        await dynamo.Received(2).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenRetryEnabledAndThrottlingExhaustsRetries_ShouldReturnFalse(
        [Frozen] IAmazonDynamoDB dynamo,
        [Frozen] IOptions<DynamoDbLockOptions> options,
        [Frozen] ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 2;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProvisionedThroughputExceededException("Throttled"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        await dynamo.Received(2).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }
}