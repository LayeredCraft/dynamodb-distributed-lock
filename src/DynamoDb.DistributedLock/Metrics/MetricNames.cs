namespace DynamoDb.DistributedLock.Metrics;

/// <summary>
/// Names of metrics that are published by DynamoDb.DistributedLock
/// </summary>
public static class MetricNames
{
    /// <summary>
    /// Name of the Meter used for publishing metrics.
    /// </summary>
    public const string MeterName = "DynamoDb.DistributedLock";
    
    /// <summary>
    /// A lock is successfully released.
    /// </summary>
    public const string LockRelease = "dynamodb.distributedlock.lock_release";
    /// <summary>
    /// A lock release operation failed.
    /// </summary>
    public const string LockReleaseFailed = "dynamodb.distributedlock.lock_release.failed";
    /// <summary>
    /// A lock is successfully acquired.
    /// </summary>
    public const string LockAcquire = "dynamodb.distributedlock.lock_acquire";
    /// <summary>
    /// A lock acquisition operation failed.
    /// </summary>
    public const string LockAcquireFailed = "dynamodb.distributedlock.lock_acquire.failed";
    /// <summary>
    /// Retries where attempted, but the maximum number of retries was reached without success.
    /// </summary>
    public const string RetriesExhausted = "dynamodb.distributedlock.retries_exhausted";
    /// <summary>
    /// A lock acquisition retry was attempted after a failure. When retrying an operation, the first attempt is not counted.
    /// </summary>
    public const string RetryAttempt = "dynamodb.distributedlock.retry_attempt";
    /// <summary>
    /// Measures the time taken to acquire a lock.
    /// </summary>
    public const string LockAcquireTimer = "dynamodb.distributedlock.lock_acquire.timer";
    /// <summary>
    /// Measures the time taken to release a lock.
    /// </summary>
    public const string LockReleaseTimer = "dynamodb.distributedlock.lock_release.timer";
}