using Compono;
using DynamoDb.DistributedLock;

// DynamoDbLockOptions is only ever reached indirectly - through IOptions<DynamoDbLockOptions>
// requests that OptionsValueProvider resolves at runtime, which the generator's static
// Create<T>()/CreateMany<T>() call-site discovery walk can't see. DynamoDbLockOptions itself also
// lives in a referenced assembly (the production DynamoDb.DistributedLock project), so it's opted in
// here rather than annotated directly - see composition-model.md's [Composable] discovery-gap
// guidance.
[assembly: Composable(typeof(DynamoDbLockOptions))]
