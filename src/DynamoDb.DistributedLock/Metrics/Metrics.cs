using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace DynamoDb.DistributedLock.Metrics;

internal static partial class Metrics
{
    [Counter<int>(Name = MetricNames.LockAcquire)]
    public static partial LockAcquired LockAcquired(this Meter meter);
    
    [Counter<int>(Name = MetricNames.LockRelease)]
    public static partial LockReleased LockReleased(this Meter meter);
    
    [Counter<int>("reason", Name = MetricNames.LockReleaseFailed)]
    public static partial LockReleaseFailed LockReleaseFailed(this Meter meter);
    
    [Counter<int>("reason", Name = MetricNames.LockAcquireFailed)]
    public static partial LockAcquireFailed LockAcquireFailed(this Meter meter);
    
    [Counter<int>(Name = MetricNames.RetriesExhausted)]
    public static partial RetriesExhausted RetriesExausted(this Meter meter);
    
    [Counter<int>(Name = MetricNames.RetryAttempt)]
    public static partial RetryAttempt RetryAttempt(this Meter meter);
    
    [Histogram<double>(Name = MetricNames.LockAcquireTimer)]
    public static partial LockAcquireTimer LockAcquireTimer(this Meter meter);
    
    [Histogram<double>(Name = MetricNames.LockReleaseTimer)]
    public static partial LockReleaseTimer LockReleaseTimer(this Meter meter);
    
    public static HistogramTimer Start(this LockAcquireTimer timer) => new(timer.Record);
    public static HistogramTimer Start(this LockReleaseTimer timer) => new(timer.Record);
}
// It seems like there should be a better way to get the benefits of source generated metrics
// without needing to implement a timer for each histogram generated type, but the generated types can't be partial
internal readonly struct HistogramTimer(Action<double> record) : IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public void Dispose()
    {
        _stopwatch.Stop();
        record(_stopwatch.ElapsedMilliseconds);
    }
}