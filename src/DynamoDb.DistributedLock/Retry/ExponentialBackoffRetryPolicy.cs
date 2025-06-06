using System;
using System.Threading;
using System.Threading.Tasks;

namespace DynamoDb.DistributedLock.Retry;

/// <summary>
/// Implements an exponential backoff retry policy with optional jitter.
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly RetryOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialBackoffRetryPolicy"/> class.
    /// </summary>
    /// <param name="options">The retry configuration options.</param>
    public ExponentialBackoffRetryPolicy(RetryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _random = new Random();
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
                return await operation(cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                if (attempt >= _options.MaxAttempts || !shouldRetry(ex))
                {
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
            // Add random jitter up to 25% of the delay to avoid thundering herd
            var jitterRange = delay.TotalMilliseconds * 0.25;
            var jitter = _random.NextDouble() * jitterRange;
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        }

        return delay;
    }
}