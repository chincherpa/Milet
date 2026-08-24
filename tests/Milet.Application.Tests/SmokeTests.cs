using Nexus.Application;
using Xunit;

namespace Nexus.Application.Tests;

public class SmokeTests
{
    [Fact]
    public void AssemblyMarker_Existiert()
    {
        Assert.NotNull(typeof(AssemblyMarker).Assembly);
    }
}
