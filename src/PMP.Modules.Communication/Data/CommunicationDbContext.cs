using Microsoft.EntityFrameworkCore;
using PMP.Modules.Communication.Entities;
using PMP.Modules.Communication.Enums;

namespace PMP.Modules.Communication.Data;

/// <summary>
/// Communication persistence (notifications + announcements). Tables live in the
/// shared SQLite database; module ownership is by table name + separate DbContext.
/// </summary>
public class CommunicationDbContext : DbContext
{
    public CommunicationDbContext(DbContextOptions<CommunicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(n => n.Title).HasMaxLength(200).IsRequired();
            e.Property(n => n.Body).HasMaxLength(4000).IsRequired();
            e.Property(n => n.EventType).HasMaxLength(50).IsRequired();
            e.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(n => n.DeliveryStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(n => n.RecipientUserId);
            e.HasIndex(n => n.IsRead);
        });

        builder.Entity<Announcement>(e =>
        {
            e.ToTable("announcements");
            e.Property(a => a.Title).HasMaxLength(200).IsRequired();
            e.Property(a => a.Body).HasMaxLength(4000).IsRequired();
            e.HasIndex(a => a.PropertyId);
        });
    }
}
