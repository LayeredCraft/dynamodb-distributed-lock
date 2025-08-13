using System;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamoDb.DistributedLock.Extensions;

/// <summary>
/// Extension methods for registering DynamoDB distributed lock services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DynamoDB distributed lock using configuration bound from the specified section.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configuration">The configuration source.</param>
    /// <param name="sectionName">The name of the configuration section to bind to <see cref="DynamoDbLockOptions"/>.</param>
    /// <param name="awsOptionsSectionName">The name of the configuration section to be read with GetAWSOptions</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddDynamoDbDistributedLock(this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DynamoDbLockOptions.DynamoDbLockSettings, string? awsOptionsSectionName = null)
    {
        var awsOptions = awsOptionsSectionName is not null
            ? configuration.GetAWSOptions(awsOptionsSectionName)
            : null;
        return services.AddDynamoDbDistributedLock(options => configuration.GetSection(sectionName).Bind(options), awsOptions);
    }

    /// <summary>
    /// Registers the DynamoDB distributed lock using a delegate to configure <see cref="DynamoDbLockOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">The delegate to configure <see cref="DynamoDbLockOptions"/>.</param>
    /// <param name="awsOptions">The AWSOptions to be used in configuring the dynamodb service client</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddDynamoDbDistributedLock(this IServiceCollection services,
        Action<DynamoDbLockOptions> configure, AWSOptions? awsOptions = null)
    {
        services.Configure(configure);
        services.AddAWSService<IAmazonDynamoDB>(awsOptions);
        services.AddSingleton<IDynamoDbDistributedLock, DynamoDbDistributedLock>();
        return services;
    }
}