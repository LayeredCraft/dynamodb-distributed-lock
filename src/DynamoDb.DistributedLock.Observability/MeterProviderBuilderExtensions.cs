using System;
using DynamoDb.DistributedLock.Metrics;
using OpenTelemetry.Metrics;

namespace DynamoDb.DistributedLock.Observability;

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> to add DynamoDB Distributed Lock metrics.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables metrics collection for DynamoDB Distributed Lock operations.
    /// This includes counters for lock acquisition/release events and histograms for timing measurements.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static MeterProviderBuilder AddDynamoDbDistributedLock(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(MetricNames.MeterName);
    }
}