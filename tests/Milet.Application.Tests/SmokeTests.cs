using Milet.Application;
using Xunit;

namespace Milet.Application.Tests;

public class SmokeTests
{
    [Fact]
    public void AssemblyMarker_Existiert()
    {
        Assert.NotNull(typeof(AssemblyMarker).Assembly);
    }
}
