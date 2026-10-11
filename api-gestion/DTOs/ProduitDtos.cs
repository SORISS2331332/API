namespace api_gestion.DTOs;

public record ProduitDto(
    int Id,
    string Nom,
    string Reference,
    string CodeBarre,
    string? Categorie,
    int QuantiteStock,
    int SeuilAlerte,
    decimal? PrixUnitaire,
    string? Description,
    bool Actif,
    bool EnRupture,
    bool EnAlerte,
    DateTime CreeLe,
    DateTime MisAJourLe
);

public record CreerProduitDto(
    string Nom,
    string Reference,
    string CodeBarre,
    string? Categorie,
    int QuantiteStock,
    int SeuilAlerte,
    decimal? PrixUnitaire,
    string? Description
);

public record ModifierProduitDto(
    string Nom,
    string Reference,
    string CodeBarre,
    string? Categorie,
    int SeuilAlerte,
    decimal? PrixUnitaire,
    string? Description,
    bool Actif
);

public record AjusterStockDto(
    int Quantite, // valeur absolue de la nouvelle quantité
    string? Commentaire
);
