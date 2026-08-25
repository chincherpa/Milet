using Milet.Application.Admin;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class BelegPdfDocumentTests
{
    static BelegPdfDocumentTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly FirmenstammDto Firma = new()
    {
        Firmenname = "Testfirma GmbH",
        Adresse = new AdresseDto { Name1 = "Testfirma GmbH", Strasse = "Teststr. 1", Plz = "00000", Ort = "Testort" },
        UStIdNr = "DE000000000",
    };

    private static BelegDto Beleg(BelegTyp typ, DateOnly? faelligkeit) => new()
    {
        BelegTyp = typ,
        BelegNummer = typ == BelegTyp.Rechnung && faelligkeit is null ? "" : $"{typ}-0001",
        BelegDatum = DateOnly.FromDateTime(DateTime.Today),
        RechnungsadresseSnapshot = new AdresseDto { Name1 = "Testkunde", Strasse = "Kundenstr. 1", Plz = "11111", Ort = "Kundenstadt" },
        Faelligkeit = faelligkeit,
        SummeNetto = 100m,
        SummeMwSt = 19m,
        SummeBrutto = 119m,
        Positionen = [new BelegPositionDto { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 100m, MwStSatzWert = 19m, GesamtNetto = 100m }],
    };

    [Theory]
    [InlineData(BelegTyp.Angebot)]
    [InlineData(BelegTyp.Auftrag)]
    public void GeneratePdf_AngebotUndAuftrag_LiefertNichtLeeresPdf(BelegTyp typ)
    {
        var titel = typ == BelegTyp.Angebot ? "Angebot" : "Auftragsbestätigung";
        var bytes = new BelegPdfDocument(Beleg(typ, faelligkeit: null), Firma, titel).GeneratePdf();
        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]); // '%' — PDF-Header "%PDF-"
    }

    [Fact]
    public void GeneratePdf_Rechnung_ZeigtFaelligkeitUndLiefertNichtLeeresPdf()
    {
        var beleg = Beleg(BelegTyp.Rechnung, faelligkeit: DateOnly.FromDateTime(DateTime.Today.AddDays(14)));
        var bytes = new BelegPdfDocument(beleg, Firma, "Rechnung").GeneratePdf();
        Assert.NotEmpty(bytes);
    }
}
