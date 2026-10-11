using api_gestion.Data;
using api_gestion.DTOs;
using api_gestion.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_gestion.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MouvementsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MouvementsController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/mouvements?produitId=&type=&depuis=&jusqua=&page=1&taille=50
    [HttpGet]
    public async Task<ActionResult<object>> Lister(
        [FromQuery] int? produitId,
        [FromQuery] string? type,
        [FromQuery] DateTime? depuis,
        [FromQuery] DateTime? jusqua,
        [FromQuery] int page = 1,
        [FromQuery] int taille = 50)
    {
        var query = _db.Mouvements.Include(m => m.Produit).AsQueryable();

        if (produitId.HasValue)
            query = query.Where(m => m.ProduitId == produitId);

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<Models.TypeMouvement>(type, true, out var t))
            query = query.Where(m => m.Type == t);

        if (depuis.HasValue)
            query = query.Where(m => m.DateHeure >= depuis.Value);

        if (jusqua.HasValue)
            query = query.Where(m => m.DateHeure <= jusqua.Value);

        query = query.OrderByDescending(m => m.DateHeure);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * taille)
            .Take(taille)
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            taille,
            items = items.Select(m => m.VersDto())
        });
    }
}
