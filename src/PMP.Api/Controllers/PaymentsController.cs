using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Payment.Contracts;
using PMP.Modules.Payment.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

/// <summary>
/// Payment management (BR-005) and accountant-scoped financial reporting (BR-011).
/// Residents pay invoices and view balances/history; managers monitor; accountants
/// and admins access financial reports and reconciliation (and are restricted from
/// non-financial modules by other controllers' policies — FR-ACCT-004).
/// </summary>
[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly CurrentUser _currentUser;

    public PaymentsController(IPaymentService payments, CurrentUser currentUser)
    {
        _payments = payments;
        _currentUser = currentUser;
    }

    /// <summary>Resident: outstanding balance + open invoices (FR-PAY-001, BRULE-PAY-006).</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var result = await _payments.GetBalanceAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }

    /// <summary>Manager/admin/accountant: invoices (FR-PAY-005).</summary>
    [HttpGet("invoices")]
    [Authorize(Policy = AppPolicies.Financial)]
    public async Task<IActionResult> GetInvoices([FromQuery] string? status)
    {
        var result = await _payments.GetInvoicesAsync(_currentUser.Id, _currentUser.Roles, status);
        return Ok(result);
    }

    /// <summary>Manager/admin creates an invoice for a lease (basis for FR-PAY).</summary>
    [HttpPost("invoices")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var result = await _payments.CreateInvoiceAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>Resident pays an invoice electronically (FR-PAY-002/003, BRULE-PAY-005).</summary>
    [HttpPost("invoices/{invoiceId:guid}/pay")]
    public async Task<IActionResult> PayInvoice(Guid invoiceId, [FromBody] PayInvoiceRequest request)
    {
        var result = await _payments.PayInvoiceAsync(_currentUser.Id, _currentUser.Roles, invoiceId, request);
        return result.ToActionResult();
    }

    /// <summary>Resident: payment history (FR-PAY-004).</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var result = await _payments.GetPaymentHistoryAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }

    /// <summary>Financial report (FR-PAY-006, FR-ACCT-002).</summary>
    [HttpGet("report")]
    [Authorize(Policy = AppPolicies.Financial)]
    public async Task<IActionResult> GetReport()
    {
        var result = await _payments.GetFinancialReportAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }

    /// <summary>Reconciliation status (FR-ACCT-003).</summary>
    [HttpGet("reconciliation")]
    [Authorize(Policy = AppPolicies.Financial)]
    public async Task<IActionResult> GetReconciliation()
    {
        var result = await _payments.GetReconciliationAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }
}
