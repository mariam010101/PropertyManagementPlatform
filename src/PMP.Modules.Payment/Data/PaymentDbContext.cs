using Microsoft.EntityFrameworkCore;
using PMP.Modules.Payment.Entities;
using PMP.Modules.Payment.Enums;

namespace PMP.Modules.Payment.Data;

/// <summary>
/// Payment persistence. Tables live in the shared SQLite database; module ownership
/// is by table name + separate DbContext.
/// </summary>
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Invoice>(e =>
        {
            e.ToTable("invoices");
            // Persist the enum as its name so the stored value stays the readable
            // lifecycle state (Open/PartiallyPaid/Paid/Overdue/Cancelled).
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(i => i.Currency).HasMaxLength(3).IsRequired();
            e.Property(i => i.Purpose).HasMaxLength(30).IsRequired();
            e.Property(i => i.CancellationReason).HasMaxLength(500);
            e.Property(i => i.Amount).HasPrecision(18, 2);
            e.Property(i => i.PaidAmount).HasPrecision(18, 2);
            e.HasIndex(i => i.ResidentUserId);
            e.HasIndex(i => i.LeaseAgreementId);
            e.HasIndex(i => i.PropertyId);
            e.HasIndex(i => i.Status);

            e.HasMany(i => i.Payments)
                .WithOne()
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PaymentTransaction>(e =>
        {
            e.ToTable("payment_transactions");
            e.Property(p => p.TransactionReference).HasMaxLength(50).IsRequired();
            e.Property(p => p.ConfirmationNumber).HasMaxLength(50);
            e.Property(p => p.FailureReason).HasMaxLength(500);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(p => p.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.HasIndex(p => p.TransactionReference).IsUnique();
            e.HasIndex(p => p.ResidentUserId);
        });
    }
}
