using Compono;
using Microsoft.Extensions.Options;

namespace DynamoDb.DistributedLock.Tests.TestKit.Providers;

/// <summary>
/// Supplies <see cref="IOptions{TOptions}"/> of <see cref="DynamoDbLockOptions"/>, returning a
/// null-valued instance for a parameter named "nullOptions" so constructor-null-check tests can
/// request it by name, and an ordinary composed instance otherwise.
/// </summary>
public sealed class OptionsValueProvider : ICompositionValueProvider
{
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (request.RequestedType != typeof(IOptions<DynamoDbLockOptions>))
            return CompositionProviderResult.NotHandled;

        if (string.Equals(request.Name, "nullOptions", StringComparison.OrdinalIgnoreCase))
            return CompositionProviderResult.Handled(Options.Create<DynamoDbLockOptions>(null!));

        return CompositionProviderResult.Handled(Options.Create(context.Resolve<DynamoDbLockOptions>()));
    }
}
