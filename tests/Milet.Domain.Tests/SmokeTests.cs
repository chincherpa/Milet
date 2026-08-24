using Nexus.Domain.Common;
using Xunit;

namespace Nexus.Domain.Tests;

public class SmokeTests
{
    [Fact]
    public void AuditableEntity_HatErwarteteFelder()
    {
        var props = typeof(AuditableEntity).GetProperties().Select(p => p.Name).ToArray();

        Assert.Contains("ErstelltAm", props);
        Assert.Contains("GeaendertAm", props);
    }
}
