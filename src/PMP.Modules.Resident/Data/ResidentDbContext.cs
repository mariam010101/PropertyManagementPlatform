using Microsoft.EntityFrameworkCore;
using PMP.Modules.Resident.Entities;

namespace PMP.Modules.Resident.Data;

/// <summary>
/// Resident persistence. Owns the <c>resident</c> schema.
/// </summary>
public class ResidentDbContext : DbContext
{
    public ResidentDbContext(DbContextOptions<ResidentDbContext> options)
        : base(options)
    {
    }

    public DbSet<ResidentProfile> ResidentProfiles => Set<ResidentProfile>();

    public DbSet<ResidentUnit> ResidentUnits => Set<ResidentUnit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("resident");

        builder.Entity<ResidentProfile>(e =>
        {
            e.ToTable("resident_profiles");
            e.Property(r => r.FirstName).HasMaxLength(100).IsRequired();
            e.Property(r => r.LastName).HasMaxLength(100).IsRequired();
            e.Property(r => r.Email).HasMaxLength(256).IsRequired();
            e.Property(r => r.PhoneNumber).HasMaxLength(32);
            e.HasIndex(r => r.UserId).IsUnique();
            e.HasIndex(r => r.Email);
            e.HasMany(r => r.UnitAssociations).WithOne(ru => ru.ResidentProfile).HasForeignKey(ru => ru.ResidentProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResidentUnit>(e =>
        {
            e.ToTable("resident_units");
            e.HasKey(ru => ru.Id);
            e.HasIndex(ru => ru.UnitId);
            e.HasIndex(ru => new { ru.ResidentProfileId, ru.UnitId });
            e.Property(ru => ru.MoveInDate).IsRequired();
        });
    }
}
