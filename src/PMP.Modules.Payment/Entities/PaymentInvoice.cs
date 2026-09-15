using PMP.Modules.Payment.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Payment.Entities;

/// <summary>
/// The invoice document generated when a resident payment reaches the completed state
/// (FR-PAY-003 extension, AC-01/AC-07). It is a formal receipt tied one-to-one with a
/// single <see cref="PaymentTransaction"/> — <c>Payment 1 → PaymentInvoice 1</c> — so a
/// successful payment can never yield more than one invoice.
///
/// <para>This is deliberately a different concept from <see cref="Invoice"/>, which is
/// the payment <em>request/order</em> a resident is required to pay. The request says
/// what is owed; the <see cref="PaymentInvoice"/> says what was actually paid and when.
/// The invoice denormalizes the resident/unit/property ids (mirroring the request) so it
/// can be reported on and scoped without joining back through the request.</para>
/// </summary>
public class PaymentInvoice : BaseEntity
{
    /// <summary>Stable, human-readable number, e.g. <c>INV-2026-000001</c>. Unique.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>The completed payment this invoice receipts. Unique — one invoice per payment.</summary>
    public Guid PaymentTransactionId { get; set; }

    /// <summary>The completed payment's unique transaction reference (BRULE-PAY-002).</summary>
    public string TransactionReference { get; set; } = string.Empty;

    /// <summary>The payment request (bill) the payment settled.</summary>
    public Guid InvoiceId { get; set; }

    public Guid ResidentUserId { get; set; }

    /// <summary>Denormalized unit/property ids for reporting + manager scoping.</summary>
    public Guid UnitId { get; set; }

    public Guid PropertyId { get; set; }

    /// <summary>The amount actually paid, matching the completed payment (AC-08).</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency the amount is expressed in.</summary>
    public string Currency { get; set; } = PaymentCurrency.Default;

    /// <summary>Why the payment was owed (Rent, Utilities, …).</summary>
    public string Purpose { get; set; } = PaymentPurpose.Rent;

    /// <summary>How the payment was made (Card, BankTransfer, …).</summary>
    public PaymentMethod Method { get; set; }

    public PaymentInvoiceStatus Status { get; set; } = PaymentInvoiceStatus.Issued;

    /// <summary>When the underlying payment completed.</summary>
    public DateTimeOffset PaymentDate { get; set; }

    /// <summary>The completed payment's confirmation reference (BRULE-PAY-005).</summary>
    public string? ConfirmationNumber { get; set; }
}
