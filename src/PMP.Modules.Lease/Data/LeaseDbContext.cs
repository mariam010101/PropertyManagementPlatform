using Microsoft.EntityFrameworkCore;
using PMP.Modules.Lease.Entities;
using PMP.Modules.Lease.Enums;

namespace PMP.Modules.Lease.Data;

/// <summary>
/// Lease persistence. Tables live in the shared SQLite database; module ownership
/// is by table name + separate DbContext.
/// </summary>
public class LeaseDbContext : DbContext
{
    public LeaseDbContext(DbContextOptions<LeaseDbContext> options)
        : base(options)
    {
    }

    public DbSet<LeaseAgreement> LeaseAgreements => Set<LeaseAgreement>();

    public DbSet<LeaseDocument> LeaseDocuments => Set<LeaseDocument>();

    public DbSet<LeaseHistoryEntry> LeaseHistory => Set<LeaseHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<LeaseAgreement>(e =>
        {
            e.ToTable("lease_agreements");
            e.Property(l => l.TerminationComment).HasMaxLength(500);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(l => l.MonthlyRent).HasPrecision(18, 2);
            e.HasIndex(l => l.ResidentUserId);
            e.HasIndex(l => l.UnitId);

            e.HasMany(l => l.Documents)
                .WithOne(d => d.LeaseAgreement)
                .HasForeignKey(d => d.LeaseAgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(l => l.History)
                .WithOne(h => h.LeaseAgreement)
                .HasForeignKey(h => h.LeaseAgreementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LeaseDocument>(e =>
        {
            e.ToTable("lease_documents");
            e.Property(d => d.FileName).HasMaxLength(255).IsRequired();
            e.Property(d => d.ContentType).HasMaxLength(100);
            e.Property(d => d.StoragePath).HasMaxLength(500).IsRequired();
        });

        builder.Entity<LeaseHistoryEntry>(e =>
        {
            e.ToTable("lease_history");
            e.Property(h => h.ChangeType).HasMaxLength(50).IsRequired();
            e.Property(h => h.Description).HasMaxLength(1000);
        });
    }
}
