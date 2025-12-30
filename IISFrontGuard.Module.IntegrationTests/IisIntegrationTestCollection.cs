using Xunit;

namespace IISFrontGuard.Module.IntegrationTests
{
    /// <summary>
    /// Collection definition for integration tests that share the same full IIS site and database.
    /// This ensures the IisIntegrationFixture is created only once for all tests in this collection.
    /// Tests in this collection will run sequentially to avoid interference.
    /// </summary>
    [CollectionDefinition("IIS Integration Tests")]
    public class IisIntegrationTestCollection : ICollectionFixture<IisIntegrationFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
