using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Payment.Contracts;
using PMP.Modules.Payment.Enums;
using PMP.Modules.Payment.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

/// <summary>
/// Payment invoices: the receipts generated when a resident payment completes.
///
/// <para>Residents read only their own invoices (<c>/api/invoices/my</c>); accountants,
/// managers and administrators read invoices within their financial scope. Access is
/// enforced server-side — the single-invoice endpoint hides records that fall outside the
/// caller's ownership/scope so a resident can never read another resident's invoice.</para>
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly CurrentUser _currentUser;

    public InvoicesController(IPaymentService payments, CurrentUser currentUser)
    {
        _payments = payments;
        _currentUser = currentUser;
    }

    /// <summary>The caller's own payment invoices (AC-03).</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyInvoices()
    {
        var result = await _payments.GetMyPaymentInvoicesAsync(_currentUser.Id);
        return Ok(result);
    }

    /// <summary>
    /// Accountant/manager/admin listing with optional filters (AC-05). Managers are
    /// scoped to their properties; accountants and administrators see everything.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.Financial)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? residentUserId,
        [FromQuery] Guid? propertyId,
        [FromQuery] PaymentInvoiceStatus? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var result = await _payments.GetPaymentInvoicesAsync(
            _currentUser.Id,
            _currentUser.Roles,
            residentUserId,
            propertyId,
            status,
            from,
            to);

        return Ok(result);
    }

    /// <summary>A single invoice with ownership/scope enforcement (AC-04/AC-06).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id)
    {
        var result = await _payments.GetPaymentInvoiceAsync(_currentUser.Id, _currentUser.Roles, id);
        return result.ToActionResult();
    }
}
