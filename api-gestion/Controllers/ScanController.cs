using api_gestion.Data;
using api_gestion.DTOs;
using api_gestion.Hubs;
using api_gestion.Models;
using api_gestion.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace api_gestion.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScanController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<StockHub> _hub;

    // Un retrait n'est possible que sur un scan récent du même utilisateur
    private static readonly TimeSpan FenetreRetrait = TimeSpan.FromHours(24);

    public ScanController(AppDbContext db, IHubContext<StockHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    /// <summary>
    /// Chaque appel décrémente le stock d'une unité : un client qui achète 3 fois le même
    /// article déclenche 3 scans. Aucun filtre anti-doublon n'est appliqué côté serveur.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ScanResultDto>> Scanner(ScanRequestDto dto)
    {
        var codeBarre = dto.CodeBarre?.Trim();

        if (string.IsNullOrWhiteSpace(codeBarre))
        {
            return Ok(new ScanResultDto(false, "Code-barres vide.", null, false, false, false));
        }

        var produit = await _db.Produits.FirstOrDefaultAsync(p => p.CodeBarre == codeBarre);

        if (produit == null)
        {
            return Ok(new ScanResultDto(false, "Produit introuvable pour ce code-barres.", null, false, false, false));
        }

        if (!produit.Actif)
        {
            return Ok(new ScanResultDto(false, "Ce produit est désactivé.", produit.VersDto(), false, false, false));
        }

        var avant = produit.QuantiteStock;
        var apres = avant - 1;

        produit.QuantiteStock = apres;
        produit.MisAJourLe = DateTime.UtcNow;

        _db.Mouvements.Add(new MouvementStock
        {
            ProduitId = produit.Id,
            Type = TypeMouvement.SortieScan,
            Quantite = -1,
            QuantiteAvant = avant,
            QuantiteApres = apres,
            Commentaire = "Sortie via scan",
            Utilisateur = dto.Utilisateur,
            Source = "scan",
            DateHeure = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var produitDto = produit.VersDto();
        await DiffuserAsync(produitDto, notifierAlertes: true);

        var message = apres < 0
            ? $"Attention : stock négatif pour « {produit.Nom} »."
            : $"« {produit.Nom} » — nouveau stock : {apres}.";

        return Ok(new ScanResultDto(true, message, produitDto, produitDto.EnAlerte, produitDto.EnRupture, false));
    }

    /// <summary>
    /// Annule un scan fait par erreur : remet une unité en stock.
    /// </summary>
    [HttpPost("retirer")]
    public async Task<ActionResult<ScanResultDto>> Retirer(ScanRequestDto dto)
    {
        var codeBarre = dto.CodeBarre?.Trim();

        if (string.IsNullOrWhiteSpace(codeBarre))
        {
            return Ok(new ScanResultDto(false, "Code-barres vide.", null, false, false, false));
        }

        var produit = await _db.Produits.FirstOrDefaultAsync(p => p.CodeBarre == codeBarre);

        if (produit == null)
        {
            return Ok(new ScanResultDto(false, "Produit introuvable pour ce code-barres.", null, false, false, false));
        }

        var depuis = DateTime.UtcNow - FenetreRetrait;
        var scansNets = await _db.Mouvements
            .Where(m => m.ProduitId == produit.Id
                        && m.Utilisateur == dto.Utilisateur
                        && m.DateHeure >= depuis
                        && (m.Type == TypeMouvement.SortieScan || m.Type == TypeMouvement.AnnulationScan))
            .SumAsync(m => (int?)-m.Quantite) ?? 0;

        if (scansNets <= 0)
        {
            return Ok(new ScanResultDto(false, "Aucun scan récent à annuler pour cet article.", produit.VersDto(), false, false, false));
        }

        var avant = produit.QuantiteStock;
        var apres = avant + 1;

        produit.QuantiteStock = apres;
        produit.MisAJourLe = DateTime.UtcNow;

        _db.Mouvements.Add(new MouvementStock
        {
            ProduitId = produit.Id,
            Type = TypeMouvement.AnnulationScan,
            Quantite = 1,
            QuantiteAvant = avant,
            QuantiteApres = apres,
            Commentaire = "Retrait d'un scan (annulation)",
            Utilisateur = dto.Utilisateur,
            Source = "scan",
            DateHeure = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var produitDto = produit.VersDto();
        await DiffuserAsync(produitDto, notifierAlertes: false);

        return Ok(new ScanResultDto(true, $"« {produit.Nom} » retiré — nouveau stock : {apres}.", produitDto, produitDto.EnAlerte, produitDto.EnRupture, false));
    }

    private async Task DiffuserAsync(ProduitDto produitDto, bool notifierAlertes)
    {
        await _hub.Clients.All.SendAsync("stockMisAJour", produitDto);

        if (!notifierAlertes) return;

        if (produitDto.EnRupture)
        {
            await _hub.Clients.All.SendAsync("alerteRupture", produitDto);
        }
        else if (produitDto.EnAlerte)
        {
            await _hub.Clients.All.SendAsync("alerteSeuil", produitDto);
        }
    }
}
