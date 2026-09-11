namespace PMP.Modules.Payment.Enums;

/// <summary>
/// Lifecycle of a payment request/order (the invoice is the request a resident is
/// required to pay — FR-PAY-001..007, BR-005).
///
/// <para>Explicit transitions:</para>
/// <list type="bullet">
///   <item><see cref="Open"/> (the request has been raised and is awaiting payment) →
///     <see cref="PartiallyPaid"/> → <see cref="Paid"/>.</item>
///   <item><see cref="Open"/><see cref="PartiallyPaid"/> → <see cref="Overdue"/> once the
///     due date passes; a late payment can still move the request to
///     <see cref="PartiallyPaid"/>/<see cref="Paid"/>.</item>
///   <item><see cref="Open"/><see cref="PartiallyPaid"/>/<see cref="Overdue"/> →
///     <see cref="Cancelled"/> when a manager/admin voids the request.</item>
/// </list>
///
/// <para><see cref="Paid"/> and <see cref="Cancelled"/> requests are never deleted, so the
/// resident can always see what was expected and what happened (BRULE-PAY-003).</para>
///
/// <para>A failed *payment attempt* is recorded separately on
/// <see cref="PaymentStatus.Failed"/> against the transaction; the request itself stays
/// <see cref="Open"/>/<see cref="PartiallyPaid"/>/<see cref="Overdue"/>.</para>
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Payment request raised; nothing paid yet.</summary>
    Open = 0,

    /// <summary>Some money received; balance remains.</summary>
    PartiallyPaid = 1,

    /// <summary>Fully settled.</summary>
    Paid = 2,

    /// <summary>Past due date with an outstanding balance.</summary>
    Overdue = 3,

    /// <summary>Voided before settlement; the record is retained for history.</summary>
    Cancelled = 4,
}

/// <summary>
/// Why a payment request was raised. Kept as a string set (not an enum) so the
/// persisted value stays human-readable and the list can grow without a schema change.
/// </summary>
public static class PaymentPurpose
{
    public const string Rent = "Rent";
    public const string Utilities = "Utilities";
    public const string Deposit = "Deposit";
    public const string Maintenance = "Maintenance";
    public const string Other = "Other";

    public static readonly string[] All = [Rent, Utilities, Deposit, Maintenance, Other];

    public static bool IsKnown(string? purpose) => purpose is not null && All.Contains(purpose);
}

/// <summary>
/// Result state of a single payment attempt (BRULE-PAY-002/004).
/// Processed records are append-only and never deleted.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3,
}

public enum PaymentMethod
{
    Card = 0,
    BankTransfer = 1,
    Cash = 2,
    Manual = 3,
}

/// <summary>
/// Kind of attention a payment request needs — drives the dashboard notification bell.
/// </summary>
public enum PaymentAlertType
{
    NewRequest = 0,
    DueSoon = 1,
    Overdue = 2,
    FailedPayment = 3,
}
