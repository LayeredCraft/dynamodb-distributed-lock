using System.Diagnostics.Metrics;
using DynamoDb.DistributedLock.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DynamoDb.DistributedLock.Tests.Metrics;

public class TestMetricAggregator<T> where T : struct
{
    private readonly Dictionary<string, MetricCollector<T>> _collectors = new();

    public TestMetricAggregator(IMeterFactory meterFactory)
    {
        var names = GetAllMetricNames();
        foreach (var name in names)
        {
            if (name != null)
            {
                var collector = new MetricCollector<T>(meterFactory, MetricNames.MeterName, name);
                _collectors.Add(name, collector);
            }
        }
    }

    public IReadOnlyList<CollectedMeasurement<T>> Collect(string metricName)
    {
        if (_collectors.TryGetValue(metricName, out var collector))
        {
            return collector.GetMeasurementSnapshot();
        }
        throw new Exception($"Metric '{metricName}' was not collected");
    }

    private static IEnumerable<string?> GetAllMetricNames()
    {
        var fields = typeof(MetricNames).GetFields(System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.Static);
        foreach (var field in fields)
        {
            if (field.IsLiteral && field.FieldType == typeof(string))
            {
                yield return field.GetRawConstantValue() as string;
            }
        }
    }
}