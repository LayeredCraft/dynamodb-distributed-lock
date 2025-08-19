using System.Diagnostics.Metrics;
using AutoFixture;
using DynamoDb.DistributedLock.Tests.TestKit.SpecimenBuilders;

namespace DynamoDb.DistributedLock.Tests.TestKit.Extensions;

/// <summary>
/// Provides extension methods for configuring <see cref="IFixture"/> with DynamoDb.DistributedLock test customizations.
/// </summary>
public static class FixtureExtensions
{
    /// <summary>
    /// Adds a customization that injects an <see cref="IOptions{T}"/> with a null <c>Value</c> when a parameter named <c>nullOptions</c> is requested.
    /// </summary>
    /// <param name="fixture">The AutoFixture instance to customize.</param>
    /// <returns>The same <see cref="IFixture"/> instance for chaining.</returns>
    public static IFixture AddNullDynamoDbLockOptions(this IFixture fixture)
    {
        fixture.Customizations.Add(new NullOptionsSpecimenBuilder<DynamoDbLockOptions>());
        return fixture;
    }

    /// <summary>
    /// Adds a customization that injects an <see cref="IOptions{T}"/>.
    /// </summary>
    /// <param name="fixture">The AutoFixture instance to customize.</param>
    /// <returns>The same <see cref="IFixture"/> instance for chaining.</returns>
    public static IFixture AddlDynamoDbLockOptions(this IFixture fixture)
    {
        fixture.Customizations.Add(new OptionsSpecimenBuilder<DynamoDbLockOptions>());
        return fixture;
    }

    /// <summary>
    /// Adds a customization that creates instances of <see cref="DistributedLockHandle"/> for testing.
    /// </summary>
    /// <param name="fixture">The AutoFixture instance to customize.</param>
    /// <returns>The same <see cref="IFixture"/> instance for chaining.</returns>
    public static IFixture AddDistributedLockHandle(this IFixture fixture)
    {
        fixture.Customizations.Add(new DistributedLockHandleSpecimenBuilder());
        return fixture;
    }

    /// <summary>
    /// Adds a customization that creates retry policy instances for testing.
    /// </summary>
    /// <param name="fixture">The AutoFixture instance to customize.</param>
    /// <returns>The same <see cref="IFixture"/> instance for chaining.</returns>
    public static IFixture AddRetryPolicy(this IFixture fixture)
    {
        fixture.Customizations.Add(new RetryPolicySpecimenBuilder());
        return fixture;
    }
    
    /// <summary>
    /// Adds customization that creates instances needed for metrics collection in tests.
    /// </summary>
    /// <param name="fixture">The AutoFixture instance to customize.</param>
    /// <returns>The same <see cref="IFixture"/> instance for chaining.</returns>
    public static IFixture AddMetrics(this IFixture fixture)
    {
        fixture.Customizations.Add(new MetricsSpecimenBuilder());
        fixture.Freeze<IMeterFactory>();
        return fixture;
    }
}