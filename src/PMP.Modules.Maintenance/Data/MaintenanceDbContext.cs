using Microsoft.EntityFrameworkCore;
using PMP.Modules.Maintenance.Entities;
using PMP.Modules.Maintenance.Enums;

namespace PMP.Modules.Maintenance.Data;

/// <summary>
/// Maintenance persistence. Owns the <c>maintenance</c> schema.
/// </summary>
public class MaintenanceDbContext : DbContext
{
    public MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

    public DbSet<MaintenanceAttachment> MaintenanceAttachments => Set<MaintenanceAttachment>();

    public DbSet<MaintenanceHistoryEntry> MaintenanceHistory => Set<MaintenanceHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("maintenance");

        builder.Entity<MaintenanceRequest>(e =>
        {
            e.ToTable("maintenance_requests");
            e.Property(r => r.Title).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(2000);
            e.Property(r => r.CancellationReason).HasMaxLength(500);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(r => r.Priority).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.HasIndex(r => r.RequestedByResidentId);
            e.HasIndex(r => r.UnitId);
            e.HasIndex(r => r.AssignedToUserId);
            e.HasIndex(r => r.Status);

            e.HasMany(r => r.Attachments)
                .WithOne(a => a.Request)
                .HasForeignKey(a => a.MaintenanceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(r => r.History)
                .WithOne(h => h.Request)
                .HasForeignKey(h => h.MaintenanceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MaintenanceAttachment>(e =>
        {
            e.ToTable("maintenance_attachments");
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.ContentType).HasMaxLength(100);
            e.Property(a => a.StoragePath).HasMaxLength(500).IsRequired();
        });

        builder.Entity<MaintenanceHistoryEntry>(e =>
        {
            e.ToTable("maintenance_history");
            e.Property(h => h.Comment).HasMaxLength(1000);
            e.Property(h => h.FromStatus).HasConversion<string?>().HasMaxLength(30);
            e.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.HasIndex(h => h.MaintenanceRequestId);
        });
    }
}
