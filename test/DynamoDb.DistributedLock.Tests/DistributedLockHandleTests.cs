using AwesomeAssertions;
using Compono;
using Compono.XunitV3;
using DynamoDb.DistributedLock.Tests.TestKit.Profiles;

namespace DynamoDb.DistributedLock.Tests;

public class DistributedLockHandleTests
{
    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Constructor_WhenLockServiceIsNull_ShouldThrowArgumentNullException(
        string resourceId,
        string ownerId,
        DateTimeOffset expiresAt)
    {
        var act = () => new DistributedLockHandle(null!, resourceId, ownerId, expiresAt);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("lockService");
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Constructor_WhenResourceIdIsNull_ShouldThrowArgumentNullException(
        IDynamoDbDistributedLock lockService,
        string ownerId,
        DateTimeOffset expiresAt)
    {
        var act = () => new DistributedLockHandle(lockService, null!, ownerId, expiresAt);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("resourceId");
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Constructor_WhenOwnerIdIsNull_ShouldThrowArgumentNullException(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        DateTimeOffset expiresAt)
    {
        var act = () => new DistributedLockHandle(lockService, resourceId, null!, expiresAt);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("ownerId");
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void Properties_ShouldReturnConstructorValues(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId,
        DateTimeOffset expiresAt)
    {
        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        handle.ResourceId.Should().Be(resourceId);
        handle.OwnerId.Should().Be(ownerId);
        handle.ExpiresAt.Should().Be(expiresAt);
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void IsAcquired_WhenNotDisposedAndNotExpired_ShouldReturnTrue(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        handle.IsAcquired.Should().BeTrue();
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public void IsAcquired_WhenExpired_ShouldReturnFalse(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        handle.IsAcquired.Should().BeFalse();
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task IsAcquired_WhenDisposed_ShouldReturnFalse(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        await handle.DisposeAsync();

        handle.IsAcquired.Should().BeFalse();
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task DisposeAsync_ShouldCallReleaseLockAsync(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId,
        DateTimeOffset expiresAt)
    {
        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        await handle.DisposeAsync();

        lockService.Verify()
            .ReleaseLockAsync(resourceId, ownerId, Match.Any<CancellationToken>())
            .Once();
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldOnlyCallReleaseLockOnce(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId,
        DateTimeOffset expiresAt)
    {
        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        await handle.DisposeAsync();
        await handle.DisposeAsync();
        await handle.DisposeAsync();

        lockService.Verify()
            .ReleaseLockAsync(resourceId, ownerId, Match.Any<CancellationToken>())
            .Once();
    }

    [Theory]
    [Compose<DynamoDbDistributedLockGeneratedTestDoubleProfile>]
    public async Task DisposeAsync_WhenReleaseLockThrows_ShouldSwallowException(
        IDynamoDbDistributedLock lockService,
        string resourceId,
        string ownerId,
        DateTimeOffset expiresAt)
    {
        lockService.Configure()
            .ReleaseLockAsync(Match.Any<string>(), Match.Any<string>(), Match.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Test exception"));

        var handle = new DistributedLockHandle(lockService, resourceId, ownerId, expiresAt);

        var act = async () => await handle.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
