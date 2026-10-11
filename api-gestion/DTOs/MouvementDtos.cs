using api_gestion.Models;

namespace api_gestion.DTOs;

public record MouvementDto(
    int Id,
    int ProduitId,
    string ProduitNom,
    string ProduitReference,
    TypeMouvement Type,
    int Quantite,
    int QuantiteAvant,
    int QuantiteApres,
    string? Commentaire,
    string? Utilisateur,
    string? Source,
    DateTime DateHeure
);

public record ScanResultDto(
    bool Succes,
    string? Message,
    ProduitDto? Produit,
    bool Alerte,
    bool Rupture,
    bool DoublonIgnore
);

public record ScanRequestDto(
    string CodeBarre,
    string? Utilisateur
);
