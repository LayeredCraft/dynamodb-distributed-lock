using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DynamoDb.DistributedLock.Metrics;

/// <inheritdoc cref="ILockMetrics" />
public sealed class LockMetrics : ILockMetrics, IDisposable
{
    private readonly Meter _meter;

    private readonly Counter<int> _lockAcquire;
    private readonly Counter<int> _lockRelease;
    private readonly Counter<int> _lockAcquireFailed;
    private readonly Counter<int> _lockReleaseFailed;
    private readonly Counter<int> _retriesExhausted;
    private readonly Counter<int> _retryAttempt;
    private readonly Histogram<double> _lockAcquireTimer;
    private readonly Histogram<double> _lockReleaseTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockMetrics"/> class with the specified <see cref="Meter"/>.
    /// </summary>
    /// <param name="meter"></param>
    public LockMetrics(Meter meter)
    {
        ArgumentNullException.ThrowIfNull(meter);
        _meter = meter;

        _lockAcquire        = _meter.CreateCounter<int>(MetricNames.LockAcquire,        unit: "count");
        _lockRelease        = _meter.CreateCounter<int>(MetricNames.LockRelease,        unit: "count");
        _lockAcquireFailed  = _meter.CreateCounter<int>(MetricNames.LockAcquireFailed,  unit: "count");
        _lockReleaseFailed  = _meter.CreateCounter<int>(MetricNames.LockReleaseFailed,  unit: "count");
        _retriesExhausted   = _meter.CreateCounter<int>(MetricNames.RetriesExhausted,   unit: "count");
        _retryAttempt       = _meter.CreateCounter<int>(MetricNames.RetryAttempt,       unit: "count");
        _lockAcquireTimer   = _meter.CreateHistogram<double>(MetricNames.LockAcquireTimer, unit: "ms");
        _lockReleaseTimer   = _meter.CreateHistogram<double>(MetricNames.LockReleaseTimer, unit: "ms");
    }

    /// <summary>
    /// A default instance of <see cref="LockMetrics"/> using a meter with the name defined by <see cref="MetricNames.MeterName"/>.
    /// </summary>
    public static LockMetrics Default { get; } = new(new Meter(MetricNames.MeterName));

    /// <inheritdoc />
    public IDisposable TrackLockAcquire() => new TimerScope(_lockAcquireTimer);

    /// <inheritdoc />
    public IDisposable TrackLockRelease() => new TimerScope(_lockReleaseTimer);

    /// <inheritdoc />
    public void LockAcquired() => _lockAcquire.Add(1);

    /// <inheritdoc />
    public void LockReleased() => _lockRelease.Add(1);

    /// <inheritdoc />
    public void LockAcquireFailed(string reason)
    {
        var tags = new TagList { { "reason", reason } };
        _lockAcquireFailed.Add(1, tags);
    }

    /// <inheritdoc />
    public void LockReleaseFailed(string reason)
    {
        var tags = new TagList { { "reason", reason } };
        _lockReleaseFailed.Add(1, tags);
    }

    /// <inheritdoc />
    public void RetryAttempt() => _retryAttempt.Add(1);

    /// <inheritdoc />
    public void RetriesExhausted() => _retriesExhausted.Add(1);

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();

    private readonly struct TimerScope(Histogram<double> hist) : IDisposable
    {
        private readonly long _start = Stopwatch.GetTimestamp();

        public void Dispose()
        {
            var ms = (Stopwatch.GetTimestamp() - _start) * 1000.0 / Stopwatch.Frequency;
            hist.Record(ms);
        }
    }
}