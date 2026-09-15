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
    /// <summary>Manager/admin raises a payment request with an explicit amount.</summary>
    Task<Result<InvoiceDto>> CreateInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, CreateInvoiceRequest request);

    /// <summary>
    /// Manager/admin raises a rent payment request whose amount comes from the lease
    /// (<c>MonthlyRent</c>) — never hard-coded.
    /// </summary>
    Task<Result<InvoiceDto>> GenerateRentRequestAsync(Guid actorId, IReadOnlyList<string> roles, GenerateRentRequestRequest request);

    /// <summary>Resident: outstanding balance + open requests (FR-PAY-001, BRULE-PAY-006).</summary>
    Task<BalanceDto> GetBalanceAsync(Guid actorId, IReadOnlyList<string> roles);

    /// <summary>
    /// The caller's own payment dashboard: amount currently due, upcoming, overdue,
    /// request statuses/due dates, history and notification-bell alerts.
    /// The caller can only ever see their own obligations (no user id is accepted).
    /// </summary>
    Task<PaymentDashboardDto> GetDashboardAsync(Guid userId);

    /// <summary>Manager/admin/accountant: payment requests within their scope (FR-PAY-005).</summary>
    Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(Guid actorId, IReadOnlyList<string> roles, InvoiceStatus? status);

    /// <summary>
    /// Manager/admin/accountant: which residents owe money, how much, and what is overdue
    /// (FR-PAY-005). Scoped to the manager's properties.
    /// </summary>
    Task<IReadOnlyList<ResidentOutstandingDto>> GetOutstandingByResidentAsync(Guid actorId, IReadOnlyList<string> roles);

    Task<IReadOnlyList<PaymentTransactionDto>> GetPaymentHistoryAsync(Guid actorId, IReadOnlyList<string> roles);

    /// <summary>
    /// Accountant/manager/admin: invoices generated for successful payments, scoped to the
    /// caller's authority (accountant/admin see all; manager sees their properties).
    /// </summary>
    Task<IReadOnlyList<PaymentInvoiceDto>> GetPaymentInvoicesAsync(
        Guid actorId,
        IReadOnlyList<string> roles,
        Guid? residentUserId,
        Guid? propertyId,
        PaymentInvoiceStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to);

    /// <summary>Resident: the caller's own payment invoices (never another resident's).</summary>
    Task<IReadOnlyList<PaymentInvoiceDto>> GetMyPaymentInvoicesAsync(Guid actorId);

    /// <summary>
    /// Single invoice with ownership/scope enforcement. The caller can only read an
    /// invoice that belongs to them or that falls within their financial scope.
    /// </summary>
    Task<Result<PaymentInvoiceDto>> GetPaymentInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId);

    Task<Result<PaymentTransactionDto>> PayInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId, PayInvoiceRequest request);

    /// <summary>Manager/admin cancels an unsettled payment request; the record is retained.</summary>
    Task<Result<InvoiceDto>> CancelInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId, CancelPaymentRequestRequest request);

    Task<FinancialReportDto> GetFinancialReportAsync(Guid actorId, IReadOnlyList<string> roles);

    Task<ReconciliationDto> GetReconciliationAsync(Guid actorId, IReadOnlyList<string> roles);

    /// <summary>
    /// System sweep: mark unsettled requests whose due date has passed as overdue and
    /// notify the resident once. Returns the number of requests now overdue.
    /// </summary>
    Task<int> MarkOverdueAsync();

    /// <summary>System sweep: remind residents of upcoming invoice due dates (FR-PAY-007).</summary>
    Task<int> SendDueDateRemindersAsync();
}

/// <summary>
/// Payment management (BR-005) plus accountant-scoped financial reporting (BR-011).
///
/// <para>The invoice is the payment request/order a resident is required to pay. This
/// service owns the request lifecycle (Open → PartiallyPaid → Paid, with Overdue and
/// Cancelled), determines each resident's current obligation, and records payment
/// attempts — including declined ones — as immutable transactions.</para>
///
/// <para>Payment processing is simulated (ADR-0012); there is no card/bank provider
/// integration yet (tracked as GAP-012). The simulated processor completes a valid
/// charge and declines a charge greater than the outstanding balance.</para>
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

    // ---------------------------------------------------------------- requests

    public async Task<Result<InvoiceDto>> CreateInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, CreateInvoiceRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<InvoiceDto>("Only a property manager or administrator can raise payment requests.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? PaymentCurrency.Default : request.Currency.Trim().ToUpperInvariant();
        if (currency.Length != 3)
        {
            return Result.Fail<InvoiceDto>("Currency must be a 3-letter ISO 4217 code.");
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose) ? PaymentPurpose.Rent : request.Purpose.Trim();
        if (!PaymentPurpose.IsKnown(purpose))
        {
            return Result.Fail<InvoiceDto>($"Unknown payment purpose '{purpose}'.");
        }

        var (lease, unit, scopeError) = await ResolveLeaseScopeAsync(actorId, roles, request.LeaseAgreementId);
        if (scopeError is not null)
        {
            return Result.Fail<InvoiceDto>(scopeError);
        }

        var invoice = new Invoice
        {
            LeaseAgreementId = lease!.Id,
            ResidentUserId = lease.ResidentUserId,
            UnitId = lease.UnitId,
            PropertyId = unit!.PropertyId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            DueDate = request.DueDate,
            Amount = request.Amount,
            Currency = currency,
            Purpose = purpose,
            Status = InvoiceStatus.Open,
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        await NotifyRequestRaisedAsync(invoice);

        return Result.Ok(await ToInvoiceDtoAsync(invoice));
    }

    public async Task<Result<InvoiceDto>> GenerateRentRequestAsync(Guid actorId, IReadOnlyList<string> roles, GenerateRentRequestRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<InvoiceDto>("Only a property manager or administrator can raise payment requests.");
        }

        var (lease, unit, scopeError) = await ResolveLeaseScopeAsync(actorId, roles, request.LeaseAgreementId);
        if (scopeError is not null)
        {
            return Result.Fail<InvoiceDto>(scopeError);
        }

        // The amount is derived from the lease — it is never hard-coded.
        if (lease!.MonthlyRent <= 0)
        {
            return Result.Fail<InvoiceDto>("The lease has no monthly rent configured, so no amount can be derived.");
        }

        var now = DateTimeOffset.UtcNow;
        var periodStart = request.PeriodStart ?? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        var periodEnd = request.PeriodEnd ?? periodStart.AddMonths(1);
        var dueDate = request.DueDate ?? new DateTimeOffset(periodStart.Year, periodStart.Month, 10, 0, 0, 0, periodStart.Offset);

        // When the period/due date were left to the defaults and the derived due date has
        // already passed (e.g. raised after the 10th), roll the whole period forward so the
        // request starts life upcoming rather than instantly overdue.
        if (request.PeriodStart is null && request.PeriodEnd is null && request.DueDate is null && dueDate < now)
        {
            periodStart = periodStart.AddMonths(1);
            periodEnd = periodEnd.AddMonths(1);
            dueDate = dueDate.AddMonths(1);
        }

        var invoice = new Invoice
        {
            LeaseAgreementId = lease.Id,
            ResidentUserId = lease.ResidentUserId,
            UnitId = lease.UnitId,
            PropertyId = unit!.PropertyId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            DueDate = dueDate,
            Amount = lease.MonthlyRent,
            Currency = PaymentCurrency.Default,
            Purpose = PaymentPurpose.Rent,
            Status = InvoiceStatus.Open,
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        await NotifyRequestRaisedAsync(invoice);

        return Result.Ok(await ToInvoiceDtoAsync(invoice));
    }

    // ---------------------------------------------------------------- reads

    public async Task<BalanceDto> GetBalanceAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (!roles.Contains(AppRoles.Resident))
        {
            // Managers/admins/accountants use GetInvoicesAsync / GetDashboardAsync / reports.
            return new BalanceDto();
        }

        var now = DateTimeOffset.UtcNow;
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.ResidentUserId == actorId && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync();

        var dtos = await ToInvoiceDtosAsync(invoices, now);
        var open = dtos.Where(i => i.OutstandingBalance > 0).ToList();

        return new BalanceDto
        {
            TotalOutstanding = open.Sum(i => i.OutstandingBalance),
            OpenInvoiceCount = open.Count,
            Invoices = dtos,
        };
    }

    public async Task<PaymentDashboardDto> GetDashboardAsync(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.ResidentUserId == userId && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync();

        var requests = await ToInvoiceDtosAsync(invoices, now);
        var outstanding = requests.Where(r => r.OutstandingBalance > 0).ToList();
        var overdue = outstanding.Where(r => r.Status == InvoiceStatus.Overdue).ToList();
        var upcoming = outstanding.Where(r => r.Status != InvoiceStatus.Overdue).ToList();

        var transactions = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.ResidentUserId == userId)
            .ToListAsync();
        var history = MapTransactions(transactions);

        return new PaymentDashboardDto
        {
            AmountCurrentlyDue = outstanding.Sum(r => r.OutstandingBalance),
            OverdueAmount = overdue.Sum(r => r.OutstandingBalance),
            UpcomingAmount = upcoming.Sum(r => r.OutstandingBalance),
            OutstandingRequestCount = outstanding.Count,
            OverdueRequestCount = overdue.Count,
            NextDueDate = upcoming.OrderBy(r => r.DueDate).FirstOrDefault()?.DueDate,
            Currency = requests.Select(r => r.Currency).FirstOrDefault() ?? PaymentCurrency.Default,
            Requests = requests.OrderBy(r => r.Status == InvoiceStatus.Overdue ? 0 : 1).ThenBy(r => r.DueDate).ToList(),
            History = history,
            Alerts = BuildAlerts(outstanding, history, now),
        };
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(Guid actorId, IReadOnlyList<string> roles, InvoiceStatus? status)
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

        var now = DateTimeOffset.UtcNow;
        var rows = await query.ToListAsync();
        var dtos = await ToInvoiceDtosAsync(rows, now);

        if (status is not null)
        {
            dtos = dtos.Where(i => i.Status == status).ToList();
        }

        return dtos.OrderByDescending(i => i.DueDate).ToList();
    }

    public async Task<IReadOnlyList<ResidentOutstandingDto>> GetOutstandingByResidentAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (!IsFinancialRole(roles))
        {
            return Array.Empty<ResidentOutstandingDto>();
        }

        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.PaidAmount < i.Amount);

        if (!roles.Contains(AppRoles.Administrator) && !roles.Contains(AppRoles.Accountant))
        {
            var managedIds = await ManagedPropertyIdsAsync(actorId);
            query = query.Where(i => managedIds.Contains(i.PropertyId));
        }

        var invoices = await query.ToListAsync();
        if (invoices.Count == 0)
        {
            return Array.Empty<ResidentOutstandingDto>();
        }

        var now = DateTimeOffset.UtcNow;

        var residentIds = invoices.Select(i => i.ResidentUserId).Distinct().ToList();
        var profiles = await _residentDb.ResidentProfiles.AsNoTracking()
            .Where(r => residentIds.Contains(r.UserId) && !r.IsDeleted)
            .Select(r => new { r.UserId, r.FirstName, r.LastName, r.Email })
            .ToListAsync();
        var profileLookup = profiles.ToDictionary(p => p.UserId);

        var unitIds = invoices.Select(i => i.UnitId).Distinct().ToList();
        var units = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UnitNumber, PropertyName = u.Building!.Property!.Name })
            .ToListAsync();
        var unitLookup = units.ToDictionary(u => u.Id);

        return invoices
            .GroupBy(i => i.ResidentUserId)
            .Select(group =>
            {
                var first = group.OrderByDescending(i => i.CreatedAt).First();
                profileLookup.TryGetValue(group.Key, out var profile);
                unitLookup.TryGetValue(first.UnitId, out var unit);
                var overdue = group.Where(i => i.GetEffectiveStatus(now) == InvoiceStatus.Overdue).ToList();

                return new ResidentOutstandingDto
                {
                    ResidentUserId = group.Key,
                    ResidentName = profile is null ? string.Empty : $"{profile.FirstName} {profile.LastName}".Trim(),
                    Email = profile?.Email ?? string.Empty,
                    UnitNumber = unit?.UnitNumber ?? string.Empty,
                    PropertyName = unit?.PropertyName ?? string.Empty,
                    TotalOutstanding = group.Sum(i => i.OutstandingBalance),
                    OverdueAmount = overdue.Sum(i => i.OutstandingBalance),
                    OpenRequestCount = group.Count(),
                    OldestDueDate = group.Min(i => i.DueDate),
                    HasOverdue = overdue.Count > 0,
                    Currency = first.Currency,
                };
            })
            .OrderByDescending(r => r.OverdueAmount)
            .ThenByDescending(r => r.TotalOutstanding)
            .ToList();
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

        var rows = await query.ToListAsync();
        return MapTransactions(rows);
    }

    // ---------------------------------------------------------------- payment invoices

    public async Task<IReadOnlyList<PaymentInvoiceDto>> GetPaymentInvoicesAsync(
        Guid actorId,
        IReadOnlyList<string> roles,
        Guid? residentUserId,
        Guid? propertyId,
        PaymentInvoiceStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!IsFinancialRole(roles))
        {
            return Array.Empty<PaymentInvoiceDto>();
        }

        var query = _db.PaymentInvoices.AsNoTracking();

        // Scope: accountant/admin read everything; a manager only reads their properties.
        if (!roles.Contains(AppRoles.Administrator) && !roles.Contains(AppRoles.Accountant))
        {
            var managedIds = await ManagedPropertyIdsAsync(actorId);
            query = query.Where(pi => managedIds.Contains(pi.PropertyId));
        }

        if (residentUserId is not null)
        {
            query = query.Where(pi => pi.ResidentUserId == residentUserId);
        }

        if (propertyId is not null)
        {
            query = query.Where(pi => pi.PropertyId == propertyId);
        }

        if (status is not null)
        {
            query = query.Where(pi => pi.Status == status);
        }

        // SQLite cannot translate DateTimeOffset range comparisons: narrow in SQL by the
        // enum where possible, then apply the date window in memory (ADR-0012).
        var rows = await query.ToListAsync();

        IEnumerable<PaymentInvoice> filtered = rows;
        if (from is not null)
        {
            filtered = filtered.Where(pi => pi.PaymentDate >= from);
        }

        if (to is not null)
        {
            filtered = filtered.Where(pi => pi.PaymentDate <= to);
        }

        return await ToPaymentInvoiceDtosAsync(filtered.OrderByDescending(pi => pi.PaymentDate).ToList());
    }

    public async Task<IReadOnlyList<PaymentInvoiceDto>> GetMyPaymentInvoicesAsync(Guid actorId)
    {
        // SQLite cannot translate DateTimeOffset in ORDER BY: load, then order in memory.
        var rows = await _db.PaymentInvoices.AsNoTracking()
            .Where(pi => pi.ResidentUserId == actorId)
            .ToListAsync();

        return await ToPaymentInvoiceDtosAsync(rows.OrderByDescending(pi => pi.PaymentDate).ToList());
    }

    public async Task<Result<PaymentInvoiceDto>> GetPaymentInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId)
    {
        var invoice = await _db.PaymentInvoices.AsNoTracking()
            .SingleOrDefaultAsync(pi => pi.Id == invoiceId);

        if (invoice is null)
        {
            return Result.Fail<PaymentInvoiceDto>("Invoice not found.");
        }

        if (roles.Contains(AppRoles.Administrator) || roles.Contains(AppRoles.Accountant))
        {
            return Result.Ok(await ToPaymentInvoiceDtoAsync(invoice));
        }

        if (roles.Contains(AppRoles.Resident))
        {
            if (invoice.ResidentUserId != actorId)
            {
                // Hide existence to prevent IDOR enumeration (FR-AUTH-007).
                return Result.Fail<PaymentInvoiceDto>("Invoice not found.");
            }

            return Result.Ok(await ToPaymentInvoiceDtoAsync(invoice));
        }

        if (roles.Contains(AppRoles.PropertyManager))
        {
            var managedIds = await ManagedPropertyIdsAsync(actorId);
            if (!managedIds.Contains(invoice.PropertyId))
            {
                return Result.Fail<PaymentInvoiceDto>("Invoice not found.");
            }

            return Result.Ok(await ToPaymentInvoiceDtoAsync(invoice));
        }

        return Result.Fail<PaymentInvoiceDto>("Invoice not found.");
    }

    // ---------------------------------------------------------------- writes

    public async Task<Result<PaymentTransactionDto>> PayInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId, PayInvoiceRequest request)
    {
        if (!roles.Contains(AppRoles.Resident) && !roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<PaymentTransactionDto>("You are not allowed to make payments.");
        }

        var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice is null)
        {
            return Result.Fail<PaymentTransactionDto>("Payment request not found.");
        }

        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
        {
            return Result.Fail<PaymentTransactionDto>("This payment request cannot be paid.");
        }

        if (invoice.ResidentUserId != actorId &&
            !roles.Contains(AppRoles.Administrator) &&
            !roles.Contains(AppRoles.PropertyManager))
        {
            return Result.Fail<PaymentTransactionDto>("You can only pay your own payment requests.");
        }

        if (roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            var managedIds = await ManagedPropertyIdsAsync(actorId);
            if (!managedIds.Contains(invoice.PropertyId))
            {
                return Result.Fail<PaymentTransactionDto>("You do not manage this payment request's property.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var outstanding = invoice.OutstandingBalance;

        // Simulated processor declines a charge above the outstanding balance. The failed
        // attempt is still recorded so the resident can see what happened (FAILED handling).
        if (request.Amount > outstanding)
        {
            const string reason = "The amount exceeds the outstanding balance.";
            await RecordFailedAttemptAsync(invoice, request, reason);
            await _communication.SendUserNotificationAsync(
                invoice.ResidentUserId,
                "Payment failed",
                $"Your payment of {Money(request.Amount, invoice.Currency)} for the {invoice.Purpose} request was declined: {reason}",
                "PaymentFailed",
                NotificationChannel.InApp);
            return Result.Fail<PaymentTransactionDto>(reason);
        }

        // Unique transaction reference (BRULE-PAY-002).
        var transaction = new PaymentTransaction
        {
            TransactionReference = $"PAY-{Guid.NewGuid():N}".ToUpperInvariant(),
            InvoiceId = invoice.Id,
            ResidentUserId = invoice.ResidentUserId,
            Amount = request.Amount,
            Method = request.Method,
            Status = PaymentStatus.Completed,
            ConfirmationNumber = $"CNF-{Guid.NewGuid():N}".ToUpperInvariant(),
            PaidAt = now,
        };

        _db.PaymentTransactions.Add(transaction);

        // The successful payment produces its invoice receipt (Payment 1 → Invoice 1).
        // Idempotency is enforced by the unique index on PaymentTransactionId.
        var paymentInvoice = new PaymentInvoice
        {
            InvoiceNumber = await NextInvoiceNumberAsync(),
            PaymentTransactionId = transaction.Id,
            TransactionReference = transaction.TransactionReference,
            InvoiceId = invoice.Id,
            ResidentUserId = invoice.ResidentUserId,
            UnitId = invoice.UnitId,
            PropertyId = invoice.PropertyId,
            Amount = transaction.Amount,
            Currency = invoice.Currency,
            Purpose = invoice.Purpose,
            Method = transaction.Method,
            Status = PaymentInvoiceStatus.Issued,
            PaymentDate = now,
            ConfirmationNumber = transaction.ConfirmationNumber,
        };
        _db.PaymentInvoices.Add(paymentInvoice);

        // Update request totals (BRULE-PAY-004: status reflects state).
        invoice.PaidAmount += transaction.Amount;
        if (Math.Abs(invoice.PaidAmount - invoice.Amount) < 0.01m)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = now;
        }
        else if (invoice.PaidAmount > 0)
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }

        await _db.SaveChangesAsync();

        // Confirmation to the resident (BRULE-PAY-005).
        await _communication.SendUserNotificationAsync(
            invoice.ResidentUserId,
            "Payment received",
            $"Your payment of {Money(transaction.Amount, invoice.Currency)} for the {invoice.Purpose} request was successful. Confirmation {transaction.ConfirmationNumber}.",
            "Payment",
            NotificationChannel.InApp);

        return Result.Ok(MapTransaction(transaction, invoice.Currency));
    }

    public async Task<Result<InvoiceDto>> CancelInvoiceAsync(Guid actorId, IReadOnlyList<string> roles, Guid invoiceId, CancelPaymentRequestRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<InvoiceDto>("Only a property manager or administrator can cancel a payment request.");
        }

        var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice is null)
        {
            return Result.Fail<InvoiceDto>("Payment request not found.");
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return Result.Fail<InvoiceDto>("A paid payment request cannot be cancelled.");
        }

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            return Result.Fail<InvoiceDto>("The payment request is already cancelled.");
        }

        if (roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            var managedIds = await ManagedPropertyIdsAsync(actorId);
            if (!managedIds.Contains(invoice.PropertyId))
            {
                return Result.Fail<InvoiceDto>("You do not manage this payment request's property.");
            }
        }

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelledAt = DateTimeOffset.UtcNow;
        invoice.CancellationReason = request.Reason;
        await _db.SaveChangesAsync();

        await _communication.SendUserNotificationAsync(
            invoice.ResidentUserId,
            "Payment request cancelled",
            $"The {invoice.Purpose} payment request for {Money(invoice.OutstandingBalance, invoice.Currency)} was cancelled.",
            "PaymentCancelled",
            NotificationChannel.InApp);

        return Result.Ok(await ToInvoiceDtoAsync(invoice));
    }

    // ---------------------------------------------------------------- reporting

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

        var now = DateTimeOffset.UtcNow;
        var invoices = await _db.Invoices.AsNoTracking().ToListAsync();
        var openRequests = invoices
            .Where(i => i.GetEffectiveStatus(now) is InvoiceStatus.Open or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue)
            .ToList();
        var overdueRequests = invoices
            .Where(i => i.GetEffectiveStatus(now) == InvoiceStatus.Overdue)
            .ToList();

        return new FinancialReportDto
        {
            TotalCollected = completed.Sum(p => p.Amount),
            TotalOutstanding = openRequests.Sum(i => i.OutstandingBalance),
            CompletedPayments = completed.Count,
            FailedPayments = failed,
            OpenInvoices = openRequests.Count,
            PaidInvoices = invoices.Count(i => i.Status == InvoiceStatus.Paid),
            OverdueInvoices = overdueRequests.Count,
            OverdueTotal = overdueRequests.Sum(i => i.OutstandingBalance),
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
            .Where(i => i.Status != InvoiceStatus.Cancelled)
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

    // ---------------------------------------------------------------- sweeps

    public async Task<int> MarkOverdueAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // SQLite cannot translate DateTimeOffset range comparisons: narrow the status
        // predicate in SQL, then apply the due-date window in memory (ADR-0012).
        var candidates = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid)
            .ToListAsync();

        var overdue = candidates.Where(i => i.DueDate < now).ToList();

        foreach (var invoice in overdue)
        {
            invoice.Status = InvoiceStatus.Overdue;

            if (invoice.OverdueNotifiedAt is null)
            {
                invoice.OverdueNotifiedAt = now;
                await _communication.SendUserNotificationAsync(
                    invoice.ResidentUserId,
                    "Payment overdue",
                    $"Your {invoice.Purpose} payment of {Money(invoice.OutstandingBalance, invoice.Currency)} is overdue (due {invoice.DueDate:yyyy-MM-dd}).",
                    "PaymentOverdue",
                    NotificationChannel.InApp);
            }
        }

        if (overdue.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return overdue.Count;
    }

    public async Task<int> SendDueDateRemindersAsync()
    {
        // Persist the overdue transition first so status is accurate before reminding.
        await MarkOverdueAsync();

        var now = DateTimeOffset.UtcNow;
        var horizon = now.AddDays(3);

        var candidates = await _db.Invoices
            .Where(i => (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue) &&
                        i.DueDateReminderSentAt == null)
            .ToListAsync();

        var due = candidates.Where(i => i.DueDate >= now && i.DueDate <= horizon).ToList();

        foreach (var invoice in due)
        {
            invoice.DueDateReminderSentAt = DateTimeOffset.UtcNow;
            await _communication.SendUserNotificationAsync(
                invoice.ResidentUserId,
                "Payment due soon",
                $"{invoice.Purpose} payment of {Money(invoice.OutstandingBalance, invoice.Currency)} is due on {invoice.DueDate:yyyy-MM-dd}.",
                "Payment",
                NotificationChannel.InApp);
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return due.Count;
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsFinancialRole(IReadOnlyList<string> roles) =>
        roles.Contains(AppRoles.Administrator) ||
        roles.Contains(AppRoles.PropertyManager) ||
        roles.Contains(AppRoles.Accountant);

    private async Task<List<Guid>> ManagedPropertyIdsAsync(Guid actorId) =>
        await _propertyDb.Properties.AsNoTracking()
            .Where(p => p.ManagerUserId == actorId)
            .Select(p => p.Id)
            .ToListAsync();

    /// <summary>
    /// Resolves a lease and its unit/property, enforcing that an active lease exists and
    /// that a manager may only act on their own properties.
    /// </summary>
    private async Task<(LeaseAgreementSnapshot? Lease, UnitScope? Unit, string? Error)> ResolveLeaseScopeAsync(
        Guid actorId,
        IReadOnlyList<string> roles,
        Guid leaseAgreementId)
    {
        var lease = await _leaseDb.LeaseAgreements.AsNoTracking()
            .Where(l => l.Id == leaseAgreementId)
            .Select(l => new LeaseAgreementSnapshot(l.Id, l.ResidentUserId, l.UnitId, l.MonthlyRent, l.Status))
            .SingleOrDefaultAsync();

        if (lease is null)
        {
            return (null, null, "Lease not found.");
        }

        if (lease.Status != LeaseStatus.Active)
        {
            return (null, null, "Payment requests can only be raised for active leases.");
        }

        var unit = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Where(u => u.Id == lease.UnitId)
            .Select(u => new UnitScope(u.Id, u.Building!.PropertyId, u.Building!.Property!.ManagerUserId))
            .SingleOrDefaultAsync();

        if (unit is null)
        {
            return (null, null, "Lease unit not found.");
        }

        if (!roles.Contains(AppRoles.Administrator) && unit.ManagerUserId != actorId)
        {
            return (null, null, "You do not have access to this lease's property.");
        }

        return (lease, unit, null);
    }

    private async Task RecordFailedAttemptAsync(Invoice invoice, PayInvoiceRequest request, string reason)
    {
        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionReference = $"PAY-{Guid.NewGuid():N}".ToUpperInvariant(),
            InvoiceId = invoice.Id,
            ResidentUserId = invoice.ResidentUserId,
            Amount = request.Amount,
            Method = request.Method,
            Status = PaymentStatus.Failed,
            FailureReason = reason,
        });

        await _db.SaveChangesAsync();
    }

    private async Task NotifyRequestRaisedAsync(Invoice invoice)
    {
        // Don't notify for requests already in the past when they were raised.
        var title = invoice.DueDate < DateTimeOffset.UtcNow ? "Payment overdue" : "New payment request";
        var body = $"{invoice.Purpose} payment of {Money(invoice.Amount, invoice.Currency)} is due {invoice.DueDate:yyyy-MM-dd}.";

        await _communication.SendUserNotificationAsync(
            invoice.ResidentUserId,
            title,
            body,
            "PaymentRequest",
            NotificationChannel.InApp);
    }

    private async Task<InvoiceDto> ToInvoiceDtoAsync(Invoice invoice)
    {
        var dtos = await ToInvoiceDtosAsync([invoice], DateTimeOffset.UtcNow);
        return dtos[0];
    }

    private async Task<IReadOnlyList<InvoiceDto>> ToInvoiceDtosAsync(IEnumerable<Invoice> invoices, DateTimeOffset now)
    {
        var list = invoices.ToList();
        if (list.Count == 0)
        {
            return Array.Empty<InvoiceDto>();
        }

        var unitIds = list.Select(i => i.UnitId).Distinct().ToList();
        var units = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UnitNumber, PropertyName = u.Building!.Property!.Name })
            .ToListAsync();
        var unitLookup = units.ToDictionary(u => u.Id);

        var residentIds = list.Select(i => i.ResidentUserId).Distinct().ToList();
        var profiles = await _residentDb.ResidentProfiles.AsNoTracking()
            .Where(r => residentIds.Contains(r.UserId) && !r.IsDeleted)
            .Select(r => new { r.UserId, r.FirstName, r.LastName })
            .ToListAsync();
        var profileLookup = profiles.ToDictionary(p => p.UserId);

        return list.Select(invoice =>
        {
            unitLookup.TryGetValue(invoice.UnitId, out var unit);
            profileLookup.TryGetValue(invoice.ResidentUserId, out var profile);

            return new InvoiceDto
            {
                Id = invoice.Id,
                LeaseAgreementId = invoice.LeaseAgreementId,
                ResidentUserId = invoice.ResidentUserId,
                ResidentName = profile is null ? string.Empty : $"{profile.FirstName} {profile.LastName}".Trim(),
                UnitId = invoice.UnitId,
                UnitNumber = unit?.UnitNumber ?? string.Empty,
                PropertyId = invoice.PropertyId,
                PropertyName = unit?.PropertyName ?? string.Empty,
                PeriodStart = invoice.PeriodStart,
                PeriodEnd = invoice.PeriodEnd,
                DueDate = invoice.DueDate,
                Amount = invoice.Amount,
                PaidAmount = invoice.PaidAmount,
                Currency = invoice.Currency,
                Purpose = invoice.Purpose,
                Status = invoice.GetEffectiveStatus(now),
                PaidAt = invoice.PaidAt,
                CreatedAt = invoice.CreatedAt,
                CancelledAt = invoice.CancelledAt,
            };
        }).ToList();
    }

    /// <summary>
    /// Issues the next deterministic invoice number for the current year from the per-year
    /// sequence table (<c>INV-{year}-{n:000000}</c>). This is a dedicated counter — not
    /// <c>COUNT(invoices)+1</c> — and the unique index on
    /// <see cref="PaymentInvoice.InvoiceNumber"/> is the hard backstop that no two invoices
    /// can share a number even under concurrent writes.
    /// </summary>
    private async Task<string> NextInvoiceNumberAsync()
    {
        var year = DateTimeOffset.UtcNow.Year;

        var sequence = await _db.InvoiceNumberSequences.SingleOrDefaultAsync(s => s.Year == year);
        if (sequence is null)
        {
            sequence = new InvoiceNumberSequence { Year = year, LastNumber = 0 };
            _db.InvoiceNumberSequences.Add(sequence);
        }

        sequence.LastNumber += 1;
        return $"INV-{year}-{sequence.LastNumber:D6}";
    }

    private async Task<PaymentInvoiceDto> ToPaymentInvoiceDtoAsync(PaymentInvoice invoice)
    {
        var dtos = await ToPaymentInvoiceDtosAsync([invoice]);
        return dtos[0];
    }

    private async Task<IReadOnlyList<PaymentInvoiceDto>> ToPaymentInvoiceDtosAsync(IReadOnlyList<PaymentInvoice> invoices)
    {
        if (invoices.Count == 0)
        {
            return Array.Empty<PaymentInvoiceDto>();
        }

        var unitIds = invoices.Select(i => i.UnitId).Distinct().ToList();
        var units = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UnitNumber, PropertyName = u.Building!.Property!.Name })
            .ToListAsync();
        var unitLookup = units.ToDictionary(u => u.Id);

        var residentIds = invoices.Select(i => i.ResidentUserId).Distinct().ToList();
        var profiles = await _residentDb.ResidentProfiles.AsNoTracking()
            .Where(r => residentIds.Contains(r.UserId) && !r.IsDeleted)
            .Select(r => new { r.UserId, r.FirstName, r.LastName })
            .ToListAsync();
        var profileLookup = profiles.ToDictionary(p => p.UserId);

        return invoices.Select(invoice =>
        {
            unitLookup.TryGetValue(invoice.UnitId, out var unit);
            profileLookup.TryGetValue(invoice.ResidentUserId, out var profile);

            return new PaymentInvoiceDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                PaymentTransactionId = invoice.PaymentTransactionId,
                TransactionReference = invoice.TransactionReference,
                InvoiceId = invoice.InvoiceId,
                ResidentUserId = invoice.ResidentUserId,
                ResidentName = profile is null ? string.Empty : $"{profile.FirstName} {profile.LastName}".Trim(),
                UnitId = invoice.UnitId,
                UnitNumber = unit?.UnitNumber ?? string.Empty,
                PropertyId = invoice.PropertyId,
                PropertyName = unit?.PropertyName ?? string.Empty,
                Amount = invoice.Amount,
                Currency = invoice.Currency,
                Purpose = invoice.Purpose,
                Method = invoice.Method,
                Status = invoice.Status,
                PaymentDate = invoice.PaymentDate,
                ConfirmationNumber = invoice.ConfirmationNumber,
                IssuedAt = invoice.CreatedAt,
            };
        }).ToList();
    }

    private static IReadOnlyList<PaymentTransactionDto> MapTransactions(IEnumerable<PaymentTransaction> transactions) =>
        transactions
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => MapTransaction(t, PaymentCurrency.Default))
            .ToList();

    private static PaymentTransactionDto MapTransaction(PaymentTransaction p, string currency) => new()
    {
        Id = p.Id,
        TransactionReference = p.TransactionReference,
        InvoiceId = p.InvoiceId,
        ResidentUserId = p.ResidentUserId,
        Amount = p.Amount,
        Currency = currency,
        Status = p.Status,
        Method = p.Method,
        PaidAt = p.PaidAt,
        ConfirmationNumber = p.ConfirmationNumber,
        FailureReason = p.FailureReason,
        CreatedAt = p.CreatedAt,
    };

    /// <summary>
    /// Turns outstanding requests and recent failed attempts into the attention items the
    /// dashboard bell shows: overdue > failed > due-soon > new.
    /// </summary>
    private static IReadOnlyList<PaymentAlertDto> BuildAlerts(
        IReadOnlyList<InvoiceDto> outstanding,
        IReadOnlyList<PaymentTransactionDto> history,
        DateTimeOffset now)
    {
        var alerts = new List<PaymentAlertDto>();
        var dueSoonHorizon = now.AddDays(3);
        var newRequestWindow = now.AddDays(-7);

        foreach (var request in outstanding)
        {
            if (request.Status == InvoiceStatus.Overdue)
            {
                alerts.Add(new PaymentAlertDto
                {
                    Type = PaymentAlertType.Overdue,
                    Severity = "critical",
                    Title = "Payment overdue",
                    Message = $"{request.Purpose} payment of {Money(request.OutstandingBalance, request.Currency)} was due {request.DueDate:yyyy-MM-dd}.",
                    Amount = request.OutstandingBalance,
                    Currency = request.Currency,
                    DueDate = request.DueDate,
                    RequestId = request.Id,
                });
            }
            else if (request.DueDate <= dueSoonHorizon)
            {
                alerts.Add(new PaymentAlertDto
                {
                    Type = PaymentAlertType.DueSoon,
                    Severity = "warning",
                    Title = "Payment due soon",
                    Message = $"{request.Purpose} payment of {Money(request.OutstandingBalance, request.Currency)} is due {request.DueDate:yyyy-MM-dd}.",
                    Amount = request.OutstandingBalance,
                    Currency = request.Currency,
                    DueDate = request.DueDate,
                    RequestId = request.Id,
                });
            }
            else if (request.CreatedAt >= newRequestWindow)
            {
                alerts.Add(new PaymentAlertDto
                {
                    Type = PaymentAlertType.NewRequest,
                    Severity = "info",
                    Title = "New payment request",
                    Message = $"{request.Purpose} payment of {Money(request.OutstandingBalance, request.Currency)} is due {request.DueDate:yyyy-MM-dd}.",
                    Amount = request.OutstandingBalance,
                    Currency = request.Currency,
                    DueDate = request.DueDate,
                    RequestId = request.Id,
                });
            }
        }

        // One failed-payment alert per request (the most recent declined attempt).
        var failedWindow = now.AddDays(-30);
        var recentFailures = history
            .Where(t => t.Status == PaymentStatus.Failed && t.CreatedAt >= failedWindow)
            .GroupBy(t => t.InvoiceId)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).First());

        foreach (var failure in recentFailures)
        {
            alerts.Add(new PaymentAlertDto
            {
                Type = PaymentAlertType.FailedPayment,
                Severity = "critical",
                Title = "Payment failed",
                Message = $"A payment of {Money(failure.Amount, failure.Currency)} was declined{(failure.FailureReason is null ? "." : $": {failure.FailureReason}")}",
                Amount = failure.Amount,
                Currency = failure.Currency,
                RequestId = failure.InvoiceId,
            });
        }

        return alerts
            .OrderBy(a => SeverityRank(a.Severity))
            .ThenBy(a => a.DueDate ?? DateTimeOffset.MaxValue)
            .ToList();
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 0,
        "warning" => 1,
        _ => 2,
    };

    private static string Money(decimal amount, string currency) => $"{currency} {amount:0.00}";

    private sealed record LeaseAgreementSnapshot(Guid Id, Guid ResidentUserId, Guid UnitId, decimal MonthlyRent, LeaseStatus Status);

    private sealed record UnitScope(Guid Id, Guid PropertyId, Guid ManagerUserId);
}
