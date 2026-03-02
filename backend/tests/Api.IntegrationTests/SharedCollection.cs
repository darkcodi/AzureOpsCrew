using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

/// <summary>
/// xUnit Collection Fixture — ensures a SINGLE AocWebApplicationFactory
/// is shared across ALL test classes.  This is critical because the factory
/// uses process-wide environment variables for config overrides.
/// </summary>
[CollectionDefinition("Shared")]
public class SharedCollection : ICollectionFixture<AocWebApplicationFactory>
{
}
