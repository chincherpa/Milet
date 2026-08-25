using Milet.Application.Admin;
using Milet.Application.Verkauf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Milet.Infrastructure.Pdf;

public sealed class BelegPdfDocument(BelegDto beleg, FirmenstammDto firma, string dokumenttitel) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text(firma.Firmenname).FontSize(14).Bold();
                col.Item().Text($"{firma.Adresse.Strasse}, {firma.Adresse.Plz} {firma.Adresse.Ort}");
                if (!string.IsNullOrWhiteSpace(firma.UStIdNr))
                    col.Item().Text($"USt-IdNr.: {firma.UStIdNr}");
                col.Item().PaddingTop(10).LineHorizontal(1);
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text(dokumenttitel).FontSize(16).Bold();
                col.Item().Text($"Nummer: {(string.IsNullOrEmpty(beleg.BelegNummer) ? "(Entwurf)" : beleg.BelegNummer)}");
                col.Item().Text($"Datum: {beleg.BelegDatum:dd.MM.yyyy}");

                col.Item().PaddingTop(4).Text(beleg.RechnungsadresseSnapshot.Name1);
                if (!string.IsNullOrWhiteSpace(beleg.RechnungsadresseSnapshot.Name2))
                    col.Item().Text(beleg.RechnungsadresseSnapshot.Name2!);
                col.Item().Text(beleg.RechnungsadresseSnapshot.Strasse);
                col.Item().Text($"{beleg.RechnungsadresseSnapshot.Plz} {beleg.RechnungsadresseSnapshot.Ort}");

                if (!string.IsNullOrWhiteSpace(beleg.Kopftext))
                    col.Item().PaddingTop(10).Text(beleg.Kopftext);

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(30);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Pos");
                        h.Cell().Text("Bezeichnung");
                        h.Cell().AlignRight().Text("Menge");
                        h.Cell().AlignRight().Text("Preis");
                        h.Cell().AlignRight().Text("Rabatt%");
                        h.Cell().AlignRight().Text("Gesamt");
                    });

                    foreach (var position in beleg.Positionen)
                    {
                        table.Cell().Text(position.PositionsNr.ToString());
                        table.Cell().Text(position.Bezeichnung);
                        table.Cell().AlignRight().Text($"{position.Menge:0.###} {position.EinheitKuerzel}");
                        table.Cell().AlignRight().Text($"{position.Einzelpreis:0.00}");
                        table.Cell().AlignRight().Text($"{position.RabattProzent:0.##}");
                        table.Cell().AlignRight().Text($"{position.GesamtNetto:0.00}");
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Column(sum =>
                {
                    sum.Item().Text($"Netto: {beleg.SummeNetto:0.00} €");
                    sum.Item().Text($"MwSt: {beleg.SummeMwSt:0.00} €");
                    sum.Item().Text($"Brutto: {beleg.SummeBrutto:0.00} €").Bold();
                });

                if (beleg.Faelligkeit is { } faelligkeit)
                    col.Item().PaddingTop(10).Text($"Fällig am: {faelligkeit:dd.MM.yyyy}");

                if (!string.IsNullOrWhiteSpace(beleg.Fusstext))
                    col.Item().PaddingTop(10).Text(beleg.Fusstext);
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}
