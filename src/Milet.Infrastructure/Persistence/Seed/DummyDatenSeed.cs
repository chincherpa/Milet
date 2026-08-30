using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Services;

namespace Milet.Infrastructure.Persistence.Seed;

/// <summary>
/// Testdaten für Entwicklung/Demo: Kunden, Lieferanten, Artikel, Preisliste + Staffelpreise
/// sowie ein paar Angebote/Aufträge/Rechnungen in unterschiedlichen Status. Läuft über die
/// echten Application-Services (nicht direkt gegen den DbContext), damit Nummernkreise,
/// Preisfindung, Steuerberechnung und Buchungspipeline exakt wie im UI durchlaufen werden.
/// Idempotent — überspringt sich selbst, sobald bereits Kunden vorhanden sind.
/// </summary>
public static class DummyDatenSeed
{
    public static async Task<bool> ApplyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<MiletDbContext>>();
        await using (var probe = await dbFactory.CreateDbContextAsync(ct))
        {
            // Gate auf Artikel statt Kunden: Kunden können schon aus manuellen Abnahmetests existieren,
            // ohne dass die hier angelegten Testdaten (Artikel, Belege) schon vorhanden wären.
            if (await probe.Artikel.AnyAsync(ct))
                return false;
        }

        var einheiten = new Dictionary<string, int>();
        int mwst19Id, mwst7Id;
        decimal mwst19Wert, mwst7Wert;
        int? mwst19Schluessel, mwst7Schluessel;
        int zbSofortId, zb14Id, zb30Id;

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            foreach (var e in await db.Einheiten.AsNoTracking().ToListAsync(ct))
                einheiten[e.Kuerzel] = e.Id;

            var mwst19 = await db.MwStSaetze.AsNoTracking().FirstAsync(m => m.Satz == 19.00m, ct);
            var mwst7 = await db.MwStSaetze.AsNoTracking().FirstAsync(m => m.Satz == 7.00m, ct);
            (mwst19Id, mwst19Wert, mwst19Schluessel) = (mwst19.Id, mwst19.Satz, mwst19.SteuerSchluessel);
            (mwst7Id, mwst7Wert, mwst7Schluessel) = (mwst7.Id, mwst7.Satz, mwst7.SteuerSchluessel);

            zbSofortId = (await db.Zahlungsbedingungen.AsNoTracking().FirstAsync(z => z.ZielTage == 0, ct)).Id;
            zb14Id = (await db.Zahlungsbedingungen.AsNoTracking().FirstAsync(z => z.ZielTage == 14, ct)).Id;
            zb30Id = (await db.Zahlungsbedingungen.AsNoTracking().FirstAsync(z => z.ZielTage == 30, ct)).Id;
        }

        var artikelService = services.GetRequiredService<IArtikelService>();
        var preislistenService = services.GetRequiredService<IPreislistenService>();
        var artikelPreiseService = services.GetRequiredService<IArtikelPreiseService>();
        var kundenService = services.GetRequiredService<IKundenService>();
        var lieferantenService = services.GetRequiredService<ILieferantenService>();
        var verkaufLookup = services.GetRequiredService<IVerkaufLookupService>();
        var belegService = services.GetRequiredService<IBelegService>();
        var ueberleitungService = services.GetRequiredService<IBelegUeberleitungService>();
        var buchenService = services.GetRequiredService<IRechnungBuchenService>();

        // --- Artikel ---------------------------------------------------------------
        var artikelDefs = new (string Bez, string Einheit, int MwStId, decimal Ek, decimal Vk, decimal? Mindestbestand, bool Lagerartikel)[]
        {
            ("Kopierpapier A4 80g (500 Blatt)", "Pak", mwst19Id, 2.80m, 4.50m, 20, true),
            ("Ordner A4 breit", "Stk", mwst19Id, 1.60m, 3.20m, 30, true),
            ("Kugelschreiber blau (10er-Pack)", "Pak", mwst19Id, 3.10m, 5.90m, 15, true),
            ("USB-Stick 32GB", "Stk", mwst19Id, 4.50m, 8.90m, 25, true),
            ("Netzwerkkabel Cat6 5m", "Stk", mwst19Id, 3.20m, 6.50m, 20, true),
            ("Kabelkanal (Meterware)", "m", mwst19Id, 1.10m, 2.20m, 50, true),
            ("LED-Schreibtischlampe", "Stk", mwst19Id, 19.00m, 34.90m, 10, true),
            ("Aktenvernichter P4", "Stk", mwst19Id, 120.00m, 189.00m, 2, true),
            ("Versandkarton 40x30x20 (20er-Pack)", "Pak", mwst19Id, 7.50m, 12.90m, 15, true),
            ("Luftpolsterfolie 50m Rolle", "Stk", mwst19Id, 9.20m, 15.90m, 10, true),
            ("Bürostuhl ergonomisch", "Stk", mwst19Id, 140.00m, 249.00m, 1, true),
            ("Schrauben-Sortiment lose", "kg", mwst19Id, 5.50m, 9.90m, 5, true),
            ("Fachbuch Buchführung Grundlagen", "Stk", mwst7Id, 14.00m, 24.90m, null, true),
            ("Montage/Einrichtung vor Ort", "h", mwst19Id, 0m, 65.00m, null, false),
        };

        var artikelIds = new List<int>();
        foreach (var a in artikelDefs)
        {
            var dto = new ArtikelDto
            {
                Bezeichnung = a.Bez,
                EinheitId = einheiten[a.Einheit],
                MwStSatzId = a.MwStId,
                Einkaufspreis = a.Ek,
                Listenpreis = a.Vk,
                Mindestbestand = a.Mindestbestand,
                IstLagerartikel = a.Lagerartikel,
            };
            var gespeichert = await artikelService.SpeichereAsync(dto, ct);
            artikelIds.Add(gespeichert.Id);
        }

        // Kurzreferenzen für die Belegpositionen weiter unten.
        var papier = artikelIds[0];
        var ordner = artikelIds[1];
        var kulis = artikelIds[2];
        var usbStick = artikelIds[3];
        var netzwerkkabel = artikelIds[4];
        var kabelkanal = artikelIds[5];
        var ledLampe = artikelIds[6];
        var aktenvernichter = artikelIds[7];
        var versandkarton = artikelIds[8];
        var buerostuhl = artikelIds[10];
        var fachbuch = artikelIds[12];
        var montage = artikelIds[13];

        // --- Preisliste + Staffelpreise ---------------------------------------------
        var preisliste = await preislistenService.SpeichereAsync(new PreislisteDto { Name = "Vertriebspartner Staffelpreise" }, ct);
        await artikelPreiseService.SpeichereAsync(new ArtikelPreisDto { PreislisteId = preisliste.Id, ArtikelId = usbStick, AbMenge = 20, Preis = 7.90m }, ct);
        await artikelPreiseService.SpeichereAsync(new ArtikelPreisDto { PreislisteId = preisliste.Id, ArtikelId = papier, AbMenge = 10, Preis = 4.00m }, ct);
        await artikelPreiseService.SpeichereAsync(new ArtikelPreisDto { PreislisteId = preisliste.Id, ArtikelId = papier, AbMenge = 50, Preis = 3.60m }, ct);

        // --- Kunden --------------------------------------------------------------
        var kundenDefs = new (string Name, string Strasse, string Plz, string Ort, string Ansprechpartner, string Email, int ZbId, decimal Rabatt, int? PreislisteId)[]
        {
            ("Bäckerei Sonnenschein GmbH", "Bahnhofstr. 12", "10115", "Berlin", "Petra Sonnenschein", "einkauf@sonnenschein-berlin.de", zb14Id, 0m, null),
            ("Autohaus Krüger KG", "Industriering 5", "40213", "Düsseldorf", "Thomas Krüger", "buchhaltung@autohaus-krueger.de", zb30Id, 5m, preisliste.Id),
            ("Café Mocca Einzelunternehmen", "Marktplatz 3", "80331", "München", "Lena Brandt", "info@cafe-mocca.de", zbSofortId, 0m, null),
            ("Handwerksbetrieb Fischer & Söhne", "Werkstattweg 8", "50667", "Köln", "Markus Fischer", "buero@fischer-handwerk.de", zb14Id, 3m, null),
            ("IT-Systemhaus Nordlicht GmbH", "Hafenstr. 22", "20457", "Hamburg", "Sven Petersen", "einkauf@nordlicht-it.de", zb30Id, 8m, preisliste.Id),
            ("Praxis Dr. Wagner", "Kurfürstendamm 45", "10707", "Berlin", "Dr. Anke Wagner", "praxis@dr-wagner-berlin.de", zbSofortId, 0m, null),
        };

        var kundenIds = new List<int>();
        foreach (var k in kundenDefs)
        {
            var dto = new KundeDto
            {
                Adresse = new AdresseDto { Name1 = k.Name, Strasse = k.Strasse, Plz = k.Plz, Ort = k.Ort, Land = "DE" },
                Ansprechpartner = k.Ansprechpartner,
                Email = k.Email,
                ZahlungsbedingungId = k.ZbId,
                RabattProzent = k.Rabatt,
                PreislisteId = k.PreislisteId,
            };
            var gespeichert = await kundenService.SpeichereAsync(dto, ct);
            kundenIds.Add(gespeichert.Id);
        }

        // --- Lieferanten -----------------------------------------------------------
        var lieferantenDefs = new (string Name, string Strasse, string Plz, string Ort, string Ansprechpartner, string Email)[]
        {
            ("Papier Großhandel Meyer OHG", "Gewerbepark 1", "33602", "Bielefeld", "Frank Meyer", "vertrieb@papier-meyer.de"),
            ("Elektro Komponenten Schmidt GmbH", "Industriestr. 9", "70565", "Stuttgart", "Julia Schmidt", "verkauf@ek-schmidt.de"),
            ("Verpackung Plus GmbH", "Logistikweg 4", "04109", "Leipzig", "Robert Klein", "info@verpackung-plus.de"),
            ("Bürotechnik Weber & Partner", "Ringstr. 17", "90402", "Nürnberg", "Sabine Weber", "vertrieb@buerotechnik-weber.de"),
        };

        foreach (var l in lieferantenDefs)
        {
            await lieferantenService.SpeichereAsync(new LieferantDto
            {
                Adresse = new AdresseDto { Name1 = l.Name, Strasse = l.Strasse, Plz = l.Plz, Ort = l.Ort, Land = "DE" },
                Ansprechpartner = l.Ansprechpartner,
                Email = l.Email,
            }, ct);
        }

        // --- Belege: Angebote, teils übergeleitet zu Auftrag/Rechnung ----------------
        var lookups = await verkaufLookup.LadeLookupsAsync(ct);
        var artikelLookup = lookups.Artikel.ToDictionary(a => a.Id);
        var heute = DateOnly.FromDateTime(DateTime.Today);

        async Task<BelegPositionDto> PositionAsync(int nr, int artikelId, decimal menge, int kundeId)
        {
            var a = artikelLookup[artikelId];
            var preis = await verkaufLookup.ErmittlePreisAsync(artikelId, menge, kundeId, ct);
            return new BelegPositionDto
            {
                PositionsNr = nr,
                PositionsTyp = PositionsTyp.Artikel,
                ArtikelId = artikelId,
                Bezeichnung = a.Bezeichnung,
                EinheitKuerzel = a.EinheitKuerzel,
                Menge = menge,
                Einzelpreis = preis.Einzelpreis,
                RabattProzent = preis.RabattProzent,
                MwStSatzId = a.MwStSatzId,
                MwStSatzWert = a.MwStSatzWert,
                SteuerSchluessel = a.SteuerSchluessel,
            };
        }

        BelegPositionDto Freitext(int nr, string bezeichnung, decimal einzelpreis) => new()
        {
            PositionsNr = nr,
            PositionsTyp = PositionsTyp.Freitext,
            Bezeichnung = bezeichnung,
            Menge = 1,
            Einzelpreis = einzelpreis,
            MwStSatzId = mwst19Id,
            MwStSatzWert = mwst19Wert,
            SteuerSchluessel = mwst19Schluessel,
        };

        async Task<BelegDto> AngebotAnlegenAsync(int kundeId, DateOnly datum, List<BelegPositionDto> positionen) =>
            await belegService.SpeichereAsync(new BelegDto
            {
                BelegTyp = BelegTyp.Angebot,
                KundeId = kundeId,
                BelegDatum = datum,
                Positionen = positionen,
            }, ct);

        // 1) Bäckerei Sonnenschein: Angebot -> Auftrag -> Rechnung (gebucht)
        var kunde1 = kundenIds[0];
        var angebot1 = await AngebotAnlegenAsync(kunde1, heute.AddDays(-24), [
            await PositionAsync(1, papier, 20, kunde1),
            await PositionAsync(2, ordner, 10, kunde1),
            await PositionAsync(3, kulis, 5, kunde1),
        ]);
        var auftrag1 = await ueberleitungService.UeberleitenAsync(angebot1.Id, BelegTyp.Auftrag, ct);
        var rechnung1 = await ueberleitungService.UeberleitenAsync(auftrag1.Id, BelegTyp.Rechnung, ct);
        await buchenService.BuchenAsync(rechnung1.Id, ct);

        // 2) Autohaus Krüger: Angebot bleibt offen (Staffelpreis USB-Sticks greift)
        var kunde2 = kundenIds[1];
        await AngebotAnlegenAsync(kunde2, heute.AddDays(-6), [
            await PositionAsync(1, usbStick, 30, kunde2),
            await PositionAsync(2, netzwerkkabel, 10, kunde2),
        ]);

        // 3) Café Mocca: Angebot -> Auftrag (noch nicht fakturiert)
        var kunde3 = kundenIds[2];
        var angebot3 = await AngebotAnlegenAsync(kunde3, heute.AddDays(-10), [
            await PositionAsync(1, ledLampe, 2, kunde3),
            await PositionAsync(2, versandkarton, 3, kunde3),
        ]);
        await ueberleitungService.UeberleitenAsync(angebot3.Id, BelegTyp.Auftrag, ct);

        // 4) Fischer & Söhne: Angebot -> Auftrag -> Rechnung (gebucht), inkl. Dienstleistung
        var kunde4 = kundenIds[3];
        var angebot4 = await AngebotAnlegenAsync(kunde4, heute.AddDays(-18), [
            await PositionAsync(1, aktenvernichter, 1, kunde4),
            await PositionAsync(2, buerostuhl, 2, kunde4),
            await PositionAsync(3, montage, 4, kunde4),
        ]);
        var auftrag4 = await ueberleitungService.UeberleitenAsync(angebot4.Id, BelegTyp.Auftrag, ct);
        var rechnung4 = await ueberleitungService.UeberleitenAsync(auftrag4.Id, BelegTyp.Rechnung, ct);
        await buchenService.BuchenAsync(rechnung4.Id, ct);

        // 5) IT-Systemhaus Nordlicht: Angebot -> Auftrag -> Rechnung (gebucht), Staffelpreise
        var kunde5 = kundenIds[4];
        var angebot5 = await AngebotAnlegenAsync(kunde5, heute.AddDays(-14), [
            await PositionAsync(1, usbStick, 25, kunde5),
            await PositionAsync(2, netzwerkkabel, 15, kunde5),
            await PositionAsync(3, kabelkanal, 50, kunde5),
        ]);
        var auftrag5 = await ueberleitungService.UeberleitenAsync(angebot5.Id, BelegTyp.Auftrag, ct);
        var rechnung5 = await ueberleitungService.UeberleitenAsync(auftrag5.Id, BelegTyp.Rechnung, ct);
        await buchenService.BuchenAsync(rechnung5.Id, ct);

        // 6) Praxis Dr. Wagner: Angebot mit reduziertem Steuersatz + Freitextposition
        var kunde6 = kundenIds[5];
        await AngebotAnlegenAsync(kunde6, heute.AddDays(-2), [
            await PositionAsync(1, fachbuch, 3, kunde6),
            await PositionAsync(2, ordner, 5, kunde6),
            Freitext(3, "Lieferung & Versand", 9.90m),
        ]);

        // --- Gärtnerei/Kulturführung (Phase 8) --------------------------------------
        // Bewusst über den echten Schreibpfad BestandService.BucheBewegungAsync gebucht (nicht per
        // direktem Insert) — der Seed durchläuft damit dieselben Regeln (KulturRegeln.PruefeDimensionen,
        // Upsert-Race-Fix) wie jede spätere Buchung aus der UI, und Ledger/Snapshot bleiben konsistent.
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var plan = await db.Gaertnereiplaene.FirstAsync(ct);

            var felderDefs = new (string Code, string Bezeichnung, decimal X, decimal Y, decimal Breite, decimal Hoehe, string[] SektionsCodes)[]
            {
                ("F1", "Feld Nord", 5m, 5m, 30m, 20m, ["A1", "A2", "A3", "A4", "A5", "A6"]),
                ("F2", "Feld Süd", 5m, 30m, 30m, 20m, ["B1", "B2", "B3", "B4", "B5"]),
                ("F3", "Folientunnel", 45m, 5m, 20m, 10m, ["C1", "C2", "C3", "C4"]),
            };

            // Relative Rasterpositionen (Meter) für bis zu 6 Sektionen à 5x5m je Feld — bewusst großzügig
            // beabstandet, damit sie in jedem der drei unterschiedlich großen Felder aus felderDefs passen.
            var rasterPositionen = new (decimal X, decimal Y)[] { (0, 0), (6, 0), (12, 0), (0, 6), (6, 6), (12, 6) };

            var sektionenJeCode = new Dictionary<string, int>();
            var feldIdJeSektionsCode = new Dictionary<string, int>();
            foreach (var f in felderDefs)
            {
                var feld = new Lagerort
                {
                    Code = f.Code,
                    Bezeichnung = f.Bezeichnung,
                    IstFeld = true,
                    GaertnereiplanId = plan.Id,
                    PosXMeter = f.X,
                    PosYMeter = f.Y,
                    BreiteMeter = f.Breite,
                    HoeheMeter = f.Hoehe,
                };
                db.Lagerorte.Add(feld);
                await db.SaveChangesAsync(ct);

                for (var i = 0; i < f.SektionsCodes.Length; i++)
                {
                    var pos = rasterPositionen[i];
                    var sektion = new Milet.Domain.Entities.Gaertnerei.Sektion
                    {
                        LagerortId = feld.Id,
                        Code = f.SektionsCodes[i],
                        Bezeichnung = $"Sektion {f.SektionsCodes[i]}",
                        PosXMeter = pos.X,
                        PosYMeter = pos.Y,
                        BreiteMeter = 5m,
                        HoeheMeter = 5m,
                    };
                    db.Sektionen.Add(sektion);
                    await db.SaveChangesAsync(ct);
                    sektionenJeCode[f.SektionsCodes[i]] = sektion.Id;
                    // Jede Sektion gehört genau zu ihrem Feld — die Bestandsbuchung unten muss die LagerortId
                    // aus DIESER Zuordnung nehmen, nicht aus einer festen Feldannahme, sonst entstünde eine
                    // ArtikelBestand-Zeile mit LagerortId≠dem Feld der referenzierten Sektion.
                    feldIdJeSektionsCode[f.SektionsCodes[i]] = feld.Id;
                }
            }

            var stufeJpId = (await db.Kulturstufen.FirstAsync(k => k.Code == "JP", ct)).Id;
            var stufeTpId = (await db.Kulturstufen.FirstAsync(k => k.Code == "TP", ct)).Id;
            var stufeVpId = (await db.Kulturstufen.FirstAsync(k => k.Code == "VP", ct)).Id;

            var kulturpflanzenDefs = new (string Bez, string BotanischerName, string JpSektion, string TpSektion, string VpSektion)[]
            {
                ("Salvia nemorosa 'Caradonna'", "Salvia nemorosa 'Caradonna'", "A1", "A4", "B1"),
                ("Geranium 'Rozanne'", "Geranium 'Rozanne'", "A2", "A5", "B2"),
                ("Echinacea purpurea", "Echinacea purpurea", "A3", "A6", "B3"),
                ("Hosta 'Blue Angel'", "Hosta 'Blue Angel'", "C1", "C3", "B4"),
                ("Astilbe arendsii", "Astilbe arendsii", "C2", "C4", "B5"),
            };

            var stkEinheitId = einheiten["Stk"];
            foreach (var p in kulturpflanzenDefs)
            {
                var artikelDto = new ArtikelDto
                {
                    Bezeichnung = p.Bez,
                    BotanischerName = p.BotanischerName,
                    IstKulturpflanze = true,
                    EinheitId = stkEinheitId,
                    MwStSatzId = mwst19Id,
                    Einkaufspreis = 0.80m,
                    Listenpreis = 4.90m,
                    IstLagerartikel = true,
                };
                var gespeichert = await artikelService.SpeichereAsync(artikelDto, ct);

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await BestandService.BucheBewegungAsync(
                    db, gespeichert.Id, feldIdJeSektionsCode[p.JpSektion], 500m, LagerbewegungTyp.Kulturzugang, null, ct,
                    sektionenJeCode[p.JpSektion], stufeJpId);
                await BestandService.BucheBewegungAsync(
                    db, gespeichert.Id, feldIdJeSektionsCode[p.TpSektion], 200m, LagerbewegungTyp.Kulturzugang, null, ct,
                    sektionenJeCode[p.TpSektion], stufeTpId);
                await BestandService.BucheBewegungAsync(
                    db, gespeichert.Id, feldIdJeSektionsCode[p.VpSektion], 100m, LagerbewegungTyp.Kulturzugang, null, ct,
                    sektionenJeCode[p.VpSektion], stufeVpId);
                await transaction.CommitAsync(ct);
            }
        }

        return true;
    }
}
