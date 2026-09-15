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

    public DbSet<PaymentInvoice> PaymentInvoices => Set<PaymentInvoice>();

    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();

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

        builder.Entity<PaymentInvoice>(e =>
        {
            e.ToTable("payment_invoices");
            e.Property(pi => pi.InvoiceNumber).HasMaxLength(20).IsRequired();
            e.Property(pi => pi.TransactionReference).HasMaxLength(50).IsRequired();
            e.Property(pi => pi.Currency).HasMaxLength(3).IsRequired();
            e.Property(pi => pi.Purpose).HasMaxLength(30).IsRequired();
            e.Property(pi => pi.ConfirmationNumber).HasMaxLength(50);
            e.Property(pi => pi.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(pi => pi.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(pi => pi.Amount).HasPrecision(18, 2);

            // Idempotency + numbering invariants (AC-07, AC-08): one invoice per payment
            // and never two invoices with the same number.
            e.HasIndex(pi => pi.PaymentTransactionId).IsUnique();
            e.HasIndex(pi => pi.InvoiceNumber).IsUnique();
            e.HasIndex(pi => pi.ResidentUserId);
            e.HasIndex(pi => pi.PropertyId);

            e.HasOne<PaymentTransaction>()
                .WithOne()
                .HasForeignKey<PaymentInvoice>(pi => pi.PaymentTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(pi => pi.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceNumberSequence>(e =>
        {
            e.ToTable("invoice_number_sequences");
            e.HasIndex(s => s.Year).IsUnique();
        });
    }
}
