using System.Diagnostics.Metrics;
using AutoFixture;
using AutoFixture.Kernel;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Tests.Metrics;
using DynamoDb.DistributedLock.Tests.TestKit.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DynamoDb.DistributedLock.Tests.TestKit.SpecimenBuilders;

public class MetricsSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is not Type type)
            return new NoSpecimen();

        if (type == typeof(Meter))
        {
            return new Meter(MetricNames.MeterName);
        }

        if (type == typeof(TestMetricAggregator<double>))
        {
            var meterFactory = context.Create<Meter>();
            return new TestMetricAggregator<double>(meterFactory);
        }
        
        if (type == typeof(TestMetricAggregator<int>))
        {
            var meterFactory = context.Create<Meter>();
            return new TestMetricAggregator<int>(meterFactory);
        }

        if (type == typeof(ILockMetrics))
        {
            var meter = context.Create<Meter>();
            return new LockMetrics(meter);
        }
        
        return new NoSpecimen();
    }
}