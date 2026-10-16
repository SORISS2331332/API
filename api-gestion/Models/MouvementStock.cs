using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api_gestion.Models;

public enum TypeMouvement
{
    SortieScan = 0,
    AjustementManuel = 1,
    Entree = 2,
    CreationInitiale = 3,
    Correction = 4,
    AnnulationScan = 5
}

public class MouvementStock
{
    public int Id { get; set; }

    public int ProduitId { get; set; }

    public Produit? Produit { get; set; }

    public TypeMouvement Type { get; set; }

    /// <summary>
    /// Variation appliquée au stock (peut être négative pour une sortie).
    /// </summary>
    public int Quantite { get; set; }

    public int QuantiteAvant { get; set; }

    public int QuantiteApres { get; set; }

    [MaxLength(255)]
    public string? Commentaire { get; set; }

    [MaxLength(100)]
    public string? Utilisateur { get; set; }

    [MaxLength(30)]
    public string? Source { get; set; } // "scan" ou "gestion"

    public DateTime DateHeure { get; set; } = DateTime.UtcNow;
}
