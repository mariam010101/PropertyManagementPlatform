using Microsoft.EntityFrameworkCore;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Communication.Services;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Payment.Contracts;
using PMP.Modules.Payment.Data;
using PMP.Modules.Payment.Entities;
using PMP.Modules.Payment.Enums;
using PMP.Modules.Property.Data;
using PMP.Modules.Resident.Data;
using PMP.Shared.Common;

namespace PMP.Modules.Payment.Services;

public interface IPaymentService
{
    Task<Result<InvoiceDto>> CreateInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, CreateInvoiceRequest request);

    Task<BalanceDto> GetBalanceAsync(Guid actorId, IReadOnlyList<string> roles);

    Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(Guid actorId, IReadOnlyList<string> roles, string? status);

    Task<IReadOnlyList<PaymentTransactionDto>> GetPaymentHistoryAsync(Guid actorId, IReadOnlyList<string> roles);

    Task<Result<PaymentTransactionDto>> PayInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId, PayInvoiceRequest request);

    Task<FinancialReportDto> GetFinancialReportAsync(Guid actorId, IReadOnlyList<string> roles);

    Task<ReconciliationDto> GetReconciliationAsync(Guid actorId, IReadOnlyList<string> roles);

    /// <summary>System sweep: remind residents of upcoming/overdue invoice due dates (FR-PAY-007).</summary>
    Task<int> SendDueDateRemindersAsync();
}

/// <summary>
/// Payment management (BR-005) plus accountant-scoped financial reporting (BR-011).
/// Invoice outstanding balances, electronic payment recording with unique transaction
/// references and confirmations, immutable history, and financial reports/reconciliation.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _db;
    private readonly LeaseDbContext _leaseDb;
    private readonly PropertyDbContext _propertyDb;
    private readonly ResidentDbContext _residentDb;
    private readonly ICommunicationService _communication;

    public PaymentService(
        PaymentDbContext db,
        LeaseDbContext leaseDb,
        PropertyDbContext propertyDb,
        ResidentDbContext residentDb,
        ICommunicationService communication)
    {
        _db = db;
        _leaseDb = leaseDb;
        _propertyDb = propertyDb;
        _residentDb = residentDb;
        _communication = communication;
    }

    public async Task<Result<InvoiceDto>> CreateInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, CreateInvoiceRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<InvoiceDto>("Only a property manager or administrator can create invoices.");
        }

        var lease = await _leaseDb.LeaseAgreements.AsNoTracking().SingleOrDefaultAsync(l => l.Id == request.LeaseAgreementId);
        if (lease is null)
        {
            return Result.Fail<InvoiceDto>("Lease not found.");
        }

        if (lease.Status != LeaseStatus.Active)
        {
            return Result.Fail<InvoiceDto>("Invoices can only be created for active leases.");
        }

        var unit = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Include(u => u.Building)
            .ThenInclude(b => b!.Property)
            .SingleOrDefaultAsync(u => u.Id == lease.UnitId);
        if (unit is null)
        {
            return Result.Fail<InvoiceDto>("Lease unit not found.");
        }

        if (!roles.Contains(AppRoles.Administrator) && unit.Building!.Property!.ManagerUserId != actorId)
        {
            return Result.Fail<InvoiceDto>("You do not have access to this lease's property.");
        }

        var invoice = new Invoice
        {
            LeaseAgreementId = lease.Id,
            ResidentUserId = lease.ResidentUserId,
            UnitId = lease.UnitId,
            PropertyId = unit.Building.PropertyId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            DueDate = request.DueDate,
            Amount = request.Amount,
            Status = "Open",
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        return Result.Ok(await ToInvoiceDtoAsync(invoice));
    }

    public async Task<BalanceDto> GetBalanceAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (!roles.Contains(AppRoles.Resident))
        {
            // Managers/admins/accountants use GetInvoicesAsync or reports.
            return new BalanceDto();
        }

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.ResidentUserId == actorId && i.Status != "Voided")
            .ToListAsync();

        var dtos = await ToInvoiceDtosAsync(invoices);
        var open = dtos.Where(i => i.OutstandingBalance > 0).ToList();

        return new BalanceDto
        {
            TotalOutstanding = open.Sum(i => i.OutstandingBalance),
            OpenInvoiceCount = open.Count,
            Invoices = dtos,
        };
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(Guid actorId, IReadOnlyList<string> roles, string? status)
    {
        var query = _db.Invoices.AsNoTracking();

        if (!roles.Contains(AppRoles.Administrator) && !roles.Contains(AppRoles.Accountant))
        {
            if (roles.Contains(AppRoles.Resident))
            {
                query = query.Where(i => i.ResidentUserId == actorId);
            }
            else if (roles.Contains(AppRoles.PropertyManager))
            {
                var managedIds = await ManagedPropertyIdsAsync(actorId);
                query = query.Where(i => managedIds.Contains(i.PropertyId));
            }
            else
            {
                return Array.Empty<InvoiceDto>();
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        var rows = await query.ToListAsync();
        var dtos = await ToInvoiceDtosAsync(rows);
        return dtos.OrderByDescending(i => i.DueDate).ToList();
    }

    public async Task<IReadOnlyList<PaymentTransactionDto>> GetPaymentHistoryAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        var query = _db.PaymentTransactions.AsNoTracking();

        if (!roles.Contains(AppRoles.Administrator) && !roles.Contains(AppRoles.Accountant))
        {
            if (roles.Contains(AppRoles.Resident))
            {
                query = query.Where(p => p.ResidentUserId == actorId);
            }
            else if (roles.Contains(AppRoles.PropertyManager))
            {
                var managedIds = await ManagedPropertyIdsAsync(actorId);
                var invoiceIds = await _db.Invoices.AsNoTracking()
                    .Where(i => managedIds.Contains(i.PropertyId))
                    .Select(i => i.Id)
                    .ToListAsync();
                query = query.Where(p => invoiceIds.Contains(p.InvoiceId));
            }
            else
            {
                return Array.Empty<PaymentTransactionDto>();
            }
        }

        var rows = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return rows.Select(p => new PaymentTransactionDto
        {
            Id = p.Id,
            TransactionReference = p.TransactionReference,
            InvoiceId = p.InvoiceId,
            ResidentUserId = p.ResidentUserId,
            Amount = p.Amount,
            Status = p.Status,
            Method = p.Method,
            PaidAt = p.PaidAt,
            ConfirmationNumber = p.ConfirmationNumber,
        }).ToList();
    }

    public async Task<Result<PaymentTransactionDto>> PayInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId, PayInvoiceRequest request)
    {
        if (!roles.Contains(AppRoles.Resident) && !roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<PaymentTransactionDto>("You are not allowed to make payments.");
        }

        var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice is null)
        {
            return Result.Fail<PaymentTransactionDto>("Invoice not found.");
        }

        if (invoice.Status is "Paid" or "Voided")
        {
            return Result.Fail<PaymentTransactionDto>("This invoice cannot be paid.");
        }

        if (invoice.ResidentUserId != actorId &&
            !roles.Contains(AppRoles.Administrator) &&
            !roles.Contains(AppRoles.PropertyManager))
        {
            return Result.Fail<PaymentTransactionDto>("You can only pay your own invoices.");
        }

        if (roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            var managedIds = await ManagedPropertyIdsAsync(actorId);
            if (!managedIds.Contains(invoice.PropertyId))
            {
                return Result.Fail<PaymentTransactionDto>("You do not manage this invoice's property.");
            }
        }

        var outstanding = invoice.Amount - invoice.PaidAmount;
        if (request.Amount > outstanding)
        {
            return Result.Fail<PaymentTransactionDto>("Payment exceeds the outstanding balance.");
        }

        // Unique transaction reference (BRULE-PAY-002).
        var reference = $"PAY-{Guid.NewGuid():N}".ToUpperInvariant();
        var transaction = new PaymentTransaction
        {
            TransactionReference = reference,
            InvoiceId = invoice.Id,
            ResidentUserId = invoice.ResidentUserId,
            Amount = request.Amount,
            Method = request.Method,
            Status = PaymentStatus.Pending,
        };

        _db.PaymentTransactions.Add(transaction);

        // Simulated electronic payment: in a real deployment this would call a PSP.
        if (request.Method == PaymentMethod.Manual)
        {
            transaction.Status = PaymentStatus.Completed;
            transaction.ConfirmationNumber = $"CNF-{Guid.NewGuid():N}".ToUpperInvariant();
            transaction.PaidAt = DateTimeOffset.UtcNow;
        }
        else
        {
            transaction.Status = PaymentStatus.Completed;
            transaction.ConfirmationNumber = $"CNF-{Guid.NewGuid():N}".ToUpperInvariant();
            transaction.PaidAt = DateTimeOffset.UtcNow;
        }

        // Update invoice totals (BRULE-PAY-004: status reflects state).
        invoice.PaidAmount += transaction.Amount;
        if (Math.Abs(invoice.PaidAmount - invoice.Amount) < 0.01m)
        {
            invoice.Status = "Paid";
            invoice.PaidAt = transaction.PaidAt;
        }
        else if (invoice.PaidAmount > 0)
        {
            invoice.Status = "PartiallyPaid";
        }

        await _db.SaveChangesAsync();

        // Confirmation to the resident (BRULE-PAY-005).
        await _communication.SendUserNotificationAsync(
            invoice.ResidentUserId,
            "Payment received",
            $"Your payment of {transaction.Amount:C} for invoice {invoice.Id:N} was successful. Confirmation {transaction.ConfirmationNumber}.",
            "Payment",
            NotificationChannel.InApp);

        return Result.Ok(new PaymentTransactionDto
        {
            Id = transaction.Id,
            TransactionReference = transaction.TransactionReference,
            InvoiceId = transaction.InvoiceId,
            ResidentUserId = transaction.ResidentUserId,
            Amount = transaction.Amount,
            Status = transaction.Status,
            Method = transaction.Method,
            PaidAt = transaction.PaidAt,
            ConfirmationNumber = transaction.ConfirmationNumber,
        });
    }

    public async Task<FinancialReportDto> GetFinancialReportAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (!IsFinancialRole(roles))
        {
            return new FinancialReportDto();
        }

        var completed = await _db.PaymentTransactions.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToListAsync();

        var failed = await _db.PaymentTransactions.AsNoTracking()
            .CountAsync(p => p.Status == PaymentStatus.Failed);

        var invoices = await _db.Invoices.AsNoTracking().ToListAsync();

        return new FinancialReportDto
        {
            TotalCollected = completed.Sum(p => p.Amount),
            TotalOutstanding = invoices.Where(i => i.Status is "Open" or "PartiallyPaid" or "Overdue").Sum(i => i.Amount - i.PaidAmount),
            CompletedPayments = completed.Count,
            FailedPayments = failed,
            OpenInvoices = invoices.Count(i => i.Status is "Open" or "PartiallyPaid" or "Overdue"),
            PaidInvoices = invoices.Count(i => i.Status == "Paid"),
            OverdueTotal = invoices.Where(i => i.Status == "Overdue").Sum(i => i.Amount - i.PaidAmount),
        };
    }

    public async Task<ReconciliationDto> GetReconciliationAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (!IsFinancialRole(roles))
        {
            return new ReconciliationDto();
        }

        var completed = await _db.PaymentTransactions.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToListAsync();

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status != "Voided")
            .ToListAsync();

        var collected = completed.Sum(p => p.Amount);
        var expected = invoices.Sum(i => i.Amount);

        return new ReconciliationDto
        {
            ExpectedFromInvoices = expected,
            Collected = collected,
            Difference = collected - expected,
            CompletedTransactions = completed.Count,
            RefundedTransactions = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Refunded),
            FailedTransactions = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Failed),
        };
    }

    public async Task<int> SendDueDateRemindersAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var horizon = now.AddDays(3);

        // SQLite cannot translate DateTimeOffset range comparisons: narrow on the
        // non-temporal predicate in SQL, then apply the due-date window in memory.
        var candidates = await _db.Invoices
            .Where(i => (i.Status == "Open" || i.Status == "PartiallyPaid" || i.Status == "Overdue") &&
                        i.DueDateReminderSentAt == null)
            .ToListAsync();

        var due = candidates.Where(i => i.DueDate >= now && i.DueDate <= horizon).ToList();

        foreach (var invoice in due)
        {
            invoice.DueDateReminderSentAt = DateTimeOffset.UtcNow;
            await _communication.SendUserNotificationAsync(
                invoice.ResidentUserId,
                "Payment due soon",
                $"Invoice due on {invoice.DueDate:yyyy-MM-dd} with outstanding balance {invoice.Amount - invoice.PaidAmount:C}.",
                "Payment",
                NotificationChannel.InApp);
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return due.Count;
    }

    // ---- helpers ----

    private static bool IsFinancialRole(IReadOnlyList<string> roles) =>
        roles.Contains(AppRoles.Administrator) ||
        roles.Contains(AppRoles.PropertyManager) ||
        roles.Contains(AppRoles.Accountant);

    private async Task<List<Guid>> ManagedPropertyIdsAsync(Guid actorId) =>
        await _propertyDb.Properties.AsNoTracking()
            .Where(p => p.ManagerUserId == actorId)
            .Select(p => p.Id)
            .ToListAsync();

    private async Task<InvoiceDto> ToInvoiceDtoAsync(Invoice invoice)
    {
        var unit = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Include(u => u.Building)
            .ThenInclude(b => b!.Property)
            .SingleOrDefaultAsync(u => u.Id == invoice.UnitId);

        var profile = await _residentDb.ResidentProfiles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == invoice.ResidentUserId && !r.IsDeleted);

        return new InvoiceDto
        {
            Id = invoice.Id,
            LeaseAgreementId = invoice.LeaseAgreementId,
            ResidentUserId = invoice.ResidentUserId,
            ResidentName = profile is null ? string.Empty : $"{profile.FirstName} {profile.LastName}".Trim(),
            UnitId = invoice.UnitId,
            UnitNumber = unit?.UnitNumber ?? string.Empty,
            PropertyId = invoice.PropertyId,
            PropertyName = unit?.Building?.Property?.Name ?? string.Empty,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd,
            DueDate = invoice.DueDate,
            Amount = invoice.Amount,
            PaidAmount = invoice.PaidAmount,
            Status = invoice.Status,
            PaidAt = invoice.PaidAt,
        };
    }

    private async Task<IReadOnlyList<InvoiceDto>> ToInvoiceDtosAsync(IEnumerable<Invoice> invoices)
    {
        var list = invoices.ToList();
        var result = new List<InvoiceDto>();
        foreach (var invoice in list)
        {
            result.Add(await ToInvoiceDtoAsync(invoice));
        }

        return result;
    }
}
