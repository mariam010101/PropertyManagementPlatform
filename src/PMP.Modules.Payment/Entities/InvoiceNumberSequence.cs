using PMP.Shared.Domain;

namespace PMP.Modules.Payment.Entities;

/// <summary>
/// A per-year sequence used to issue deterministic, human-readable invoice numbers
/// (<c>INV-{year}-{n:000000}</c>). This is a dedicated counter — not
/// <c>COUNT(invoices)+1</c> — so the number survives deletions and remains stable once
/// issued. A unique index on <see cref="PaymentInvoice.InvoiceNumber"/> is the hard
/// backstop that guarantees two invoices can never share a number under contention.
/// </summary>
public class InvoiceNumberSequence : BaseEntity
{
    /// <summary>The calendar year this sequence covers.</summary>
    public int Year { get; set; }

    /// <summary>The last number issued for <see cref="Year"/>.</summary>
    public int LastNumber { get; set; }
}
