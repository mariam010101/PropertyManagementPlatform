using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PMP.Modules.Payment.Data;
using PMP.Modules.Payment.Entities;
using PMP.Modules.Payment.Enums;

namespace PMP.Tests;

/// <summary>
/// AC-07/AC-08: the database itself guarantees idempotency and unique invoice numbers.
/// These use a real SQLite engine (not the in-memory provider) because the in-memory
/// provider does not enforce relational unique indexes or foreign keys. Each insert is
/// made through a fresh <see cref="DbContext"/> so the change tracker cannot short-circuit
/// the invariant — only the database constraint is being exercised.
/// </summary>
public class PaymentInvoiceConstraintTests
{
    [Fact]
    public async Task Database_RejectsSecondInvoiceForTheSamePayment()
    {
        await using var db = await SqliteTestDb.CreateAsync();

        var paymentId = Guid.NewGuid();
        var requestId = await SeedParentsAsync(db, paymentId);

        await InsertAsync(db, NewInvoice("INV-2026-000001", paymentId, requestId));

        var second = db.NewContext();
        await using (second)
        {
            second.PaymentInvoices.Add(NewInvoice("INV-2026-000002", paymentId, requestId));
            await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Database_RejectsDuplicateInvoiceNumber()
    {
        await using var db = await SqliteTestDb.CreateAsync();

        var firstPayment = Guid.NewGuid();
        var firstRequest = await SeedParentsAsync(db, firstPayment);
        await InsertAsync(db, NewInvoice("INV-2026-000001", firstPayment, firstRequest));

        var secondPayment = Guid.NewGuid();
        var secondRequest = await SeedParentsAsync(db, secondPayment);

        var second = db.NewContext();
        await using (second)
        {
            second.PaymentInvoices.Add(NewInvoice("INV-2026-000001", secondPayment, secondRequest));
            await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        }
    }

    private static async Task<Guid> SeedParentsAsync(SqliteTestDb db, Guid paymentTransactionId)
    {
        await using var context = db.NewContext();

        var requestId = Guid.NewGuid();
        var residentId = Guid.NewGuid();

        context.Invoices.Add(new Invoice
        {
            Id = requestId,
            LeaseAgreementId = Guid.NewGuid(),
            ResidentUserId = residentId,
            UnitId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            PeriodStart = DateTimeOffset.UtcNow.AddDays(-30),
            PeriodEnd = DateTimeOffset.UtcNow,
            DueDate = DateTimeOffset.UtcNow.AddDays(10),
            Amount = 100m,
            Status = InvoiceStatus.Open,
        });

        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = paymentTransactionId,
            TransactionReference = $"PAY-{paymentTransactionId:N}".ToUpperInvariant(),
            InvoiceId = requestId,
            ResidentUserId = residentId,
            Amount = 100m,
            Status = PaymentStatus.Completed,
            Method = PaymentMethod.Card,
            PaidAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
        return requestId;
    }

    private static async Task InsertAsync(SqliteTestDb db, PaymentInvoice invoice)
    {
        await using var context = db.NewContext();
        context.PaymentInvoices.Add(invoice);
        await context.SaveChangesAsync();
    }

    private static PaymentInvoice NewInvoice(string number, Guid paymentTransactionId, Guid requestId) => new()
    {
        InvoiceNumber = number,
        PaymentTransactionId = paymentTransactionId,
        TransactionReference = $"PAY-{paymentTransactionId:N}".ToUpperInvariant(),
        InvoiceId = requestId,
        ResidentUserId = Guid.NewGuid(),
        UnitId = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        Amount = 100m,
        Currency = PaymentCurrency.Default,
        Purpose = PaymentPurpose.Rent,
        Method = PaymentMethod.Card,
        Status = PaymentInvoiceStatus.Issued,
        PaymentDate = DateTimeOffset.UtcNow,
    };

    private sealed class SqliteTestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<PaymentDbContext> _options;

        private SqliteTestDb(SqliteConnection connection, DbContextOptions<PaymentDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public PaymentDbContext NewContext() => new(_options);

        public static async Task<SqliteTestDb> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PaymentDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new PaymentDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new SqliteTestDb(connection, options);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
