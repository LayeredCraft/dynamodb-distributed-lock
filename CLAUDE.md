# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET library that provides distributed locking using Amazon DynamoDB. It supports .NET 8 and 9, uses nullable reference types, and follows modern C# conventions.

### Key Architecture Components

- **DynamoDbDistributedLock** (src/DynamoDb.DistributedLock/DynamoDbDistributedLock.cs): Main implementation of the distributed lock
- **IDistributedLockHandle** (src/DynamoDb.DistributedLock/IDistributedLockHandle.cs): Handle interface with IAsyncDisposable for automatic cleanup
- **DynamoDbLockOptions** (src/DynamoDb.DistributedLock/DynamoDbLockOptions.cs): Configuration options with retry settings
- **Retry System** (src/DynamoDb.DistributedLock/Retry/): Exponential backoff retry policy with jitter
- **Dependency Injection** (src/DynamoDb.DistributedLock/Extensions/ServiceCollectionExtensions.cs): Configuration binding and service registration
- **Metrics** (src/DynamoDb.DistributedLock/Metrics/): System.Diagnostics.Metrics integration for observability
- **Observability Package** (src/DynamoDb.DistributedLock.Observability/): OpenTelemetry integration extensions

## Development Commands

### Build
```bash
dotnet build
```

### Run Tests (uses Microsoft Testing Platform)
```bash
dotnet run --project test/DynamoDb.DistributedLock.Tests
```

### Run Tests for Specific Framework
```bash
dotnet run --project test/DynamoDb.DistributedLock.Tests --framework net8.0
dotnet run --project test/DynamoDb.DistributedLock.Tests --framework net9.0
```

### Run Single Test Class
```bash
dotnet run --project test/DynamoDb.DistributedLock.Tests -- --filter "ClassName=DynamoDbDistributedLockTests"
```

### Pack NuGet Package
```bash
dotnet pack --configuration Release
```

## Testing Framework

- **xUnit v3** with Microsoft Testing Platform runner
- **AutoFixture + NSubstitute** for test data generation and mocking
- **FluentAssertions (AwesomeAssertions)** for assertions
- **Custom TestKit** with specialized attributes and specimen builders:
  - `DynamoDbDistributedLockAutoDataAttribute` for streamlined tests
  - Specimen builders for consistent test object creation

## Configuration Notes

- The library uses **Directory.Build.props** for shared MSBuild properties
- Test projects are automatically excluded from packing via `IsTestProject=true`
- Both main library and observability package target .NET 8.0 and 9.0
- Tests use `UseMicrosoftTestingPlatformRunner=true` for modern test execution
- The solution contains two projects:
  - **DynamoDb.DistributedLock**: Core distributed lock library
  - **DynamoDb.DistributedLock.Observability**: OpenTelemetry integration extensions

## Code Patterns

- Uses nullable reference types throughout
- Implements IAsyncDisposable pattern for lock handles
- Dependency injection follows Microsoft.Extensions patterns
- Configuration binding supports both programmatic and JSON configuration
- Internal visibility is granted to test projects via InternalsVisibleTo

## AWS Integration

- Uses AWSSDK.DynamoDBv2 for DynamoDB operations
- Supports configurable table structure (partition key, sort key attributes)
- Implements TTL-based lock expiration
- Handles DynamoDB-specific exceptions for retry logic