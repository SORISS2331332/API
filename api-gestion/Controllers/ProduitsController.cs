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
public class ProduitsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<StockHub> _hub;
    private readonly BarcodeService _barcodeService;

    public ProduitsController(AppDbContext db, IHubContext<StockHub> hub, BarcodeService barcodeService)
    {
        _db = db;
        _hub = hub;
        _barcodeService = barcodeService;
    }

    // GET api/produits?recherche=...&categorie=...&statut=alerte|rupture|ok
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProduitDto>>> Lister(
        [FromQuery] string? recherche,
        [FromQuery] string? categorie,
        [FromQuery] string? statut)
    {
        var query = _db.Produits.AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim().ToLower();
            query = query.Where(p =>
                p.Nom.ToLower().Contains(r) ||
                p.Reference.ToLower().Contains(r) ||
                p.CodeBarre.ToLower().Contains(r) ||
                (p.Categorie != null && p.Categorie.ToLower().Contains(r)));
        }

        if (!string.IsNullOrWhiteSpace(categorie))
        {
            query = query.Where(p => p.Categorie == categorie);
        }

        var produits = await query.OrderBy(p => p.Nom).ToListAsync();

        var resultat = produits.Select(p => p.VersDto());

        resultat = statut switch
        {
            "rupture" => resultat.Where(p => p.EnRupture),
            "alerte" => resultat.Where(p => p.EnAlerte || p.EnRupture),
            "ok" => resultat.Where(p => !p.EnAlerte && !p.EnRupture),
            _ => resultat
        };

        return Ok(resultat);
    }

    // GET api/produits/generer-code-barre : génère un nouveau code-barres EAN-13 unique
    // (ne modifie pas la base — à utiliser pour pré-remplir le formulaire de création/édition)
    [HttpGet("generer-code-barre")]
    public async Task<ActionResult<object>> GenererCodeBarre()
    {
        var code = await _barcodeService.GenererCodeUniqueAsync();
        return Ok(new { codeBarre = code });
    }

    // GET api/produits/{id}/etiquette-pdf : étiquette PDF prête à imprimer pour un seul produit
    [HttpGet("{id:int}/etiquette-pdf")]
    public async Task<IActionResult> EtiquettePdf(int id)
    {
        var produit = await _db.Produits.FindAsync(id);
        if (produit == null) return NotFound();

        var image = BarcodeService.GenererImagePng(produit.CodeBarre);
        var pdf = EtiquettePdfService.GenererEtiquetteUnique(produit, image);

        var nomFichier = $"etiquette-{produit.Reference}.pdf".Replace(" ", "-").ToLowerInvariant();
        return File(pdf, "application/pdf", nomFichier);
    }

    // GET api/produits/etiquettes-pdf?ids=1,2,3&categorie=...&statut=...
    // Planche PDF regroupant les étiquettes code-barres de plusieurs produits (impression en masse).
    // Sans paramètre, génère la planche pour tous les produits actifs.
    [HttpGet("etiquettes-pdf")]
    public async Task<IActionResult> EtiquettesPdf(
        [FromQuery] string? ids,
        [FromQuery] string? categorie,
        [FromQuery] string? statut)
    {
        var query = _db.Produits.Where(p => p.Actif).AsQueryable();

        if (!string.IsNullOrWhiteSpace(ids))
        {
            var idsListe = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToHashSet();

            if (idsListe.Count > 0)
                query = query.Where(p => idsListe.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(categorie))
            query = query.Where(p => p.Categorie == categorie);

        var produits = await query.OrderBy(p => p.Nom).ToListAsync();

        if (!string.IsNullOrWhiteSpace(statut))
        {
            produits = statut switch
            {
                "rupture" => produits.Where(p => p.EnRupture).ToList(),
                "alerte" => produits.Where(p => p.EnAlerte || p.EnRupture).ToList(),
                "ok" => produits.Where(p => !p.EnAlerte && !p.EnRupture).ToList(),
                _ => produits
            };
        }

        if (produits.Count == 0)
            return BadRequest(new { message = "Aucun produit ne correspond à cette sélection." });

        var pdf = EtiquettePdfService.GenererPlanchesEtiquettes(produits, p => BarcodeService.GenererImagePng(p.CodeBarre));

        var nomFichier = $"etiquettes-codes-barres-{DateTime.Now:yyyy-MM-dd}.pdf";
        return File(pdf, "application/pdf", nomFichier);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> Categories()
    {
        var categories = await _db.Produits
            .Where(p => p.Categorie != null && p.Categorie != "")
            .Select(p => p.Categorie!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProduitDto>> Obtenir(int id)
    {
        var produit = await _db.Produits.FindAsync(id);
        if (produit == null) return NotFound();
        return Ok(produit.VersDto());
    }

    [HttpGet("code-barre/{codeBarre}")]
    public async Task<ActionResult<ProduitDto>> ObtenirParCodeBarre(string codeBarre)
    {
        var produit = await _db.Produits.FirstOrDefaultAsync(p => p.CodeBarre == codeBarre);
        if (produit == null) return NotFound();
        return Ok(produit.VersDto());
    }

    [HttpPost]
    public async Task<ActionResult<ProduitDto>> Creer(CreerProduitDto dto)
    {
        if (await _db.Produits.AnyAsync(p => p.CodeBarre == dto.CodeBarre))
            return Conflict(new { message = "Un produit avec ce code-barres existe déjà." });

        if (await _db.Produits.AnyAsync(p => p.Reference == dto.Reference))
            return Conflict(new { message = "Un produit avec cette référence existe déjà." });

        var produit = new Produit
        {
            Nom = dto.Nom,
            Reference = dto.Reference,
            CodeBarre = dto.CodeBarre,
            Categorie = dto.Categorie,
            QuantiteStock = dto.QuantiteStock,
            SeuilAlerte = dto.SeuilAlerte,
            PrixUnitaire = dto.PrixUnitaire,
            Description = dto.Description,
            CreeLe = DateTime.UtcNow,
            MisAJourLe = DateTime.UtcNow
        };

        _db.Produits.Add(produit);

        _db.Mouvements.Add(new MouvementStock
        {
            Produit = produit,
            Type = TypeMouvement.CreationInitiale,
            Quantite = dto.QuantiteStock,
            QuantiteAvant = 0,
            QuantiteApres = dto.QuantiteStock,
            Commentaire = "Création de la fiche article",
            Source = "gestion",
            DateHeure = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("stockMisAJour", produit.VersDto());

        return CreatedAtAction(nameof(Obtenir), new { id = produit.Id }, produit.VersDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProduitDto>> Modifier(int id, ModifierProduitDto dto)
    {
        var produit = await _db.Produits.FindAsync(id);
        if (produit == null) return NotFound();

        if (await _db.Produits.AnyAsync(p => p.Id != id && p.CodeBarre == dto.CodeBarre))
            return Conflict(new { message = "Un autre produit utilise déjà ce code-barres." });

        if (await _db.Produits.AnyAsync(p => p.Id != id && p.Reference == dto.Reference))
            return Conflict(new { message = "Un autre produit utilise déjà cette référence." });

        produit.Nom = dto.Nom;
        produit.Reference = dto.Reference;
        produit.CodeBarre = dto.CodeBarre;
        produit.Categorie = dto.Categorie;
        produit.SeuilAlerte = dto.SeuilAlerte;
        produit.PrixUnitaire = dto.PrixUnitaire;
        produit.Description = dto.Description;
        produit.Actif = dto.Actif;
        produit.MisAJourLe = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("stockMisAJour", produit.VersDto());

        return Ok(produit.VersDto());
    }

    // Ajustement manuel de la quantité (approvisionnement / correction d'inventaire)
    [HttpPost("{id:int}/ajuster")]
    public async Task<ActionResult<ProduitDto>> AjusterStock(int id, AjusterStockDto dto)
    {
        var produit = await _db.Produits.FindAsync(id);
        if (produit == null) return NotFound();

        var avant = produit.QuantiteStock;
        var apres = dto.Quantite;
        var delta = apres - avant;

        if (delta == 0)
            return Ok(produit.VersDto());

        produit.QuantiteStock = apres;
        produit.MisAJourLe = DateTime.UtcNow;

        _db.Mouvements.Add(new MouvementStock
        {
            ProduitId = produit.Id,
            Type = TypeMouvement.AjustementManuel,
            Quantite = delta,
            QuantiteAvant = avant,
            QuantiteApres = apres,
            Commentaire = dto.Commentaire ?? "Ajustement manuel",
            Source = "gestion",
            DateHeure = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var produitDto = produit.VersDto();
        await _hub.Clients.All.SendAsync("stockMisAJour", produitDto);

        return Ok(produitDto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Supprimer(int id)
    {
        var produit = await _db.Produits.FindAsync(id);
        if (produit == null) return NotFound();

        _db.Produits.Remove(produit);
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("produitSupprime", id);

        return NoContent();
    }
}
