using Amazon.DynamoDBv2;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using DynamoDb.DistributedLock.Tests.TestKit.Extensions;

namespace DynamoDb.DistributedLock.Tests.TestKit.Customizations;

/// <summary>
/// Applies custom fixture configuration for testing DynamoDbDistributedLock.
/// </summary>
public class LockingCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize(new AutoNSubstituteCustomization());
        
        // add metrics customizations
        fixture.AddMetrics();

        // 🔒 Inject a null-value IOptions<DynamoDbLockOptions> for specific test scenarios
        fixture.AddNullDynamoDbLockOptions();

        fixture.AddlDynamoDbLockOptions();
        
        // 🔗 Add DistributedLockHandle customization
        fixture.AddDistributedLockHandle();
        
        // Add DynamoDbDistributedLock
        fixture.AddDynamoDbDistributedLock();
        
        // 🔄 Add retry policy customization
        fixture.AddRetryPolicy();
        
        // ❄️ Freeze core constructor dependencies
        fixture.Freeze<IAmazonDynamoDB>();
    }
}