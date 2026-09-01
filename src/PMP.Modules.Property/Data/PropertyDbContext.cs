using Microsoft.EntityFrameworkCore;
using PMP.Modules.Property.Entities;

namespace PMP.Modules.Property.Data;

/// <summary>
/// Property persistence. Tables live in the shared SQLite database; module
/// ownership is by table name + separate DbContext (modular-monolith boundary).
/// </summary>
public class PropertyDbContext : DbContext
{
    public PropertyDbContext(DbContextOptions<PropertyDbContext> options)
        : base(options)
    {
    }

    public DbSet<ManagedProperty> Properties => Set<ManagedProperty>();

    public DbSet<Building> Buildings => Set<Building>();

    public DbSet<ResidentialUnit> ResidentialUnits => Set<ResidentialUnit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ManagedProperty>(e =>
        {
            e.ToTable("properties");
            e.Property(p => p.Name).HasMaxLength(150).IsRequired();
            e.Property(p => p.Address).HasMaxLength(300).IsRequired();
            e.Property(p => p.City).HasMaxLength(100);
            e.Property(p => p.Description).HasMaxLength(1000);
            e.HasIndex(p => p.ManagerUserId);
            e.HasMany(p => p.Buildings).WithOne(b => b.Property).HasForeignKey(b => b.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Building>(e =>
        {
            e.ToTable("buildings");
            e.Property(b => b.Name).HasMaxLength(150).IsRequired();
            e.Property(b => b.Address).HasMaxLength(300);
            e.HasIndex(b => b.PropertyId);
            e.HasMany(b => b.Units).WithOne(u => u.Building).HasForeignKey(u => u.BuildingId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResidentialUnit>(e =>
        {
            e.ToTable("residential_units");
            e.Property(u => u.UnitNumber).HasMaxLength(30).IsRequired();
            e.Property(u => u.UnitType).HasMaxLength(50);
            e.Property(u => u.Notes).HasMaxLength(1000);
            e.Property(u => u.OperationalStatus).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(u => u.BuildingId);
        });
    }
}
