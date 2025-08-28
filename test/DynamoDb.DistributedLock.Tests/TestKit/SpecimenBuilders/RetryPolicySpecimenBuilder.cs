using System.Diagnostics.Metrics;
using AutoFixture;
using AutoFixture.Kernel;
using DynamoDb.DistributedLock.Metrics;
using DynamoDb.DistributedLock.Retry;

namespace DynamoDb.DistributedLock.Tests.TestKit.SpecimenBuilders;

/// <summary>
/// Creates instances of retry policy types for testing purposes.
/// </summary>
public class RetryPolicySpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is not Type type)
            return new NoSpecimen();

        if (type == typeof(ExponentialBackoffRetryPolicy))
        {
            var options = new RetryOptions
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromSeconds(1),
                BackoffMultiplier = 2.0,
                UseJitter = false,
                Enabled = false
            };
            var lockMetrics = context.Create<ILockMetrics>();
            return new ExponentialBackoffRetryPolicy(options, lockMetrics);
        }

        if (type == typeof(RetryOptions))
        {
            return new RetryOptions
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10), // Fast for tests
                MaxDelay = TimeSpan.FromSeconds(1),
                BackoffMultiplier = 2.0,
                UseJitter = false, // Deterministic for tests
                Enabled = false // Default to disabled for backward compatibility
            };
        }

        return new NoSpecimen();
    }
}