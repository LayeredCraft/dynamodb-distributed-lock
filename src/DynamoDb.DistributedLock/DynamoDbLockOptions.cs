using DynamoDb.DistributedLock.Retry;

namespace DynamoDb.DistributedLock;

/// <summary>
/// Configuration options for the DynamoDB-based distributed lock.
/// </summary>
public sealed class DynamoDbLockOptions
{
    /// <summary>
    /// The default name of the configuration section for DynamoDB lock settings.
    /// </summary>
    public const string DynamoDbLockSettings = "DynamoDbLock";
    
    /// <summary>
    /// The name of the DynamoDB table to use.
    /// </summary>
    public required string TableName { get; set; }

    /// <summary>
    /// Lock timeout duration in seconds.
    /// </summary>
    public int LockTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// The name of the partition key attribute.
    /// </summary>
    public string PartitionKeyAttribute { get; set; } = "pk";
    
    /// <summary>
    /// The name of the sort key attribute.
    /// </summary>
    public string SortKeyAttribute { get; set; } = "sk";
    
    /// <summary>
    /// Retry configuration for lock acquisition operations.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();
}