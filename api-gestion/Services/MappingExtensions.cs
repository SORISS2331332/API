using api_gestion.DTOs;
using api_gestion.Models;

namespace api_gestion.Services;

public static class MappingExtensions
{
    public static ProduitDto VersDto(this Produit p) => new(
        p.Id,
        p.Nom,
        p.Reference,
        p.CodeBarre,
        p.Categorie,
        p.QuantiteStock,
        p.SeuilAlerte,
        p.PrixUnitaire,
        p.Description,
        p.Actif,
        p.EnRupture,
        p.EnAlerte,
        p.CreeLe,
        p.MisAJourLe
    );

    public static MouvementDto VersDto(this MouvementStock m) => new(
        m.Id,
        m.ProduitId,
        m.Produit?.Nom ?? string.Empty,
        m.Produit?.Reference ?? string.Empty,
        m.Type,
        m.Quantite,
        m.QuantiteAvant,
        m.QuantiteApres,
        m.Commentaire,
        m.Utilisateur,
        m.Source,
        m.DateHeure
    );
}
