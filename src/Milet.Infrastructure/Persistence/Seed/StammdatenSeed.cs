using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Gaertnerei;
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
        // migrierten DB dadurch nie automatisch nachgetragen. Jetzt: je fehlender (Code, Jahr)-Kombination einzeln
        // ergänzen, vorhandene Zeilen werden nie angefasst (kein Reset von NaechsteNummer bei bereits existierenden
        // Kreisen).
        //
        // Der Abgleich läuft über (Code, Jahr) — passend zum Unique-Index in NummernkreisConfiguration. Ein
        // Abgleich nur über den Code würde am 01.01. keinen Kreis für das neue Jahr nachtragen, während
        // NumberRangeService strikt nach dem laufenden Jahr sucht: das System könnte danach keine Belegnummer
        // mehr vergeben. Verlassen muss man sich darauf nicht — NumberRangeService legt einen fehlenden
        // Jahreskreis beim ersten Zugriff selbst an; dieser Seed ist der Weg, es beim Migratorlauf sauber
        // vorzubereiten.
        //
        // Jahr aus DateTime.Today (lokal), nicht UtcNow: gleiche Quelle wie NumberRangeService und das Belegdatum.
        var aktuellesJahr = DateTime.Today.Year;
        var benoetigteNummernkreise = new[]
        {
            new Nummernkreis { Code = "KD", NaechsteNummer = 10001, Format = "KD-{0}" },
            new Nummernkreis { Code = "LF", NaechsteNummer = 70001, Format = "LF-{0}" },
            new Nummernkreis { Code = "ART", NaechsteNummer = 1001, Format = "ART-{0:00000}" },
            new Nummernkreis { Code = "AN", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "AN-{1}-{0:0000}" },
            new Nummernkreis { Code = "AU", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "AU-{1}-{0:0000}" },
            new Nummernkreis { Code = "LS", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "LS-{1}-{0:0000}" },
            new Nummernkreis { Code = "RE", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" },
            new Nummernkreis { Code = "GS", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "GS-{1}-{0:0000}" },
            new Nummernkreis { Code = "BE", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "BE-{1}-{0:0000}" },
            new Nummernkreis { Code = "WE", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "WE-{1}-{0:0000}" },
            new Nummernkreis { Code = "ER", Jahr = aktuellesJahr, NaechsteNummer = 1, Format = "ER-{1}-{0:0000}" },
        };
        var vorhandeneKreise = await db.Nummernkreise.Select(n => new { n.Code, n.Jahr }).ToListAsync(ct);
        foreach (var kreis in benoetigteNummernkreise)
        {
            if (!vorhandeneKreise.Any(v => v.Code == kreis.Code && v.Jahr == kreis.Jahr))
            {
                db.Nummernkreise.Add(kreis);
            }
        }

        if (!await db.Lagerorte.AnyAsync(ct))
        {
            db.Lagerorte.Add(new Milet.Domain.Entities.Lager.Lagerort { Code = "HL", Bezeichnung = "Hauptlager", Aktiv = true });
        }

        // Gleiches "je fehlender Stufe ergänzen"-Muster wie bei den Nummernkreisen (s. o.).
        var benoetigteMahnstufen = new[]
        {
            new Mahnstufe { Stufe = 1, Karenztage = 7, Gebuehr = 0.00m },
            new Mahnstufe { Stufe = 2, Karenztage = 14, Gebuehr = 5.00m },
            new Mahnstufe { Stufe = 3, Karenztage = 21, Gebuehr = 10.00m },
        };
        var vorhandeneStufen = await db.Mahnstufen.Select(m => m.Stufe).ToListAsync(ct);
        foreach (var stufe in benoetigteMahnstufen)
        {
            if (!vorhandeneStufen.Contains(stufe.Stufe))
            {
                db.Mahnstufen.Add(stufe);
            }
        }

        // Gleiches "je fehlendem Code ergänzen"-Muster wie bei den Nummernkreisen (s. o.) — Namen/Farben
        // sind Startpunkte, jederzeit über den Kulturstufen-Tab in den Einstellungen änderbar (E5).
        var benoetigteKulturstufen = new[]
        {
            new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, IstVerkaufsfaehig = false, FarbeHex = "#8BC34A" },
            new Kulturstufe { Code = "TP", Bezeichnung = "Teenagerpflanze", Reihenfolge = 2, IstVerkaufsfaehig = false, FarbeHex = "#4CAF50" },
            new Kulturstufe { Code = "VP", Bezeichnung = "Verkaufspflanze", Reihenfolge = 3, IstVerkaufsfaehig = true, FarbeHex = "#2E7D32" },
        };
        var vorhandeneKulturstufen = await db.Kulturstufen.Select(k => k.Code).ToListAsync(ct);
        foreach (var stufe in benoetigteKulturstufen)
        {
            if (!vorhandeneKulturstufen.Contains(stufe.Code))
            {
                db.Kulturstufen.Add(stufe);
            }
        }

        // v1 zeigt genau einen Plan (E11) — als Tabelle vorbereitet, damit "mehrere Standorte" später ohne
        // Schemabruch nachrüstbar ist. Das Hauptlager (HL) bleibt IstFeld=false und bekommt keine Geometrie.
        if (!await db.Gaertnereiplaene.AnyAsync(ct))
        {
            db.Gaertnereiplaene.Add(new Gaertnereiplan { Bezeichnung = "Gärtnerei", BreiteMeter = 100m, HoeheMeter = 60m });
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

        if (!await db.FibuKonfiguration.AnyAsync(ct))
        {
            db.FibuKonfiguration.Add(new FibuKonfiguration
            {
                Id = 1,
                Kontenrahmen = Kontenrahmen.Skr03,
                BeraterNr = 1001,
                MandantNr = 1,
                WirtschaftsjahrBeginnMonat = 1,
                SachkontenLaenge = 4,
                BankkontoNr = 1200,
            });
        }

        // Zwischenspeichern nötig, bevor die MwSt-Sätze unten per Query nachgeladen werden: auf einer frisch
        // migrierten (leeren) DB wurden sie gerade erst oben in diesem Aufruf per AddRange hinzugefügt, aber
        // noch nicht gespeichert — eine LINQ-Query gegen den DbSet sieht ungespeicherte Added-Entities nicht
        // (anders als db.MwStSaetze.Local), sonst bliebe der Kontenkontrolle-Backfill unten wirkungslos.
        await db.SaveChangesAsync(ct);

        // SKR03-Standardkonten je Steuerschlüssel für den DATEV-Export — nur wo noch NULL gesetzt
        // (Update-in-place, nie bereits vom Nutzer gepflegte Werte überschreiben; editierbar über den
        // FibuKonten-Tab/MwSt-Tab in KleinstammPage). Grobe Orientierungswerte, kein Ersatz für die
        // Abstimmung mit dem tatsächlichen Kontenrahmen/Steuerberater des Nutzers.
        var skr03KontenJeSteuerschluessel = new Dictionary<int, (int Erloes, int Aufwand)>
        {
            [3] = (8400, 3400), // 19 % USt
            [2] = (8300, 3300), // 7 % USt
            [0] = (8120, 3200), // steuerfrei
        };
        var mwStOhneKonten = await db.MwStSaetze
            .Where(m => m.SteuerSchluessel != null && (m.ErloeskontoNr == null || m.AufwandskontoNr == null))
            .ToListAsync(ct);
        foreach (var mwSt in mwStOhneKonten)
        {
            if (!skr03KontenJeSteuerschluessel.TryGetValue(mwSt.SteuerSchluessel!.Value, out var konten)) continue;
            mwSt.ErloeskontoNr ??= konten.Erloes;
            mwSt.AufwandskontoNr ??= konten.Aufwand;
        }

        await db.SaveChangesAsync(ct);
    }
}
