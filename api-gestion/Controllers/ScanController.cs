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

    // Fenêtre de sécurité anti double-scan (lecture répétée involontaire du même article)
    private static readonly TimeSpan FenetreAntiDoublon = TimeSpan.FromSeconds(3);

    public ScanController(AppDbContext db, IHubContext<StockHub> hub)
    {
        _db = db;
        _hub = hub;
    }

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

        // Anti doublon : un même produit scanné deux fois en moins de 3 secondes est ignoré
        var dernierScan = await _db.Mouvements
            .Where(m => m.ProduitId == produit.Id && m.Type == TypeMouvement.SortieScan)
            .OrderByDescending(m => m.DateHeure)
            .FirstOrDefaultAsync();

        if (dernierScan != null && DateTime.UtcNow - dernierScan.DateHeure < FenetreAntiDoublon)
        {
            return Ok(new ScanResultDto(
                true,
                "Scan ignoré (doublon détecté, même article scanné il y a moins de 3 secondes).",
                produit.VersDto(),
                produit.EnAlerte,
                produit.EnRupture,
                true));
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

        await _hub.Clients.All.SendAsync("stockMisAJour", produitDto);

        if (produitDto.EnRupture)
        {
            await _hub.Clients.All.SendAsync("alerteRupture", produitDto);
        }
        else if (produitDto.EnAlerte)
        {
            await _hub.Clients.All.SendAsync("alerteSeuil", produitDto);
        }

        var message = apres < 0
            ? $"Attention : stock négatif pour « {produit.Nom} »."
            : $"« {produit.Nom} » — nouveau stock : {apres}.";

        return Ok(new ScanResultDto(true, message, produitDto, produitDto.EnAlerte, produitDto.EnRupture, false));
    }
}
