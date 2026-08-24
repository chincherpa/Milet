using Milet.Infrastructure.Persistence;
using Xunit;

namespace Milet.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public void DbContext_Typ_IstVorhanden()
    {
        Assert.NotNull(typeof(MiletDbContext));
    }
}
