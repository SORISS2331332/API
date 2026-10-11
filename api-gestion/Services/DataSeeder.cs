using api_gestion.Data;
using api_gestion.Models;
using Microsoft.EntityFrameworkCore;

namespace api_gestion.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (!await db.Utilisateurs.AnyAsync())
        {
            db.Utilisateurs.Add(new Utilisateur
            {
                Nom = "Administrateur",
                Email = "admin@stock.local",
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = RoleUtilisateur.Admin
            });

            db.Utilisateurs.Add(new Utilisateur
            {
                Nom = "Opérateur Caisse",
                Email = "operateur@stock.local",
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("Operateur123!"),
                Role = RoleUtilisateur.Operateur
            });

            await db.SaveChangesAsync();
        }

        if (!await db.Produits.AnyAsync())
        {
            var maintenant = DateTime.UtcNow;
            var produits = new List<Produit>
            {
                new() { Nom = "Coca-Cola 33cl", Reference = "BOI-001", CodeBarre = "3017620422003", Categorie = "Boissons", QuantiteStock = 48, SeuilAlerte = 15, PrixUnitaire = 500m, Description = "Canette 33cl" },
                new() { Nom = "Eau minérale 50cl", Reference = "BOI-002", CodeBarre = "3274080005003", Categorie = "Boissons", QuantiteStock = 6, SeuilAlerte = 20, PrixUnitaire = 300m, Description = "Bouteille plastique 50cl" },
                new() { Nom = "Jus d'orange 1L", Reference = "BOI-003", CodeBarre = "3023290004012", Categorie = "Boissons", QuantiteStock = 0, SeuilAlerte = 10, PrixUnitaire = 1200m, Description = "Jus pur fruit" },
                new() { Nom = "Chips nature 150g", Reference = "SNK-001", CodeBarre = "8710398503961", Categorie = "Snacks", QuantiteStock = 32, SeuilAlerte = 10, PrixUnitaire = 750m },
                new() { Nom = "Barre chocolatée", Reference = "SNK-002", CodeBarre = "7622210449283", Categorie = "Snacks", QuantiteStock = 3, SeuilAlerte = 5, PrixUnitaire = 500m },
                new() { Nom = "Biscuits sablés", Reference = "SNK-003", CodeBarre = "3038350994400", Categorie = "Snacks", QuantiteStock = 58, SeuilAlerte = 12, PrixUnitaire = 1000m },
                new() { Nom = "Savon liquide 250ml", Reference = "HYG-001", CodeBarre = "8001090318411", Categorie = "Hygiène", QuantiteStock = 14, SeuilAlerte = 8, PrixUnitaire = 1500m },
                new() { Nom = "Dentifrice 75ml", Reference = "HYG-002", CodeBarre = "3014230002311", Categorie = "Hygiène", QuantiteStock = 2, SeuilAlerte = 6, PrixUnitaire = 1200m },
                new() { Nom = "Shampoing 400ml", Reference = "HYG-003", CodeBarre = "8710447261019", Categorie = "Hygiène", QuantiteStock = 21, SeuilAlerte = 5, PrixUnitaire = 2000m },
                new() { Nom = "Cahier 96 pages", Reference = "PAP-001", CodeBarre = "3296580102426", Categorie = "Papeterie", QuantiteStock = 75, SeuilAlerte = 20, PrixUnitaire = 750m },
                new() { Nom = "Stylo bille bleu", Reference = "PAP-002", CodeBarre = "3086123537026", Categorie = "Papeterie", QuantiteStock = 4, SeuilAlerte = 25, PrixUnitaire = 200m },
                new() { Nom = "Pile AA (x4)", Reference = "ELE-001", CodeBarre = "4006822189554", Categorie = "Électronique", QuantiteStock = 17, SeuilAlerte = 10, PrixUnitaire = 2500m },
            };

            foreach (var p in produits)
            {
                p.CreeLe = maintenant;
                p.MisAJourLe = maintenant;
            }

            db.Produits.AddRange(produits);
            await db.SaveChangesAsync();

            // Génération de mouvements de démonstration sur les 14 derniers jours
            var rnd = new Random(42);
            var mouvements = new List<MouvementStock>();

            foreach (var p in produits)
            {
                mouvements.Add(new MouvementStock
                {
                    ProduitId = p.Id,
                    Type = TypeMouvement.CreationInitiale,
                    Quantite = p.QuantiteStock,
                    QuantiteAvant = 0,
                    QuantiteApres = p.QuantiteStock,
                    Commentaire = "Stock initial",
                    Source = "gestion",
                    DateHeure = maintenant.AddDays(-14)
                });

                var stockCourant = p.QuantiteStock + rnd.Next(5, 20);
                for (var jour = 13; jour >= 0; jour--)
                {
                    var ventesJour = rnd.Next(0, 6);
                    for (var v = 0; v < ventesJour && stockCourant > 0; v++)
                    {
                        var avant = stockCourant;
                        stockCourant -= 1;
                        mouvements.Add(new MouvementStock
                        {
                            ProduitId = p.Id,
                            Type = TypeMouvement.SortieScan,
                            Quantite = -1,
                            QuantiteAvant = avant,
                            QuantiteApres = stockCourant,
                            Commentaire = "Sortie via scan",
                            Source = "scan",
                            DateHeure = maintenant.AddDays(-jour).AddHours(rnd.Next(8, 20)).AddMinutes(rnd.Next(0, 60))
                        });
                    }
                }
            }

            db.Mouvements.AddRange(mouvements);
            await db.SaveChangesAsync();
        }

        await CorrigerCodesBarresInvalidesAsync(db);
    }

    /// <summary>
    /// Auto-réparation exécutée à chaque démarrage : corrige en base tout code-barres EAN-13
    /// (13 chiffres) dont la clé de contrôle est invalide. Nécessaire car la génération d'image
    /// (ZXing) recalcule toujours sa propre clé de contrôle à l'impression : si la valeur stockée
    /// ne correspond pas à celle qui sera réellement imprimée/scannée, le scan échoue avec
    /// "code-barres introuvable" même si le produit existe et est en stock.
    /// </summary>
    private static async Task CorrigerCodesBarresInvalidesAsync(AppDbContext db)
    {
        try
        {
            var produits = await db.Produits.ToListAsync();
            var corriges = 0;

            foreach (var p in produits)
            {
                var corrige = BarcodeService.NormaliserCodeBarreEan13(p.CodeBarre);
                if (corrige != p.CodeBarre)
                {
                    p.CodeBarre = corrige;
                    p.MisAJourLe = DateTime.UtcNow;
                    corriges++;
                }
            }

            if (corriges > 0)
            {
                await db.SaveChangesAsync();
                Console.WriteLine($"[DataSeeder] {corriges} code(s)-barres EAN-13 corrigé(s) (clé de contrôle invalide).");
            }
        }
        catch (Exception ex)
        {
            // Ne doit jamais empêcher le démarrage de l'API : simple log, on réessaiera au prochain démarrage.
            Console.WriteLine($"[DataSeeder] Échec de la correction automatique des codes-barres : {ex.Message}");
        }
    }
}
