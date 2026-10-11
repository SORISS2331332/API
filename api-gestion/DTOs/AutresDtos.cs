namespace api_gestion.DTOs;

public record LoginDto(string Email, string MotDePasse);

public record LoginResponseDto(string Token, string Nom, string Email, string Role, DateTime ExpireLe);

public record StatsDashboardDto(
    int TotalProduits,
    int TotalReferences,
    int ProduitsEnAlerte,
    int ProduitsEnRupture,
    int SortiesAujourdHui,
    int SortiesCetteSemaine,
    int MouvementsAujourdHui
);

public record VenteParJourDto(DateOnly Jour, int Quantite);

public record ProduitVenteDto(int ProduitId, string Nom, string Reference, string? Categorie, int QuantiteVendue);

public record BilanPeriodeDto(
    DateOnly Debut,
    DateOnly Fin,
    int TotalSorties,
    int TotalEntrees,
    int TotalAjustements,
    List<VenteParJourDto> VentesParJour,
    List<ProduitVenteDto> TopVentes,
    List<ProduitVenteDto> FlopVentes
);
