using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class DatevExtfWriterTests
{
    private static DatevExportKopf Kopf() => new()
    {
        BeraterNr = 1001,
        MandantNr = 42,
        WirtschaftsjahrBeginn = new DateOnly(2026, 1, 1),
        SachkontenLaenge = 4,
        DatumVon = new DateOnly(2026, 8, 1),
        DatumBis = new DateOnly(2026, 8, 31),
        Bezeichnung = "Milet Buchungsstapel 08/2026",
        ErzeugtAm = new DateTime(2026, 8, 27, 10, 15, 30, 123, DateTimeKind.Utc),
    };

    [Fact]
    public void Schreiben_LeereListe_NurKopfUndSpaltenzeile()
    {
        var ergebnis = DatevExtfWriter.Schreiben(Kopf(), []);

        const string erwartet =
            "\"EXTF\";700;21;\"Buchungsstapel\";13;20260827101530123;;\"Milet\";;;1001;42;20260101;4;20260801;20260831;\"Milet Buchungsstapel 08/2026\";;1;0;0;\"EUR\"\r\n" +
            "\"Umsatz (ohne Soll/Haben-Kz)\";\"Soll/Haben-Kennzeichen\";\"WKZ Umsatz\";\"Konto\";\"Gegenkonto (ohne BU-Schlüssel)\";\"BU-Schlüssel\";\"Belegdatum\";\"Belegfeld 1\";\"Buchungstext\"\r\n";

        Assert.Equal(erwartet, ergebnis);
    }

    [Fact]
    public void Schreiben_RechnungUndZahlung_GoldenFile()
    {
        var zeilen = new List<DatevBuchungszeile>
        {
            new()
            {
                Umsatz = 1190.00m,
                SollHaben = 'S',
                Konto = 10001,
                Gegenkonto = 8400,
                BuSchluessel = 3,
                Belegdatum = new DateOnly(2026, 8, 5),
                Belegfeld1 = "RE-2026-0001",
                Buchungstext = "Rechnung RE-2026-0001",
            },
            new()
            {
                Umsatz = 1190.00m,
                SollHaben = 'H',
                Konto = 1200,
                Gegenkonto = 10001,
                Belegdatum = new DateOnly(2026, 8, 12),
                Belegfeld1 = "RE-2026-0001",
                Buchungstext = "Zahlungseingang RE-2026-0001",
            },
        };

        var ergebnis = DatevExtfWriter.Schreiben(Kopf(), zeilen);
        var zeilenGetrennt = ergebnis.Split("\r\n");

        Assert.Equal(5, zeilenGetrennt.Length); // Kopf, Spaltenzeile, 2 Datenzeilen, trailing empty
        Assert.Equal("", zeilenGetrennt[^1]);
        Assert.Equal(
            "1190,00;\"S\";\"EUR\";10001;8400;3;05082026;\"RE-2026-0001\";\"Rechnung RE-2026-0001\"",
            zeilenGetrennt[2]);
        Assert.Equal(
            "1190,00;\"H\";\"EUR\";1200;10001;;12082026;\"RE-2026-0001\";\"Zahlungseingang RE-2026-0001\"",
            zeilenGetrennt[3]);
    }

    [Fact]
    public void Schreiben_NegativerUmsatz_WirdAlsBetragOhneVorzeichenGeschrieben()
    {
        var zeilen = new List<DatevBuchungszeile>
        {
            new()
            {
                Umsatz = -50.00m,
                SollHaben = 'H',
                Konto = 10001,
                Gegenkonto = 8400,
                Belegdatum = new DateOnly(2026, 8, 20),
                Belegfeld1 = "GS-2026-0001",
                Buchungstext = "Storno",
            },
        };

        var ergebnis = DatevExtfWriter.Schreiben(Kopf(), zeilen);

        Assert.Contains("50,00;\"H\"", ergebnis);
        Assert.DoesNotContain("-50,00", ergebnis);
    }

    [Fact]
    public void Schreiben_BuchungstextMitAnfuehrungszeichenUndZuLang_WirdEscaptUndGekuerzt()
    {
        var langerText = new string('x', 70) + "\"Ende\"";
        var zeilen = new List<DatevBuchungszeile>
        {
            new()
            {
                Umsatz = 10.00m,
                SollHaben = 'S',
                Konto = 10001,
                Gegenkonto = 8400,
                Belegdatum = new DateOnly(2026, 8, 1),
                Belegfeld1 = "RE-1",
                Buchungstext = langerText,
            },
        };

        var ergebnis = DatevExtfWriter.Schreiben(Kopf(), zeilen);
        var datenzeile = ergebnis.Split("\r\n")[2];

        // Buchungstext auf 60 Zeichen gekürzt (vor dem Escaping), danach Anführungszeichen verdoppelt.
        Assert.Contains(new string('x', 60), datenzeile);
        Assert.DoesNotContain(langerText, datenzeile);
    }
}
