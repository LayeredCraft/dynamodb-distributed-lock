# 🔒 DynamoDb.DistributedLock
<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-2-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

**DynamoDb.DistributedLock** is a lightweight .NET library for distributed locking using Amazon DynamoDB. It is designed for serverless and cloud-native applications that require coordination across services or instances.

- ✅ Safe and atomic lock acquisition using conditional writes
- ✅ TTL-based expiration to prevent stale locks
- ✅ AWS-native, no external infrastructure required
- ✅ Simple `IDynamoDbDistributedLock` interface
- ✅ **IAsyncDisposable support** for automatic lock cleanup
- ✅ **Retry logic** with exponential backoff for handling lock contention and throttling
- ✅ Tested and production-ready for .NET 8 and 9

---

## 📦 Package

| Package                     | Build | NuGet                                                                                                                                                                                      | Downloads                                                                                 |
|----------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| **DynamoDb.DistributedLock** | [![Build](https://github.com/LayeredCraft/dynamodb-distributed-lock/actions/workflows/build.yaml/badge.svg)](https://github.com/LayeredCraft/dynamodb-distributed-lock/actions/workflows/build.yaml) | [![NuGet](https://img.shields.io/nuget/v/DynamoDb.DistributedLock.svg)](https://www.nuget.org/packages/DynamoDb.DistributedLock)                                                           | [![NuGet Downloads](https://img.shields.io/nuget/dt/DynamoDb.DistributedLock.svg)](https://www.nuget.org/packages/DynamoDb.DistributedLock) |

---

## 🚀 Getting Started

### 1. Install the NuGet package

```bash
dotnet add package DynamoDb.DistributedLock
```

### 2. Register the lock in your DI container

```csharp
services.AddDynamoDbDistributedLock(options =>
{
    options.TableName = "my-lock-table";
    options.LockTimeoutSeconds = 30;
    options.PartitionKeyAttribute = "pk";
    options.SortKeyAttribute = "sk";
});
```

Or bind from configuration:

```csharp
services.AddDynamoDbDistributedLock(configuration);
```

### appsettings.json
```json
{
  "DynamoDbLock": {
    "TableName": "my-lock-table",
    "LockTimeoutSeconds": 30,
    "PartitionKeyAttribute": "pk",
    "SortKeyAttribute": "sk"
  }
}
```

### 3. Use the lock

#### Recommended: IAsyncDisposable Pattern (v1.1.0+)

```csharp
public class MyService(IDynamoDbDistributedLock distributedLock)
{
    public async Task<bool> TryDoWorkAsync()
    {
        await using var lockHandle = await distributedLock.AcquireLockHandleAsync("resource-1", "owner-abc");
        if (lockHandle == null) return false; // Lock not acquired

        // 🔧 Critical section - lock automatically released when disposed
        // Your protected code here...
        
        return true;
    }
}
```

#### Traditional Pattern

```csharp
public class MyService(IDynamoDbDistributedLock distributedLock)
{
    public async Task<bool> TryDoWorkAsync()
    {
        var acquired = await distributedLock.AcquireLockAsync("resource-1", "owner-abc");
        if (!acquired) return false;

        try
        {
            // 🔧 Critical section
        }
        finally
        {
            await distributedLock.ReleaseLockAsync("resource-1", "owner-abc");
        }

        return true;
    }
}
```

---

## 🔧 Lock Handle API (v1.1.0+)

The `AcquireLockHandleAsync` method returns an `IDistributedLockHandle` that implements `IAsyncDisposable` for automatic cleanup. This provides several benefits:

### ✅ Automatic Lock Release
```csharp
await using var lockHandle = await distributedLock.AcquireLockHandleAsync("resource-1", "owner-abc");
// Lock is automatically released when the handle goes out of scope
```

### ✅ Exception Safety
```csharp
await using var lockHandle = await distributedLock.AcquireLockHandleAsync("resource-1", "owner-abc");
if (lockHandle == null) return;

throw new Exception("Oops!"); // Lock is still properly released
```

### ✅ Lock Metadata Access
```csharp
await using var lockHandle = await distributedLock.AcquireLockHandleAsync("resource-1", "owner-abc");
if (lockHandle == null) return;

Console.WriteLine($"Lock acquired for {lockHandle.ResourceId} by {lockHandle.OwnerId}");
Console.WriteLine($"Lock expires at: {lockHandle.ExpiresAt}");
Console.WriteLine($"Lock is still valid: {lockHandle.IsAcquired}");
```

---

## 🔄 Retry Configuration (v1.1.0+)

The library includes built-in retry logic with exponential backoff to handle lock contention and DynamoDB throttling. Retry is **disabled by default** to maintain backward compatibility.

### ✅ Enable Retry Logic
```csharp
services.AddDynamoDbDistributedLock(options =>
{
    options.TableName = "my-lock-table";
    options.Retry.Enabled = true;              // Enable retry logic
    options.Retry.MaxAttempts = 5;             // Max retry attempts (default: 3)
    options.Retry.BaseDelay = TimeSpan.FromMilliseconds(100);  // Base delay (default: 100ms)
    options.Retry.MaxDelay = TimeSpan.FromSeconds(5);          // Max delay (default: 5s)
    options.Retry.BackoffMultiplier = 2.0;     // Exponential multiplier (default: 2.0)
    options.Retry.UseJitter = true;            // Add jitter to prevent thundering herd (default: true)
    options.Retry.JitterFactor = 0.25;         // Jitter factor as percentage (default: 0.25 = 25%)
});
```

### ✅ Configuration via appsettings.json
```json
{
  "DynamoDbLock": {
    "TableName": "my-lock-table",
    "Retry": {
      "Enabled": true,
      "MaxAttempts": 5,
      "BaseDelay": "00:00:00.100",
      "MaxDelay": "00:00:05",
      "BackoffMultiplier": 2.0,
      "UseJitter": true,
      "JitterFactor": 0.25
    }
  }
}
```

### ✅ When Retry is Triggered
The retry logic automatically handles these scenarios:
- **Lock contention** - When another process holds the lock (`ConditionalCheckFailedException`)
- **DynamoDB throttling** - When requests exceed provisioned capacity (`ProvisionedThroughputExceededException`)
- **Internal errors** - Transient DynamoDB service errors (`InternalServerErrorException`)
- **Rate limiting** - When request rate is exceeded (`RequestLimitExceededException`)

### ✅ Exponential Backoff Example
```
Attempt 1: Immediate
Attempt 2: 100ms + jitter
Attempt 3: 200ms + jitter  
Attempt 4: 400ms + jitter
Attempt 5: 800ms + jitter (capped at MaxDelay)
```

> **Note:** Jitter adds randomness (configurable percentage of delay, default 25%) to prevent multiple clients from retrying simultaneously.

---

## 🏗️ Table Schema

This library supports both dedicated tables and shared, single-table designs. You do not need to create a separate table just for locking — this works seamlessly alongside your existing entities.

By default, the library uses the following attributes:

- Partition key: `pk` (String)
- Sort key: `sk` (String)
- TTL attribute: `expiresAt` (Number, UNIX timestamp in seconds)

However, the partition and sort key attribute names are fully configurable via `DynamoDbLockOptions`. This makes it easy to integrate into your existing table structure.
> ✅ Enable TTL on the expiresAt field in your table settings to allow automatic cleanup of expired locks.

---

## 🧪 Unit Testing

Unit tests are written with:

- ✅ xUnit v3
- ✅ AutoFixture + NSubstitute
- ✅ FluentAssertions (AwesomeAssertions)

The library provides `DynamoDbDistributedLockAutoData` to support streamlined tests with frozen mocks and null-value edge cases.

---

## 🔮 Future Enhancements

- ⏱ Lock renewal support
- 🔁 Auto-release logic for expired locks
- 📈 Metrics and diagnostics support
- 🎯 Health check integration

---

## 📜 License

MIT

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Contributions, feedback, and GitHub issues welcome!

## Contributors ✨

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/ncipollina"><img src="https://avatars.githubusercontent.com/u/1405469?v=4?s=100" width="100px;" alt="Nick Cipollina"/><br /><sub><b>Nick Cipollina</b></sub></a><br /><a href="https://github.com/LayeredCraft/dynamodb-distributed-lock/commits?author=ncipollina" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/TylerReid"><img src="https://avatars.githubusercontent.com/u/2939992?v=4?s=100" width="100px;" alt="Tyler Reid"/><br /><sub><b>Tyler Reid</b></sub></a><br /><a href="https://github.com/LayeredCraft/dynamodb-distributed-lock/commits?author=TylerReid" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!