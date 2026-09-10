using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Services;

namespace Milet.Infrastructure.Persistence.Seed;

/// <summary>
/// Testdaten für Entwicklung/Demo: Stauden-Sortiment über alle Kulturstufen (Jungpflanze/Teenagerpflanze/
/// Verkaufspflanze) verteilt auf die Felder/Sektionen des Gärtnereiplans, dazu Kunden, Lieferanten,
/// Preisliste + Staffelpreise sowie ein paar Angebote/Aufträge/Rechnungen in unterschiedlichen Status.
/// Läuft über die echten Application-Services (nicht direkt gegen den DbContext), damit Nummernkreise,
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

        // --- Gärtnereiplan: Felder + Sektionen --------------------------------------
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
        int stufeJpId, stufeTpId, stufeVpId;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var plan = await db.Gaertnereiplaene.FirstAsync(ct);

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

            stufeJpId = (await db.Kulturstufen.FirstAsync(k => k.Code == "JP", ct)).Id;
            stufeTpId = (await db.Kulturstufen.FirstAsync(k => k.Code == "TP", ct)).Id;
            stufeVpId = (await db.Kulturstufen.FirstAsync(k => k.Code == "VP", ct)).Id;
        }

        // --- Artikel: Stauden-Sortiment ----------------------------------------------
        // Jede Staude durchläuft die drei Kulturstufen (Jungpflanze -> Teenagerpflanze -> Verkaufspflanze,
        // s. StammdatenSeed) mit sinkenden Stückzahlen je Stufe (Anzucht-Schwund, je Art unterschiedlich
        // stark) und wird über alle drei Felder verteilt: Anzucht (Jung-/Teenagerpflanzen) in Feld Nord +
        // Folientunnel, verkaufsfertige Ware in Feld Süd. Pflanzen unterliegen dem ermäßigten Steuersatz
        // (§12 Abs. 2 UStG, Anlage 2), daher mwst7Id statt mwst19Id.
        var growSektionsCodes = new[] { "A1", "A2", "A3", "A4", "A5", "A6", "C1", "C2", "C3", "C4" };
        var verkaufsSektionsCodes = new[] { "B1", "B2", "B3", "B4", "B5" };

        var staudenDefs = new (string Bez, string BotanischerName, decimal Ek, decimal Vk, decimal MengeJp, decimal MengeTp, decimal MengeVp)[]
        {
            ("Lavendel 'Hidcote'", "Lavandula angustifolia 'Hidcote'", 0.45m, 4.50m, 600, 250, 90),
            ("Steppensalbei 'Caradonna'", "Salvia nemorosa 'Caradonna'", 0.40m, 3.90m, 550, 220, 80),
            ("Storchschnabel 'Rozanne'", "Geranium 'Rozanne'", 0.55m, 5.90m, 450, 180, 70),
            ("Purpur-Sonnenhut", "Echinacea purpurea", 0.50m, 4.90m, 500, 200, 75),
            ("Sonnenhut 'Goldsturm'", "Rudbeckia fulgida 'Goldsturm'", 0.45m, 4.50m, 480, 190, 65),
            ("Katzenminze 'Walker's Low'", "Nepeta faassenii 'Walker's Low'", 0.35m, 3.50m, 700, 300, 110),
            ("Fetthenne 'Herbstfreude'", "Sedum spectabile 'Herbstfreude'", 0.40m, 3.90m, 400, 160, 55),
            ("Glattblattaster", "Aster novi-belgii", 0.40m, 3.90m, 420, 170, 60),
            ("Flammenblume", "Phlox paniculata", 0.60m, 6.50m, 300, 120, 45),
            ("Taglilie 'Stella de Oro'", "Hemerocallis 'Stella de Oro'", 0.90m, 7.90m, 260, 100, 35),
            ("Purpurglöckchen 'Palace Purple'", "Heuchera 'Palace Purple'", 0.70m, 6.90m, 320, 130, 50),
            ("Frauenmantel", "Alchemilla mollis", 0.30m, 3.20m, 650, 280, 100),
            ("Funkie 'Blue Angel'", "Hosta 'Blue Angel'", 0.80m, 7.50m, 280, 110, 40),
            ("Prachtspiere", "Astilbe arendsii", 0.55m, 5.50m, 350, 140, 50),
        };

        var stkEinheitId = einheiten["Stk"];
        var staudenIds = new List<int>();
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            for (var i = 0; i < staudenDefs.Length; i++)
            {
                var p = staudenDefs[i];
                var artikelDto = new ArtikelDto
                {
                    Bezeichnung = p.Bez,
                    BotanischerName = p.BotanischerName,
                    IstKulturpflanze = true,
                    EinheitId = stkEinheitId,
                    MwStSatzId = mwst7Id,
                    Einkaufspreis = p.Ek,
                    Listenpreis = p.Vk,
                    IstLagerartikel = true,
                };
                var gespeichert = await artikelService.SpeichereAsync(artikelDto, ct);
                staudenIds.Add(gespeichert.Id);

                var jpCode = growSektionsCodes[i % growSektionsCodes.Length];
                var tpCode = growSektionsCodes[(i + 5) % growSektionsCodes.Length];
                var vpCode = verkaufsSektionsCodes[i % verkaufsSektionsCodes.Length];

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await BestandService.BucheBewegungAsync(
                    db, gespeichert.Id, feldIdJeSektionsCode[jpCode], p.MengeJp, LagerbewegungTyp.Kulturzugang, null, ct,
                    sektionenJeCode[jpCode], stufeJpId);
                await BestandService.BucheBewegungAsync(
                    db, gespeichert.Id, feldIdJeSektionsCode[tpCode], p.MengeTp, LagerbewegungTyp.Kulturzugang, null, ct,
                    sektionenJeCode[tpCode], stufeTpId);
                await BestandService.BucheBewegungAsync(
                    db, gespeichert.Id, feldIdJeSektionsCode[vpCode], p.MengeVp, LagerbewegungTyp.Kulturzugang, null, ct,
                    sektionenJeCode[vpCode], stufeVpId);
                await transaction.CommitAsync(ct);
            }
        }

        // Kurzreferenzen für Preisliste/Belegpositionen weiter unten.
        var lavendel = staudenIds[0];
        var storchschnabel = staudenIds[2];
        var sonnenhut = staudenIds[3];
        var goldsturm = staudenIds[4];
        var katzenminze = staudenIds[5];
        var fetthenne = staudenIds[6];
        var phlox = staudenIds[8];
        var taglilie = staudenIds[9];
        var purpurgloeckchen = staudenIds[10];
        var frauenmantel = staudenIds[11];
        var funkie = staudenIds[12];
        var prachtspiere = staudenIds[13];

        // Dienstleistung (kein Lagerartikel) — deckt weiterhin den Freitext/Nicht-Lagerartikel-Pfad in
        // Belegpositionen ab, jetzt thematisch passend zur Staudengärtnerei statt "Montage/Einrichtung".
        var pflanzservice = (await artikelService.SpeichereAsync(new ArtikelDto
        {
            Bezeichnung = "Pflanzung vor Ort",
            EinheitId = einheiten["h"],
            MwStSatzId = mwst19Id,
            Einkaufspreis = 0m,
            Listenpreis = 45.00m,
            IstLagerartikel = false,
        }, ct)).Id;

        // --- Preisliste + Staffelpreise ---------------------------------------------
        var preisliste = await preislistenService.SpeichereAsync(new PreislisteDto { Name = "Landschaftsbau Staffelpreise" }, ct);
        await artikelPreiseService.SpeichereAsync(new ArtikelPreisDto { PreislisteId = preisliste.Id, ArtikelId = katzenminze, AbMenge = 50, Preis = 2.90m }, ct);
        await artikelPreiseService.SpeichereAsync(new ArtikelPreisDto { PreislisteId = preisliste.Id, ArtikelId = frauenmantel, AbMenge = 30, Preis = 2.60m }, ct);
        await artikelPreiseService.SpeichereAsync(new ArtikelPreisDto { PreislisteId = preisliste.Id, ArtikelId = frauenmantel, AbMenge = 100, Preis = 2.20m }, ct);

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
            await PositionAsync(1, lavendel, 20, kunde1),
            await PositionAsync(2, storchschnabel, 10, kunde1),
            await PositionAsync(3, katzenminze, 5, kunde1),
        ]);
        var auftrag1 = await ueberleitungService.UeberleitenAsync(angebot1.Id, BelegTyp.Auftrag, ct);
        var rechnung1 = await ueberleitungService.UeberleitenAsync(auftrag1.Id, BelegTyp.Rechnung, ct);
        await buchenService.BuchenAsync(rechnung1.Id, ct);

        // 2) Autohaus Krüger: Angebot bleibt offen (Staffelpreis Katzenminze greift)
        var kunde2 = kundenIds[1];
        await AngebotAnlegenAsync(kunde2, heute.AddDays(-6), [
            await PositionAsync(1, katzenminze, 60, kunde2),
            await PositionAsync(2, frauenmantel, 10, kunde2),
        ]);

        // 3) Café Mocca: Angebot -> Auftrag (noch nicht fakturiert)
        var kunde3 = kundenIds[2];
        var angebot3 = await AngebotAnlegenAsync(kunde3, heute.AddDays(-10), [
            await PositionAsync(1, phlox, 24, kunde3),
            await PositionAsync(2, taglilie, 12, kunde3),
        ]);
        await ueberleitungService.UeberleitenAsync(angebot3.Id, BelegTyp.Auftrag, ct);

        // 4) Fischer & Söhne: Angebot -> Auftrag -> Rechnung (gebucht), inkl. Dienstleistung
        var kunde4 = kundenIds[3];
        var angebot4 = await AngebotAnlegenAsync(kunde4, heute.AddDays(-18), [
            await PositionAsync(1, purpurgloeckchen, 15, kunde4),
            await PositionAsync(2, funkie, 8, kunde4),
            await PositionAsync(3, pflanzservice, 3, kunde4),
        ]);
        var auftrag4 = await ueberleitungService.UeberleitenAsync(angebot4.Id, BelegTyp.Auftrag, ct);
        var rechnung4 = await ueberleitungService.UeberleitenAsync(auftrag4.Id, BelegTyp.Rechnung, ct);
        await buchenService.BuchenAsync(rechnung4.Id, ct);

        // 5) IT-Systemhaus Nordlicht: Angebot -> Auftrag -> Rechnung (gebucht), Staffelpreise Frauenmantel
        var kunde5 = kundenIds[4];
        var angebot5 = await AngebotAnlegenAsync(kunde5, heute.AddDays(-14), [
            await PositionAsync(1, frauenmantel, 120, kunde5),
            await PositionAsync(2, goldsturm, 20, kunde5),
            await PositionAsync(3, sonnenhut, 15, kunde5),
        ]);
        var auftrag5 = await ueberleitungService.UeberleitenAsync(angebot5.Id, BelegTyp.Auftrag, ct);
        var rechnung5 = await ueberleitungService.UeberleitenAsync(auftrag5.Id, BelegTyp.Rechnung, ct);
        await buchenService.BuchenAsync(rechnung5.Id, ct);

        // 6) Praxis Dr. Wagner: Angebot mit gemischtem Steuersatz (Pflanzen 7% + Fracht 19%) + Freitextposition
        var kunde6 = kundenIds[5];
        await AngebotAnlegenAsync(kunde6, heute.AddDays(-2), [
            await PositionAsync(1, prachtspiere, 3, kunde6),
            await PositionAsync(2, fetthenne, 5, kunde6),
            Freitext(3, "Lieferung & Versand", 9.90m),
        ]);

        return true;
    }
}
