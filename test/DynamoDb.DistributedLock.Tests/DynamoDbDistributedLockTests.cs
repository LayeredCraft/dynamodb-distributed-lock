using System.Diagnostics.Metrics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Compono;
using Compono.XunitV3;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Tests.Metrics;
using DynamoDb.DistributedLock.Tests.TestKit.Profiles;
using Microsoft.Extensions.Options;

namespace DynamoDb.DistributedLock.Tests;

public class DynamoDbDistributedLockTests
{
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Constructor_WhenClientIsNull_ShouldThrowArgumentNullException(
        IOptions<DynamoDbLockOptions> options,
        ILockMetrics lockMetrics)
    {
        Action act = () => _ = new DynamoDbDistributedLock(null!, options, lockMetrics);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("client");
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Constructor_WhenOptionsValueIsNull_ShouldThrowArgumentNullException(
        IAmazonDynamoDB client,
        IOptions<DynamoDbLockOptions> nullOptions,
        ILockMetrics lockMetrics)
    {
        var act = () => _ = new DynamoDbDistributedLock(client, nullOptions, lockMetrics);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("options");
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Constructor_WhenLockMetricsValueIsNull_ShouldThrowArgumentNullException(
        IAmazonDynamoDB client,
        IOptions<DynamoDbLockOptions> options)
    {
        var act = () => _ = new DynamoDbDistributedLock(client, options, null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("lockMetrics");
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenLockIsAvailable_ShouldReturnTrue(
        [Shared] Meter meter, [Shared] IAmazonDynamoDB dynamo, TestMetricAggregator<int> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange - no argument matching needed (a blanket response regardless of args); a literal
        // discriminator argument just selects the (PutItemRequest, CancellationToken) overload.
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Returns(Task.FromResult(new PutItemResponse()));

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(0);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenLockAlreadyExists_ShouldReturnFalse(
        [Shared] Meter meter, [Shared] IAmazonDynamoDB dynamo, TestMetricAggregator<int> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new ConditionalCheckFailedException("lock exists"));

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenUnexpectedExceptionOccurs_ShouldThrow(
        [Shared] Meter meter, [Shared] IAmazonDynamoDB dynamo, TestMetricAggregator<int> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new InvalidOperationException("unexpected failure"));

        // Act
        var act = async () => await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task ReleaseLockAsync_WhenOwnerMatches_ShouldReturnTrue(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .DeleteItemAsync(new DeleteItemRequest(), CancellationToken.None)
            .Returns(Task.FromResult(new DeleteItemResponse()));

        // Act
        var result = await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        metricAggregator.Collect(MetricNames.LockRelease).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockReleaseFailed).Should().HaveCount(0);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task ReleaseLockAsync_WhenOwnerDoesNotMatch_ShouldReturnFalse(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .DeleteItemAsync(new DeleteItemRequest(), CancellationToken.None)
            .Throws(new ConditionalCheckFailedException("owner mismatch"));

        // Act
        var result = await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        metricAggregator.Collect(MetricNames.LockRelease).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockReleaseFailed).Should().HaveCount(1);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task ReleaseLockAsync_WhenUnexpectedExceptionOccurs_ShouldThrow(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .DeleteItemAsync(new DeleteItemRequest(), CancellationToken.None)
            .Throws(new InvalidOperationException("unexpected failure"));

        // Act
        var act = async () => await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        metricAggregator.Collect(MetricNames.LockRelease).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockReleaseFailed).Should().HaveCount(1);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockHandleAsync_WhenLockIsAvailable_ShouldReturnHandle(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Returns(Task.FromResult(new PutItemResponse()));

        // Act
        var result = await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ResourceId.Should().Be(resourceId);
        result.OwnerId.Should().Be(ownerId);
        result.IsAcquired.Should().BeTrue();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(0);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockHandleAsync_WhenLockAlreadyExists_ShouldReturnNull(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new ConditionalCheckFailedException("lock exists"));

        // Act
        var result = await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeNull();

        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockHandleAsync_WhenUnexpectedExceptionOccurs_ShouldThrow(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Throws(new InvalidOperationException("unexpected failure"));

        // Act
        var act = async () => await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    // ADR-0044 Amendment 21 (overload-safe argument matching): asserts on
    // DeleteItemRequest.ConditionExpression/ExpressionAttributeValues *content* via the new
    // DeleteItemAsyncMatching(...) surface, which shares DeleteItemAsync's own entries/call log -
    // the discriminator-only Configure() below still answers every real call regardless of content;
    // Verify() below independently filters by the predicate.
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockHandleAsync_DisposeHandle_ShouldCallReleaseLock(
        [Shared] Meter meter,
        [Shared] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Returns(Task.FromResult(new PutItemResponse()));
        dynamo.Configure()
            .DeleteItemAsync(new DeleteItemRequest(), CancellationToken.None)
            .Returns(Task.FromResult(new DeleteItemResponse()));

        // Act
        var handle = await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);
        await handle!.DisposeAsync();

        // Assert
        dynamo.Verify()
            .DeleteItemAsyncMatching(
                Match.Is<DeleteItemRequest>(req =>
                    req.ConditionExpression.Contains("ownerId = :owner") &&
                    req.ExpressionAttributeValues.ContainsKey(":owner") &&
                    req.ExpressionAttributeValues[":owner"].S == ownerId),
                Match.Any<CancellationToken>())
            .Once();

        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(0);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task AcquireLockAsync_WhenLockIsAvailable_TimersRecordMetrics(
        [Shared] Meter meter, [Shared] IAmazonDynamoDB dynamo, TestMetricAggregator<double> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // No argument matching or per-call sequencing needed here - a literal discriminator argument
        // selects the (PutItemRequest, CancellationToken)/(DeleteItemRequest, CancellationToken)
        // overload, and a pre-built, already-in-flight delayed Task (not a callback) is enough to make
        // the timer histogram below observe a non-zero duration. Each Configure() call is deferred
        // until immediately before the SUT call it backs - unlike the retry-loop tests, these are two
        // separate, test-controlled SUT operations, so there's no need for the double to compute the
        // delay lazily at invocation time; reconfiguring between them is enough.

        // Arrange + Act (acquire)
        dynamo.Configure()
            .PutItemAsync(new PutItemRequest(), CancellationToken.None)
            .Returns(DelayedPutItemResponseAsync());
        var acquired = await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Arrange + Act (release)
        dynamo.Configure()
            .DeleteItemAsync(new DeleteItemRequest(), CancellationToken.None)
            .Returns(DelayedDeleteItemResponseAsync());
        var released = await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        acquired.Should().BeTrue();
        released.Should().BeTrue();

        var acquisitionTimer = metricAggregator.Collect(MetricNames.LockAcquireTimer).Single();
        acquisitionTimer.Value.Should().BeGreaterThan(4);

        var releaseTimer = metricAggregator.Collect(MetricNames.LockReleaseTimer).Single();
        releaseTimer.Value.Should().BeGreaterThan(4);
    }

    private static async Task<PutItemResponse> DelayedPutItemResponseAsync()
    {
        // simulate some delay to ensure the timer above captures it
        await Task.Delay(TimeSpan.FromMilliseconds(5));
        return new PutItemResponse();
    }

    private static async Task<DeleteItemResponse> DelayedDeleteItemResponseAsync()
    {
        // simulate some delay to ensure the timer above captures it
        await Task.Delay(TimeSpan.FromMilliseconds(5));
        return new DeleteItemResponse();
    }
}
