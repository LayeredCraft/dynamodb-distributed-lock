using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Compono;
using Compono.XunitV3;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Tests.TestKit.Profiles;
using Microsoft.Extensions.Options;

namespace DynamoDb.DistributedLock.Tests.Retry;

public class RetryIntegrationTests
{
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenRetryDisabled_ShouldNotRetryOnFailure(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange - one static response regardless of args; no matching or sequencing needed.
        options.Value.Retry.Enabled = false;
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new ConditionalCheckFailedException("Lock exists"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Once();
    }

    // ADR-0054 (sequential/call-count-based responses): the SUT's own internal retry loop makes
    // all 3 PutItemAsync calls inside one `await sut.AcquireLockAsync(...)`, with no opportunity
    // for the test to reconfigure the double between calls - ReturnsSequence(...) configures the
    // fail/fail/succeed sequence up front instead.
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenRetryEnabledAndEventuallySucceeds_ShouldReturnTrue(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .ReturnsSequence(
                SequenceOutcome.Throw(new ConditionalCheckFailedException("Lock exists")),
                SequenceOutcome.Throw(new ConditionalCheckFailedException("Lock exists")),
                Task.FromResult(new PutItemResponse()));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Exactly(3);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenRetryEnabledButMaxAttemptsReached_ShouldReturnFalse(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange - fails identically on every attempt; no matching or sequencing needed.
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 2;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new ConditionalCheckFailedException("Lock exists"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Exactly(2);
    }

    // ADR-0054 (sequential/call-count-based responses) - same reasoning as
    // AcquireLockAsync_WhenRetryEnabledAndEventuallySucceeds_ShouldReturnTrue above.
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenRetryEnabledWithThrottling_ShouldRetryOnProvisionedThroughputExceeded(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .ReturnsSequence(
                SequenceOutcome.Throw(new ProvisionedThroughputExceededException("Throttled")),
                SequenceOutcome.Throw(new ProvisionedThroughputExceededException("Throttled")),
                Task.FromResult(new PutItemResponse()));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Exactly(3);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenRetryEnabledButNonRetriableException_ShouldThrowImmediately(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange - one static response regardless of args; no matching or sequencing needed.
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;

        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new ArgumentException("Non-retriable exception"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act & Assert
        var act = async () => await sut.AcquireLockAsync(resourceId, ownerId);
        await act.Should().ThrowAsync<ArgumentException>();

        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Once();
    }

    // ADR-0054 (sequential/call-count-based responses) - same reasoning as
    // AcquireLockAsync_WhenRetryEnabledAndEventuallySucceeds_ShouldReturnTrue above.
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockHandleAsync_WhenRetryEnabledAndSucceeds_ShouldReturnHandle(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 3;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .ReturnsSequence(
                SequenceOutcome.Throw(new ConditionalCheckFailedException("Lock exists")),
                Task.FromResult(new PutItemResponse()));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockHandleAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ResourceId.Should().Be(resourceId);
        result.OwnerId.Should().Be(ownerId);
        result.IsAcquired.Should().BeTrue();
        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Exactly(2);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenRetryEnabledAndThrottlingExhaustsRetries_ShouldReturnFalse(
        IAmazonDynamoDB dynamo,
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics,
        string resourceId,
        string ownerId)
    {
        // Arrange - fails identically on every attempt; no matching or sequencing needed.
        options.Value.Retry.Enabled = true;
        options.Value.Retry.MaxAttempts = 2;
        options.Value.Retry.BaseDelay = TimeSpan.FromMilliseconds(1); // Fast test

        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new ProvisionedThroughputExceededException("Throttled"));

        var sut = new DynamoDbDistributedLock(dynamo, options, lockMetrics);

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        dynamo.Verify()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Exactly(2);
    }
}
