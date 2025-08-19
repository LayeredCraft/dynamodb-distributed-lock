using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AutoFixture.Xunit3;
using DynamoDb.DistributedLock.Tests.TestKit.Attributes;
using AwesomeAssertions;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Tests.Metrics;
using DynamoDb.DistributedLock.Tests.TestKit.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DynamoDb.DistributedLock.Tests;

public class DynamoDbDistributedLockTests
{
    [Theory]
    [DynamoDbDistributedLockAutoData]
    public void Constructor_WhenClientIsNull_ShouldThrowArgumentNullException(IOptions<DynamoDbLockOptions> options)
    {
        Action act = () => _ = new DynamoDbDistributedLock(null!, options);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("client");
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public void Constructor_WhenOptionsValueIsNull_ShouldThrowArgumentNullException(
        IAmazonDynamoDB client,
        IOptions<DynamoDbLockOptions> nullOptions)
    {
        var act = () => _ = new DynamoDbDistributedLock(client, nullOptions);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("options");
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenLockIsAvailable_ShouldReturnTrue(
        [Frozen] IAmazonDynamoDB dynamo, TestMetricAggregator<int> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(0);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenLockAlreadyExists_ShouldReturnFalse(
        [Frozen] IAmazonDynamoDB dynamo, TestMetricAggregator<int> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConditionalCheckFailedException("lock exists"));

        // Act
        var result = await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenUnexpectedExceptionOccurs_ShouldThrow(
        [Frozen] IAmazonDynamoDB dynamo, TestMetricAggregator<int> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("unexpected failure"));

        // Act
        var act = async () => await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ReleaseLockAsync_WhenOwnerMatches_ShouldReturnTrue(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act
        var result = await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        metricAggregator.Collect(MetricNames.LockRelease).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockReleaseFailed).Should().HaveCount(0);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ReleaseLockAsync_WhenOwnerDoesNotMatch_ShouldReturnFalse(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConditionalCheckFailedException("owner mismatch"));

        // Act
        var result = await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        metricAggregator.Collect(MetricNames.LockRelease).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockReleaseFailed).Should().HaveCount(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task ReleaseLockAsync_WhenUnexpectedExceptionOccurs_ShouldThrow(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("unexpected failure"));

        // Act
        var act = async () => await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        metricAggregator.Collect(MetricNames.LockRelease).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockReleaseFailed).Should().HaveCount(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockHandleAsync_WhenLockIsAvailable_ShouldReturnHandle(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

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
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockHandleAsync_WhenLockAlreadyExists_ShouldReturnNull(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConditionalCheckFailedException("lock exists"));

        // Act
        var result = await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockHandleAsync_WhenUnexpectedExceptionOccurs_ShouldThrow(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("unexpected failure"));

        // Act
        var act = async () => await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(0);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(1);
    }

    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockHandleAsync_DisposeHandle_ShouldCallReleaseLock(
        [Frozen] IAmazonDynamoDB dynamo,
        DynamoDbDistributedLock sut,
        TestMetricAggregator<int> metricAggregator,
        string resourceId,
        string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());
        dynamo.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act
        var handle = await sut.AcquireLockHandleAsync(resourceId, ownerId, CancellationToken.None);
        await handle!.DisposeAsync();

        // Assert
        await dynamo.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(req => 
                req.ConditionExpression.Contains("ownerId = :owner") &&
                req.ExpressionAttributeValues.ContainsKey(":owner") &&
                req.ExpressionAttributeValues[":owner"].S == ownerId),
            Arg.Any<CancellationToken>());
        
        metricAggregator.Collect(MetricNames.LockAcquire).Should().HaveCount(1);
        metricAggregator.Collect(MetricNames.LockAcquireFailed).Should().HaveCount(0);
    }
    
    [Theory]
    [DynamoDbDistributedLockAutoData]
    public async Task AcquireLockAsync_WhenLockIsAvailable_TimersRecordMetrics(
        [Frozen] IAmazonDynamoDB dynamo, TestMetricAggregator<double> metricAggregator,
        DynamoDbDistributedLock sut, string resourceId, string ownerId)
    {
        // Arrange
        dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // simulate some delay to ensure timer captures it
                await Task.Delay(TimeSpan.FromMilliseconds(5));
                return new PutItemResponse();
            });

        dynamo.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // simulate some delay to ensure timer captures it
                await Task.Delay(TimeSpan.FromMilliseconds(5));
                return new DeleteItemResponse();
            });

        // Act
        var acquired = await sut.AcquireLockAsync(resourceId, ownerId, CancellationToken.None);
        var released = await sut.ReleaseLockAsync(resourceId, ownerId, CancellationToken.None);

        // Assert
        acquired.Should().BeTrue();
        released.Should().BeTrue();
        
        var acquisitionTimer = metricAggregator.Collect(MetricNames.LockAcquireTimer).Single();
        acquisitionTimer.Value.Should().BeGreaterThan(4);
        
        var releaseTimer = metricAggregator.Collect(MetricNames.LockReleaseTimer).Single();
        releaseTimer.Value.Should().BeGreaterThan(4);
    }
}