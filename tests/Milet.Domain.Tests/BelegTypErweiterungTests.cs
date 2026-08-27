using Milet.Domain.Entities.Verkauf;
using Xunit;

namespace Milet.Domain.Tests;

public class BelegTypErweiterungTests
{
    [Theory]
    [InlineData(BelegTyp.Angebot, false)]
    [InlineData(BelegTyp.Auftrag, false)]
    [InlineData(BelegTyp.Rechnung, false)]
    [InlineData(BelegTyp.Lieferschein, false)]
    [InlineData(BelegTyp.Bestellung, true)]
    [InlineData(BelegTyp.Wareneingang, true)]
    [InlineData(BelegTyp.Eingangsrechnung, true)]
    public void IstEinkaufsBeleg_KorrekteKlassifizierung(BelegTyp typ, bool erwartet) =>
        Assert.Equal(erwartet, typ.IstEinkaufsBeleg());
}
