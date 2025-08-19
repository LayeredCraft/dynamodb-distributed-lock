using System.Diagnostics.Metrics;
using AutoFixture;
using AutoFixture.Kernel;
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

        if (type == typeof(IMeterFactory))
        {
            // the DefaultMeterFactory is an internal type
            // we could implement a mock here but just doing this now to get it working
            var services = new ServiceCollection();
            services.AddMetrics();
            var serviceProvider = services.BuildServiceProvider();
            var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
            return meterFactory;
        }

        if (type == typeof(TestMetricAggregator<double>))
        {
            var meterFactory = context.Create<IMeterFactory>();
            return new TestMetricAggregator<double>(meterFactory);
        }
        
        if (type == typeof(TestMetricAggregator<int>))
        {
            var meterFactory = context.Create<IMeterFactory>();
            return new TestMetricAggregator<int>(meterFactory);
        }
        
        return new NoSpecimen();
    }
}