using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public void DbContext_Typ_IstVorhanden()
    {
        Assert.NotNull(typeof(NexusDbContext));
    }
}
