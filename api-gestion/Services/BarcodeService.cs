using api_gestion.Data;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using ZXing.SkiaSharp.Rendering;

namespace api_gestion.Services;

/// <summary>
/// Génération de codes-barres EAN-13 uniques et rendu en image PNG pour impression.
/// </summary>
public class BarcodeService
{
    private readonly AppDbContext _db;
    private static readonly Random Rand = new();

    public BarcodeService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Génère un code-barres EAN-13 (préfixe 20-29 réservé à l'usage interne/magasin)
    /// garanti unique en base de données.
    /// </summary>
    public async Task<string> GenererCodeUniqueAsync()
    {
        string code;
        var tentatives = 0;
        do
        {
            code = GenererEan13();
            tentatives++;
        }
        while (tentatives < 30 && await _db.Produits.AnyAsync(p => p.CodeBarre == code));

        return code;
    }

    private static string GenererEan13()
    {
        var chiffres = new int[12];
        chiffres[0] = 2; // préfixe interne 20-29
        chiffres[1] = Rand.Next(0, 10);
        for (var i = 2; i < 12; i++)
        {
            chiffres[i] = Rand.Next(0, 10);
        }

        var cle = CalculerCleControleEan13(chiffres);
        return string.Concat(chiffres) + cle;
    }

    private static int CalculerCleControleEan13(int[] douzeChiffres)
    {
        var somme = 0;
        for (var i = 0; i < 12; i++)
        {
            somme += douzeChiffres[i] * (i % 2 == 0 ? 1 : 3);
        }

        var reste = somme % 10;
        return reste == 0 ? 0 : 10 - reste;
    }

    /// <summary>
    /// Génère l'image PNG (bytes) d'un code-barres. Utilise le format EAN-13 si le code
    /// est numérique à 12 ou 13 chiffres (standard), sinon se rabat sur le format Code 128.
    /// </summary>
    public static byte[] GenererImagePng(string codeBarre, int largeur = 320, int hauteur = 140)
    {
        var estNumeriqueEan = (codeBarre.Length == 12 || codeBarre.Length == 13) && codeBarre.All(char.IsDigit);
        var format = estNumeriqueEan ? BarcodeFormat.EAN_13 : BarcodeFormat.CODE_128;
        var contenu = estNumeriqueEan && codeBarre.Length == 13 ? codeBarre[..12] : codeBarre;

        var writer = new BarcodeWriter<SKBitmap>
        {
            Format = format,
            Renderer = new SKBitmapRenderer(),
            Options = new EncodingOptions
            {
                Width = largeur,
                Height = hauteur,
                Margin = 2,
                PureBarcode = false
            }
        };

        using var bitmap = writer.Write(contenu);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
