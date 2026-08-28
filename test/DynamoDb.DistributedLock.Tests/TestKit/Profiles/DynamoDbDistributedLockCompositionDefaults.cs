using System.Diagnostics.Metrics;
using Amazon.DynamoDBv2;
using Compono;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Retry;
using DynamoDb.DistributedLock.Tests.TestKit.Providers;
using Microsoft.Extensions.Options;

namespace DynamoDb.DistributedLock.Tests.TestKit.Profiles;

/// <summary>
/// Registrations shared by every DynamoDb.DistributedLock composition profile, regardless of how
/// <see cref="IAmazonDynamoDB"/> itself is resolved: a real Meter/ILockMetrics pair (so a
/// <c>[Shared] Meter</c> theory parameter lets a TestMetricAggregator observe what the composed SUT
/// actually publishes), the null-options-by-name provider, and the constructor selections required
/// by types with more than one accessible constructor.
/// </summary>
internal static class DynamoDbDistributedLockCompositionDefaults
{
    internal static void Configure(CompositionBuilder builder)
    {
        builder.Register<Meter>(_ => new Meter(MetricNames.MeterName));
        builder.Register<ILockMetrics>(context => new LockMetrics(context.Resolve<Meter>()));
        builder.AddSemanticProvider(new OptionsValueProvider());

        builder.For<DynamoDbDistributedLock>()
            .UseConstructor<IAmazonDynamoDB, IOptions<DynamoDbLockOptions>, ILockMetrics>();
        builder.For<ExponentialBackoffRetryPolicy>()
            .UseConstructor<RetryOptions, ILockMetrics>();
        // Meter has 4 accessible constructors (CMP0001). The generator still needs a compile-time
        // plan for it even though Register<Meter> above always supplies the real runtime value -
        // this selection is never actually invoked.
        builder.For<Meter>().UseConstructor<string>();
    }
}
