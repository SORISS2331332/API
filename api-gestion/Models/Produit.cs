using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api_gestion.Models;

public class Produit
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Nom { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string CodeBarre { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Categorie { get; set; }

    public int QuantiteStock { get; set; }

    /// <summary>
    /// Seuil personnalisé en dessous duquel le produit est considéré en alerte / rupture.
    /// </summary>
    public int SeuilAlerte { get; set; } = 5;

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PrixUnitaire { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    public bool Actif { get; set; } = true;

    public DateTime CreeLe { get; set; } = DateTime.UtcNow;

    public DateTime MisAJourLe { get; set; } = DateTime.UtcNow;

    public ICollection<MouvementStock> Mouvements { get; set; } = new List<MouvementStock>();

    [NotMapped]
    public bool EnRupture => QuantiteStock <= 0;

    [NotMapped]
    public bool EnAlerte => QuantiteStock > 0 && QuantiteStock <= SeuilAlerte;
}
