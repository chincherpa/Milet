using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.ValueObjects;

namespace Milet.Infrastructure.Persistence.Seed;

/// <summary>
/// Grunddaten, ohne die das System nicht sinnvoll bedienbar ist: Einheiten, MwSt-Sätze,
/// eine Standard-Zahlungsbedingung und die Nummernkreise für Stammdaten.
/// Idempotent — läuft bei jedem Migrator-Aufruf, legt nur Fehlendes an.
/// </summary>
public static class StammdatenSeed
{
    public static async Task ApplyAsync(MiletDbContext db, CancellationToken ct = default)
    {
        if (!await db.Einheiten.AnyAsync(ct))
        {
            db.Einheiten.AddRange(
                new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück", NachkommaStellen = 0 },
                new Einheit { Kuerzel = "kg", Bezeichnung = "Kilogramm", NachkommaStellen = 3 },
                new Einheit { Kuerzel = "h", Bezeichnung = "Stunde", NachkommaStellen = 2 },
                new Einheit { Kuerzel = "m", Bezeichnung = "Meter", NachkommaStellen = 2 },
                new Einheit { Kuerzel = "Pak", Bezeichnung = "Paket", NachkommaStellen = 0 });
        }

        if (!await db.MwStSaetze.AnyAsync(ct))
        {
            var gueltigAb = new DateOnly(2007, 1, 1);
            db.MwStSaetze.AddRange(
                new MwStSatz { Bezeichnung = "Voller Satz", Satz = 19.00m, SteuerSchluessel = 3, GueltigAb = gueltigAb },
                new MwStSatz { Bezeichnung = "Ermäßigter Satz", Satz = 7.00m, SteuerSchluessel = 2, GueltigAb = gueltigAb },
                new MwStSatz { Bezeichnung = "Steuerfrei", Satz = 0.00m, SteuerSchluessel = 0, GueltigAb = gueltigAb });
        }

        if (!await db.Zahlungsbedingungen.AnyAsync(ct))
        {
            db.Zahlungsbedingungen.AddRange(
                new Zahlungsbedingung { Bezeichnung = "Sofort netto", ZielTage = 0 },
                new Zahlungsbedingung { Bezeichnung = "14 Tage netto", ZielTage = 14 },
                new Zahlungsbedingung { Bezeichnung = "30 Tage netto, 14 Tage 2% Skonto", ZielTage = 30, SkontoTage = 14, SkontoProzent = 2.00m });
        }

        // Fix für ein bekanntes Risiko (STATUS.md „Bekannte Risiken"): vorher wurden Nummernkreise nur angelegt,
        // wenn die ganze Tabelle leer war — ein später hinzugefügter Code (hier: WE, ER) wurde auf einer bereits
        // migrierten DB dadurch nie automatisch nachgetragen. Jetzt: je fehlendem Code einzeln ergänzen,
        // vorhandene Zeilen werden nie angefasst (kein Reset von NaechsteNummer bei bereits existierenden Codes).
        var benoetigteNummernkreise = new[]
        {
            new Nummernkreis { Code = "KD", NaechsteNummer = 10001, Format = "KD-{0}" },
            new Nummernkreis { Code = "LF", NaechsteNummer = 70001, Format = "LF-{0}" },
            new Nummernkreis { Code = "ART", NaechsteNummer = 1001, Format = "ART-{0:00000}" },
            new Nummernkreis { Code = "AN", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AN-{1}-{0:0000}" },
            new Nummernkreis { Code = "AU", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AU-{1}-{0:0000}" },
            new Nummernkreis { Code = "LS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "LS-{1}-{0:0000}" },
            new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" },
            new Nummernkreis { Code = "GS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "GS-{1}-{0:0000}" },
            new Nummernkreis { Code = "BE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "BE-{1}-{0:0000}" },
            new Nummernkreis { Code = "WE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "WE-{1}-{0:0000}" },
            new Nummernkreis { Code = "ER", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "ER-{1}-{0:0000}" },
        };
        var vorhandeneCodes = await db.Nummernkreise.Select(n => n.Code).ToListAsync(ct);
        foreach (var kreis in benoetigteNummernkreise)
        {
            if (!vorhandeneCodes.Contains(kreis.Code))
            {
                db.Nummernkreise.Add(kreis);
            }
        }

        if (!await db.Lagerorte.AnyAsync(ct))
        {
            db.Lagerorte.Add(new Milet.Domain.Entities.Lager.Lagerort { Code = "HL", Bezeichnung = "Hauptlager", Aktiv = true });
        }

        if (!await db.Firmenstamm.AnyAsync(ct))
        {
            db.Firmenstamm.Add(new Firmenstamm
            {
                Id = 1,
                Firmenname = "Milet Handels GmbH",
                Adresse = new Adresse { Name1 = "Milet Handels GmbH", Strasse = "Musterstraße 1", Plz = "12345", Ort = "Musterstadt", Land = "DE" },
                UStIdNr = "DE123456789",
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
