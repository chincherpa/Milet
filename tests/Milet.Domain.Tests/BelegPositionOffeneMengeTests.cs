using Milet.Domain.Entities.Verkauf;
using Xunit;

namespace Milet.Domain.Tests;

public class BelegPositionOffeneMengeTests
{
    [Fact]
    public void OffeneMenge_OhneFolgepositionen_IstVolleMenge()
    {
        var position = new BelegPosition { Id = 1, Menge = 10 };
        Assert.Equal(10, BelegPosition.OffeneMenge(position, []));
    }

    [Fact]
    public void OffeneMenge_MitTeilweiserUebernahme_ZiehtAb()
    {
        var position = new BelegPosition { Id = 1, Menge = 10 };
        var folge = new BelegPosition { Id = 2, UrsprungsPositionId = 1, Menge = 4 };
        Assert.Equal(6, BelegPosition.OffeneMenge(position, [folge]));
    }

    [Fact]
    public void OffeneMenge_MitMehrerenFolgepositionen_ZiehtSummeAb()
    {
        var position = new BelegPosition { Id = 1, Menge = 10 };
        var folge1 = new BelegPosition { Id = 2, UrsprungsPositionId = 1, Menge = 4 };
        var folge2 = new BelegPosition { Id = 3, UrsprungsPositionId = 1, Menge = 6 };
        Assert.Equal(0, BelegPosition.OffeneMenge(position, [folge1, folge2]));
    }
}
