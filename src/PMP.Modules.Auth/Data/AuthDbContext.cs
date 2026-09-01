using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PMP.Modules.Auth.Entities;

namespace PMP.Modules.Auth.Data;

/// <summary>
/// Identity + auth persistence. Owns the <c>auth</c> schema within the
/// modular-monolith database (schema-per-module boundary).
/// </summary>
public class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("auth");

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
        });

        builder.Entity<IdentityRole<Guid>>(e =>
        {
            e.ToTable("roles");
        });

        builder.Entity<IdentityUserRole<Guid>>(e => e.ToTable("user_roles"));
        builder.Entity<IdentityUserClaim<Guid>>(e => e.ToTable("user_claims"));
        builder.Entity<IdentityUserLogin<Guid>>(e => e.ToTable("user_logins"));
        builder.Entity<IdentityRoleClaim<Guid>>(e => e.ToTable("role_claims"));
        builder.Entity<IdentityUserToken<Guid>>(e => e.ToTable("user_tokens"));

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Token).IsUnique();
            e.Property(t => t.Token).HasMaxLength(128).IsRequired();
            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuthEvent>(e =>
        {
            e.ToTable("auth_events");
            e.HasKey(a => a.Id);
            e.Property(a => a.EventType).HasMaxLength(64).IsRequired();
            e.Property(a => a.Email).HasMaxLength(256);
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.HasIndex(a => a.OccurredAt);
        });
    }
}
