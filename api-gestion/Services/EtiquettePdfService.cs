using System.Globalization;
using api_gestion.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace api_gestion.Services;

/// <summary>
/// Génération de documents PDF d'étiquettes code-barres prêtes à imprimer.
/// </summary>
public static class EtiquettePdfService
{
    private static readonly CultureInfo CultureMontant = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>
    /// Formate un montant en Francs CFA (ex. "1 500 F CFA").
    /// </summary>
    private static string FormaterMontant(decimal montant) =>
        $"{montant.ToString("N0", CultureMontant)} F CFA";

    /// <summary>
    /// Génère un PDF avec une seule étiquette grand format pour un produit donné.
    /// </summary>
    public static byte[] GenererEtiquetteUnique(Produit produit, byte[] imageCodeBarre)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(100, 60, Unit.Millimetre);
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily("Lato"));

                page.Content().Column(col =>
                {
                    col.Spacing(2);

                    col.Item().AlignCenter().Text(produit.Nom)
                        .FontSize(12).Bold().AlignCenter();

                    col.Item().AlignCenter().Text($"Réf. {produit.Reference}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);

                    col.Item().AlignCenter().Height(26, Unit.Millimetre)
                        .Image(imageCodeBarre).FitArea();

                    if (produit.PrixUnitaire.HasValue)
                    {
                        col.Item().AlignCenter().Text(FormaterMontant(produit.PrixUnitaire.Value))
                            .FontSize(11).Bold();
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Génère un PDF multi-pages en grille d'étiquettes pour l'ensemble des produits fournis,
    /// prêt à imprimer sur des planches d'étiquettes A4.
    /// </summary>
    public static byte[] GenererPlanchesEtiquettes(IReadOnlyList<Produit> produits, Func<Produit, byte[]> genererImage)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily("Lato"));

                page.Header().PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Text("StockWave — Planche d'étiquettes code-barres")
                        .FontSize(14).Bold();
                    row.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Inlined(inlined =>
                {
                    inlined.Spacing(4, Unit.Millimetre);
                    inlined.AlignCenter();

                    foreach (var produit in produits)
                    {
                        var image = genererImage(produit);

                        inlined.Item().Width(63, Unit.Millimetre).Height(38, Unit.Millimetre)
                            .Border(0.75f).BorderColor(Colors.Grey.Lighten1).Padding(3, Unit.Millimetre)
                            .Column(col =>
                            {
                                col.Spacing(1);

                                col.Item().AlignCenter().Text(produit.Nom)
                                    .FontSize(8).Bold().AlignCenter();

                                col.Item().AlignCenter().Text($"Réf. {produit.Reference}")
                                    .FontSize(6).FontColor(Colors.Grey.Darken2);

                                col.Item().AlignCenter().Height(16, Unit.Millimetre)
                                    .Image(image).FitArea();

                                if (produit.PrixUnitaire.HasValue)
                                {
                                    col.Item().AlignCenter().Text(FormaterMontant(produit.PrixUnitaire.Value))
                                        .FontSize(7).Bold();
                                }
                            });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
