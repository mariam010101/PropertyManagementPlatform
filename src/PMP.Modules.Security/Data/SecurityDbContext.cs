using Microsoft.EntityFrameworkCore;
using PMP.Modules.Security.Entities;
using PMP.Modules.Security.Enums;

namespace PMP.Modules.Security.Data;

/// <summary>
/// Security & visitor persistence. Tables live in the shared SQLite database; module
/// ownership is by table name + separate DbContext.
/// </summary>
public class SecurityDbContext : DbContext
{
    public SecurityDbContext(DbContextOptions<SecurityDbContext> options)
        : base(options)
    {
    }

    public DbSet<VisitorRecord> Visitors => Set<VisitorRecord>();

    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<VisitorRecord>(e =>
        {
            e.ToTable("visitors");
            e.Property(v => v.FirstName).HasMaxLength(100).IsRequired();
            e.Property(v => v.LastName).HasMaxLength(100).IsRequired();
            e.Property(v => v.PhoneNumber).HasMaxLength(32);
            e.Property(v => v.UnitNumber).HasMaxLength(30);
            e.Property(v => v.Notes).HasMaxLength(1000);
            e.Property(v => v.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(v => v.PropertyId);
            e.HasIndex(v => v.Status);
        });

        builder.Entity<AccessGrant>(e =>
        {
            e.ToTable("access_grants");
            e.Property(a => a.TargetType).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(a => a.SubjectType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(a => a.PropertyId);
            e.HasIndex(a => a.SubjectUserId);
            e.HasIndex(a => a.VisitorId);
        });
    }
}
