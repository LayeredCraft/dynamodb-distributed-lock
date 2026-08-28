using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using AwesomeAssertions;
using Compono;
using Compono.XunitV3;
using DynamoDb.DistributedLock.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DynamoDb.DistributedLock.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    // This only needs a working IAmazonDynamoDB instance to bypass credential resolution - no
    // Configure()/Verify() call needed here.
    private static IAmazonDynamoDB CreateDynamoDbDouble() =>
        Composer.Create(builder => builder.UseGeneratedTestDoubles()).Create<IAmazonDynamoDB>();

    [Fact]
    public void AddDynamoDbDistributedLock_WithAction_SetsUpServiceAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDynamoDbDistributedLock(options =>
        {
            options.TableName = "locks";
            options.LockTimeoutSeconds = 45;
        });

        // 👇 Override with a double AFTER to bypass credential resolution
        services.AddSingleton(CreateDynamoDbDouble());

        var provider = services.BuildServiceProvider();

        // Assert
        var lockService = provider.GetService<IDynamoDbDistributedLock>();
        lockService.Should().NotBeNull();

        var options = provider.GetRequiredService<IOptions<DynamoDbLockOptions>>().Value;
        options.TableName.Should().Be("locks");
        options.LockTimeoutSeconds.Should().Be(45);
        options.PartitionKeyAttribute.Should().Be("pk");
        options.SortKeyAttribute.Should().Be("sk");
    }

    [Fact]
    public void AddDynamoDbDistributedLock_WithConfiguration_BindsOptionsCorrectly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            ["DynamoDbLock:TableName"] = "my-table",
            ["DynamoDbLock:LockTimeoutSeconds"] = "60"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddDynamoDbDistributedLock(configuration);
        // 👇 Override with a double AFTER to bypass credential resolution
        services.AddSingleton(CreateDynamoDbDouble());
        var provider = services.BuildServiceProvider();

        // Assert
        var lockService = provider.GetService<IDynamoDbDistributedLock>();
        lockService.Should().NotBeNull();

        var options = provider.GetRequiredService<IOptions<DynamoDbLockOptions>>().Value;
        options.TableName.Should().Be("my-table");
        options.LockTimeoutSeconds.Should().Be(60);
        options.PartitionKeyAttribute.Should().Be("pk");
        options.SortKeyAttribute.Should().Be("sk");
    }

    [Theory]
    [Compose]
    public void AddDynamoDbDistributedLock_WithAction_SetsCustomKeyAttributes(string partitionKey, string sortKey)
    {
        var services = new ServiceCollection();

        services.AddDynamoDbDistributedLock(options =>
        {
            options.TableName = "locks";
            options.LockTimeoutSeconds = 45;
            options.PartitionKeyAttribute = partitionKey;
            options.SortKeyAttribute = sortKey;
        });

        services.AddSingleton(CreateDynamoDbDouble());
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DynamoDbLockOptions>>().Value;

        options.PartitionKeyAttribute.Should().Be(partitionKey);
        options.SortKeyAttribute.Should().Be(sortKey);
    }

    [Theory]
    [Compose]
    public void AddDynamoDbDistributedLock_WithConfiguration_BindsCustomKeyAttributes(string partitionKey, string sortKey)
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            ["DynamoDbLock:TableName"] = "my-table",
            ["DynamoDbLock:LockTimeoutSeconds"] = "60",
            ["DynamoDbLock:PartitionKeyAttribute"] = partitionKey,
            ["DynamoDbLock:SortKeyAttribute"] = sortKey
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var services = new ServiceCollection();
        services.AddDynamoDbDistributedLock(configuration);
        services.AddSingleton(CreateDynamoDbDouble());
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DynamoDbLockOptions>>().Value;

        options.PartitionKeyAttribute.Should().Be(partitionKey);
        options.SortKeyAttribute.Should().Be(sortKey);
    }

    [Fact]
    public void AddDynamoDbDistributedLock_WithActionAndAwsConfig_SetsUpServiceAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var awsOptions = new AWSOptions
        {
            DefaultClientConfig = { ServiceURL = "http://localhost/" },
            // to allow the service client to be resolved without actual AWS credentials
            Credentials = new AnonymousAWSCredentials(),
        };

        // Act
        services.AddDynamoDbDistributedLock(options =>
        {
            options.TableName = "locks";
            options.LockTimeoutSeconds = 45;
        }, awsOptions);

        var provider = services.BuildServiceProvider();

        // Assert
        var lockService = provider.GetService<IDynamoDbDistributedLock>();
        lockService.Should().NotBeNull();

        var options = provider.GetRequiredService<IOptions<DynamoDbLockOptions>>().Value;
        options.TableName.Should().Be("locks");
        options.LockTimeoutSeconds.Should().Be(45);
        options.PartitionKeyAttribute.Should().Be("pk");
        options.SortKeyAttribute.Should().Be("sk");

        var dynamoDbClient = provider.GetRequiredService<IAmazonDynamoDB>();
        dynamoDbClient.Config.ServiceURL.Should().Be("http://localhost/");
    }
}
