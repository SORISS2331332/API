using api_gestion.Data;
using api_gestion.DTOs;
using api_gestion.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_gestion.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<StatsDashboardDto>> Dashboard()
    {
        var aujourdHui = DateTime.UtcNow.Date;
        var debutSemaine = aujourdHui.AddDays(-7);

        var produits = await _db.Produits.ToListAsync();

        // Les annulations de scan (+1) sont déduites des sorties (-1)
        var sortiesAujourdHui = await _db.Mouvements
            .Where(m => (m.Type == TypeMouvement.SortieScan || m.Type == TypeMouvement.AnnulationScan) && m.DateHeure >= aujourdHui)
            .SumAsync(m => (int?)-m.Quantite) ?? 0;

        var sortiesSemaine = await _db.Mouvements
            .Where(m => (m.Type == TypeMouvement.SortieScan || m.Type == TypeMouvement.AnnulationScan) && m.DateHeure >= debutSemaine)
            .SumAsync(m => (int?)-m.Quantite) ?? 0;

        var mouvementsAujourdHui = await _db.Mouvements.CountAsync(m => m.DateHeure >= aujourdHui);

        var dto = new StatsDashboardDto(
            TotalProduits: produits.Sum(p => p.QuantiteStock),
            TotalReferences: produits.Count,
            ProduitsEnAlerte: produits.Count(p => p.EnAlerte),
            ProduitsEnRupture: produits.Count(p => p.EnRupture),
            SortiesAujourdHui: sortiesAujourdHui,
            SortiesCetteSemaine: sortiesSemaine,
            MouvementsAujourdHui: mouvementsAujourdHui
        );

        return Ok(dto);
    }

    // GET api/stats/bilan?periode=semaine|mois
    [HttpGet("bilan")]
    public async Task<ActionResult<BilanPeriodeDto>> Bilan([FromQuery] string periode = "semaine")
    {
        var fin = DateTime.UtcNow.Date.AddDays(1);
        var debut = periode == "mois" ? fin.AddMonths(-1) : fin.AddDays(-7);

        var mouvements = await _db.Mouvements
            .Include(m => m.Produit)
            .Where(m => m.DateHeure >= debut && m.DateHeure < fin)
            .ToListAsync();

        var sorties = mouvements.Where(m => m.Type == TypeMouvement.SortieScan || m.Type == TypeMouvement.AnnulationScan).ToList();
        var entrees = mouvements.Where(m => m.Type == TypeMouvement.Entree || (m.Type == TypeMouvement.AjustementManuel && m.Quantite > 0)).ToList();
        var ajustements = mouvements.Where(m => m.Type == TypeMouvement.AjustementManuel).ToList();

        var ventesParJour = sorties
            .GroupBy(m => DateOnly.FromDateTime(m.DateHeure))
            .Select(g => new VenteParJourDto(g.Key, -g.Sum(m => m.Quantite)))
            .OrderBy(v => v.Jour)
            .ToList();

        var venteParProduit = sorties
            .GroupBy(m => m.ProduitId)
            .Select(g => new
            {
                ProduitId = g.Key,
                Quantite = -g.Sum(m => m.Quantite),
                Nom = g.First().Produit?.Nom ?? "",
                Reference = g.First().Produit?.Reference ?? "",
                Categorie = g.First().Produit?.Categorie
            })
            .ToList();

        var topVentes = venteParProduit
            .OrderByDescending(v => v.Quantite)
            .Take(10)
            .Select(v => new ProduitVenteDto(v.ProduitId, v.Nom, v.Reference, v.Categorie, v.Quantite))
            .ToList();

        var flopVentes = venteParProduit
            .OrderBy(v => v.Quantite)
            .Take(10)
            .Select(v => new ProduitVenteDto(v.ProduitId, v.Nom, v.Reference, v.Categorie, v.Quantite))
            .ToList();

        var dto = new BilanPeriodeDto(
            DateOnly.FromDateTime(debut),
            DateOnly.FromDateTime(fin.AddDays(-1)),
            TotalSorties: -sorties.Sum(m => m.Quantite),
            TotalEntrees: entrees.Sum(m => m.Quantite),
            TotalAjustements: ajustements.Count,
            VentesParJour: ventesParJour,
            TopVentes: topVentes,
            FlopVentes: flopVentes
        );

        return Ok(dto);
    }
}
