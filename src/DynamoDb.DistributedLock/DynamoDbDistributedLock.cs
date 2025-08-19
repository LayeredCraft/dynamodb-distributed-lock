using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Retry;
using Microsoft.Extensions.Options;

namespace DynamoDb.DistributedLock;

/// <summary>
/// Implements a DynamoDB-backed distributed lock mechanism.
/// </summary>
public class DynamoDbDistributedLock : IDynamoDbDistributedLock
{
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbLockOptions _options;
    private readonly Lazy<IRetryPolicy> _retryPolicy;
    private readonly Meter _meter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamoDbDistributedLock"/> class.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="options">Configuration options for the lock.</param>
    /// <param name="meterFactory">Creates Meters for use in telemetry</param>
    public DynamoDbDistributedLock(IAmazonDynamoDB client,
        IOptions<DynamoDbLockOptions> options,
        IMeterFactory? meterFactory = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _retryPolicy = new Lazy<IRetryPolicy>(() => new ExponentialBackoffRetryPolicy(_options.Retry));
        // use the IMeterFactory if it is available, otherwise create a new Meter instance.
        // The DefaultMeterFactory will cache things and improve performance.
        _meter = meterFactory?.Create(MetricNames.MeterName) ?? new Meter(MetricNames.MeterName);
    }

    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource.
    /// </summary>
    /// <param name="resourceId">The resource identifier (e.g., a game or operation name).</param>
    /// <param name="ownerId">The unique ID of the lock owner.</param>
    /// <param name="cancellationToken">A cancellation token for the async operation.</param>
    /// <returns><c>true</c> if the lock was acquired; otherwise, <c>false</c>.</returns>
    public async Task<bool> AcquireLockAsync(string resourceId, string ownerId, CancellationToken cancellationToken = default)
    {
        using var _ = _meter.LockAcquireTimer().Start();
        var result = await TryAcquireLockInternalAsync(resourceId, ownerId, cancellationToken);
        return result.IsSuccess;
    }

    /// <summary>
    /// Releases a previously acquired distributed lock.
    /// </summary>
    /// <param name="resourceId">The resource identifier (e.g., a game or operation name).</param>
    /// <param name="ownerId">The unique ID of the lock owner requesting release.</param>
    /// <param name="cancellationToken">A cancellation token for the async operation.</param>
    /// <returns><c>true</c> if the lock was released; <c>false</c> if the lock was not owned by the caller.</returns>
    public async Task<bool> ReleaseLockAsync(string resourceId, string ownerId, CancellationToken cancellationToken = default)
    {
        using var _ = _meter.LockReleaseTimer().Start();
        var request = new DeleteItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [_options.PartitionKeyAttribute] = new() { S = $"lock#{resourceId}" },
                [_options.SortKeyAttribute] = new() { S = "metadata#lock" },
            },
            ConditionExpression = "ownerId = :owner",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":owner"] = new() { S = ownerId }
            }
        };

        try
        {
            await _client.DeleteItemAsync(request, cancellationToken);
            _meter.LockReleased().Add(1);
            return true; // Lock released
        }
        catch (ConditionalCheckFailedException)
        {
            _meter.LockReleaseFailed().Add(1, "not_owned");
            return false; // Lock was held by another process
        }
        catch
        {
            _meter.LockReleaseFailed().Add(1, "unexpected_exception");
            throw;
        }
    }

    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource and returns a handle for automatic cleanup.
    /// </summary>
    /// <param name="resourceId">The resource identifier (e.g., a game or operation name).</param>
    /// <param name="ownerId">The unique ID of the lock owner.</param>
    /// <param name="cancellationToken">A cancellation token for the async operation.</param>
    /// <returns>An <see cref="IDistributedLockHandle"/> if the lock was successfully acquired; otherwise, <c>null</c>.</returns>
    public async Task<IDistributedLockHandle?> AcquireLockHandleAsync(string resourceId, string ownerId, CancellationToken cancellationToken = default)
    {
        using var _ = _meter.LockAcquireTimer().Start();
        var result = await TryAcquireLockInternalAsync(resourceId, ownerId, cancellationToken);
        return result.IsSuccess ? new DistributedLockHandle(this, resourceId, ownerId, result.ExpiresAt) : null;
    }

    private async Task<LockAcquisitionResult> TryAcquireLockInternalAsync(string resourceId, string ownerId, CancellationToken cancellationToken)
    {
        if (!_options.Retry.Enabled)
        {
            return await TryAcquireLockOnceAsync(resourceId, ownerId, suppressExceptions: true, cancellationToken);
        }

        try
        {
            return await _retryPolicy.Value.ExecuteAsync(
                async ct => await TryAcquireLockOnceAsync(resourceId, ownerId, suppressExceptions: false, ct),
                ShouldRetryLockAcquisition,
                cancellationToken);
        }
        catch (Exception ex) when (ShouldRetryLockAcquisition(ex))
        {
            // After all retry attempts failed due to retriable exceptions (lock conflicts, throttling, etc.)
            return new LockAcquisitionResult(false, default);
        }
    }

    private async Task<LockAcquisitionResult> TryAcquireLockOnceAsync(string resourceId, string ownerId, bool suppressExceptions, CancellationToken cancellationToken)
    {
        var (request, expiresAt) = CreatePutItemRequest(resourceId, ownerId);

        try
        {
            await _client.PutItemAsync(request, cancellationToken);
            _meter.LockAcquired().Add(1);
            return new LockAcquisitionResult(true, expiresAt);
        }
        catch (ConditionalCheckFailedException) when (suppressExceptions)
        {
            _meter.LockAcquireFailed().Add(1, "condition_check_failed");
            return new LockAcquisitionResult(false, default);
        }
        catch
        {
            _meter.LockAcquireFailed().Add(1, "exception_taking_lock");
            throw;
        }
    }

    private (PutItemRequest Request, DateTimeOffset ExpiresAt) CreatePutItemRequest(string resourceId, string ownerId)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddSeconds(_options.LockTimeoutSeconds);
        var expiresAtUnix = expiresAt.ToUnixTimeSeconds();

        var request = new PutItemRequest
        {
            TableName = _options.TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                [_options.PartitionKeyAttribute] = new() { S = $"lock#{resourceId}" },
                [_options.SortKeyAttribute] = new() { S = "metadata#lock" },
                ["ownerId"] = new() { S = ownerId },
                ["expiresAt"] = new() { N = expiresAtUnix.ToString() }
            },
            ConditionExpression = $"(attribute_not_exists({_options.PartitionKeyAttribute}) AND attribute_not_exists({_options.SortKeyAttribute})) OR expiresAt < :now",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":now"] = new() { N = now.ToUnixTimeSeconds().ToString() }
            }
        };

        return (request, expiresAt);
    }

    private static bool ShouldRetryLockAcquisition(Exception exception)
    {
        return exception switch
        {
            ConditionalCheckFailedException => true, // Lock is held by another process - retry
            ProvisionedThroughputExceededException => true, // DynamoDB throttling - retry
            InternalServerErrorException => true, // DynamoDB internal error - retry
            RequestLimitExceededException => true, // DynamoDB request rate exceeded - retry
            _ => false // Other exceptions should not be retried
        };
    }

    private readonly record struct LockAcquisitionResult(bool IsSuccess, DateTimeOffset ExpiresAt);
}