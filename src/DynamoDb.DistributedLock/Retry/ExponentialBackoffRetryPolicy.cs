using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using DynamoDb.DistributedLock.Metrics;

namespace DynamoDb.DistributedLock.Retry;

/// <summary>
/// Implements an exponential backoff retry policy with optional jitter.
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly RetryOptions _options;
    private readonly Meter _meter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialBackoffRetryPolicy"/> class.
    /// </summary>
    /// <param name="options">The retry configuration options.</param>
    /// <param name="meterFactory">Factory for meters used in telemetry collection</param>
    public ExponentialBackoffRetryPolicy(RetryOptions options, IMeterFactory? meterFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // use the IMeterFactory if it is available, otherwise create a new Meter instance.
        // The DefaultMeterFactory will cache things and improve performance.
        _meter = meterFactory?.Create(MetricNames.MeterName) ?? new Meter(MetricNames.MeterName);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (shouldRetry == null) throw new ArgumentNullException(nameof(shouldRetry));

        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.MaxAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // only increment as a retry attempt if this is not the first attempt
                if (attempt != 0)
                {
                    _meter.RetryAttempt().Add(1);
                }
                return await operation(cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                if (attempt >= _options.MaxAttempts || !shouldRetry(ex))
                {
                    _meter.RetriesExhausted().Add(1);
                    throw;
                }

                var delay = CalculateDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // This should never be reached due to the throw in the catch block,
        // but the compiler requires it for definite assignment
        throw lastException ?? new InvalidOperationException("Retry attempts exhausted");
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        var exponentialDelay = TimeSpan.FromMilliseconds(
            _options.BaseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attempt - 1));

        var delay = exponentialDelay > _options.MaxDelay ? _options.MaxDelay : exponentialDelay;

        if (_options.UseJitter)
        {
            // Add random jitter to avoid thundering herd
            var jitterRange = delay.TotalMilliseconds * _options.JitterFactor;
            var jitter = Random.Shared.NextDouble() * jitterRange;
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        }

        return delay;
    }
}