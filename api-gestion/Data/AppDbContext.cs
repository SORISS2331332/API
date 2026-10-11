using Microsoft.EntityFrameworkCore;
using api_gestion.Models;

namespace api_gestion.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<MouvementStock> Mouvements => Set<MouvementStock>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Produit>(e =>
        {
            e.HasIndex(p => p.CodeBarre).IsUnique();
            e.HasIndex(p => p.Reference).IsUnique();
        });

        modelBuilder.Entity<Utilisateur>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<MouvementStock>(e =>
        {
            e.HasOne(m => m.Produit)
                .WithMany(p => p.Mouvements)
                .HasForeignKey(m => m.ProduitId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(m => m.DateHeure);
        });
    }
}
