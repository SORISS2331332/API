using api_gestion.Data;
using api_gestion.DTOs;
using api_gestion.Models;
using api_gestion.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_gestion.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var utilisateur = await _db.Utilisateurs
            .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower() && u.Actif);

        if (utilisateur == null || !BCrypt.Net.BCrypt.Verify(dto.MotDePasse, utilisateur.MotDePasseHash))
        {
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });
        }

        var (token, expireLe) = _jwt.GenererToken(utilisateur);

        return Ok(new LoginResponseDto(token, utilisateur.Nom, utilisateur.Email, utilisateur.Role.ToString(), expireLe));
    }
}
