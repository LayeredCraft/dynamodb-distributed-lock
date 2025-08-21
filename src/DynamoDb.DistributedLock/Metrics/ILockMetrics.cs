using System;

namespace DynamoDb.DistributedLock.Metrics;

/// <summary>
/// Defines methods for tracking metrics related to distributed lock operations.
/// </summary>
public interface ILockMetrics
{
    // Timers (use with 'using' to record elapsed milliseconds)
    /// <summary>
    /// Creates a timer to track the duration of a lock acquisition attempt.
    /// </summary>
    /// <returns>IDisposable that tracks start time based on time created, and publishes end time based on disposal time</returns>
    IDisposable TrackLockAcquire();
    /// <summary>
    /// Creates a timer to track the duration of a lock release attempt.
    /// </summary>
    /// <returns>IDisposable that tracks start time based on time created, and publishes end time based on disposal time</returns>
    IDisposable TrackLockRelease();

    // Counters
    /// <summary>
    /// A lock was successfully acquired.
    /// </summary>
    void LockAcquired();
    /// <summary>
    /// A lock was successfully released.
    /// </summary>
    void LockReleased();
    /// <summary>
    /// A lock acquisition attempt failed.
    /// </summary>
    /// <param name="reason">tag value for the reason the failure occured</param>
    void LockAcquireFailed(string reason);   // e.g., "not_owned", "timeout", "unexpected_exception"
    /// <summary>
    /// A lock release attempt failed.
    /// </summary>
    /// <param name="reason">tag value for the reason the failure occured</param>
    void LockReleaseFailed(string reason);   // e.g., "not_owned", "unexpected_exception"
    /// <summary>
    /// A retry attempt was made during lock acquisition.
    /// </summary>
    void RetryAttempt();
    /// <summary>
    /// All retry attempts were exhausted without acquiring the lock.
    /// </summary>
    void RetriesExhausted();
}