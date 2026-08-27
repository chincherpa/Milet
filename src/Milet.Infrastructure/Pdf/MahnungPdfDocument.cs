using Milet.Application.Admin;
using Milet.Application.Finanzen;
using Milet.Application.Stammdaten;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Milet.Infrastructure.Pdf;

public sealed class MahnungPdfDocument(MahnungDto mahnung, KundeDto kunde, FirmenstammDto firma, string mahntitel) : IDocument
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
                col.Item().PaddingTop(10).LineHorizontal(1);
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text(mahntitel).FontSize(16).Bold();
                col.Item().Text($"Datum: {mahnung.MahnDatum:dd.MM.yyyy}");

                col.Item().PaddingTop(4).Text(kunde.Adresse.Name1);
                if (!string.IsNullOrWhiteSpace(kunde.Adresse.Name2))
                    col.Item().Text(kunde.Adresse.Name2!);
                col.Item().Text(kunde.Adresse.Strasse);
                col.Item().Text($"{kunde.Adresse.Plz} {kunde.Adresse.Ort}");

                col.Item().PaddingTop(10).Text(
                    "trotz Fälligkeit konnten wir bislang keinen Zahlungseingang für folgende Rechnung(en) feststellen. " +
                    "Bitte gleichen Sie den offenen Betrag umgehend aus.");

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Rechnung");
                        h.Cell().AlignRight().Text("Offener Betrag");
                    });

                    foreach (var position in mahnung.Positionen)
                    {
                        table.Cell().Text(position.BelegNummerSnapshot);
                        table.Cell().AlignRight().Text($"{position.OffenerBetragSnapshot:0.00} €");
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Column(sum =>
                {
                    if (mahnung.Gebuehr > 0)
                        sum.Item().Text($"Mahngebühr: {mahnung.Gebuehr:0.00} €");
                    sum.Item().Text($"Gesamtbetrag: {mahnung.Gesamtbetrag:0.00} €").Bold();
                });
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
