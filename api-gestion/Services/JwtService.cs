using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api_gestion.Models;
using Microsoft.IdentityModel.Tokens;

namespace api_gestion.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public (string token, DateTime expireLe) GenererToken(Utilisateur utilisateur)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = jwtSection["Key"] ?? "cle-secrete-par-defaut-a-changer-en-production-1234567890";
        var issuer = jwtSection["Issuer"] ?? "api-gestion";
        var audience = jwtSection["Audience"] ?? "gestion-stock";
        var dureeHeures = double.TryParse(jwtSection["DureeValiditeHeures"], out var h) ? h : 12;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, utilisateur.Id.ToString()),
            new(ClaimTypes.Name, utilisateur.Nom),
            new(ClaimTypes.Email, utilisateur.Email),
            new(ClaimTypes.Role, utilisateur.Role.ToString())
        };

        var expireLe = DateTime.UtcNow.AddHours(dureeHeures);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expireLe,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expireLe);
    }
}
