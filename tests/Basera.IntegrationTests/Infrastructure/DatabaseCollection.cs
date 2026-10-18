namespace Basera.IntegrationTests.Infrastructure;

/// <summary>
/// Test classes marked <c>[Collection(DatabaseCollection.Name)]</c> share one <see cref="TestDatabase"/>,
/// created before the first of them runs and dropped after the last. They run one class at a time.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<TestDatabase>
{
    public const string Name = "Database";
}
