using System.ComponentModel.DataAnnotations;

namespace api_gestion.Models;

public enum RoleUtilisateur
{
    Admin = 0,
    Operateur = 1
}

public class Utilisateur
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Nom { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string MotDePasseHash { get; set; } = string.Empty;

    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Operateur;

    public bool Actif { get; set; } = true;

    public DateTime CreeLe { get; set; } = DateTime.UtcNow;
}
