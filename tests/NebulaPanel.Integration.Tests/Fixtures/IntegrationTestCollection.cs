namespace NebulaPanel.Integration.Tests.Fixtures;

/// <summary>
/// Collection definition for integration tests that share a WebApplicationFactory.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<NebulaPanelWebApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
