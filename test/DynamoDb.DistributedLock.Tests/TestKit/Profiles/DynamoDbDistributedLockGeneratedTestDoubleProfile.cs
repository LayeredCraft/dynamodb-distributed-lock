using Compono;

namespace DynamoDb.DistributedLock.Tests.TestKit.Profiles;

/// <summary>
/// Composition profile where <see cref="Amazon.DynamoDBv2.IAmazonDynamoDB"/> resolves to a
/// Compono-generated test double. The only profile this project needs - Compono.TestDoubles now
/// supports both overload-aware argument matching (ADR-0044 Amendment 21,
/// <c>DeleteItemAsyncMatching(...)</c>) and sequential/call-count-based responses (ADR-0054,
/// <c>.ReturnsSequence(...)</c>) directly on the generated double, so the earlier hand-rolled-
/// NSubstitute fallback profile this project used is gone.
/// </summary>
public sealed class DynamoDbDistributedLockGeneratedTestDoubleProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.UseGeneratedTestDoubles();
        DynamoDbDistributedLockCompositionDefaults.Configure(builder);
    }
}
