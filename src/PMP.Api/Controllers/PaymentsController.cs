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
/// Payment management (BR-005) and accountant-scoped financial reporting (BR-011).
///
/// <para>Residents see only their own payment requests, dashboard and history; managers
/// monitor the residents who owe money on their properties; accountants/admins access
/// financial reports and reconciliation.</para>
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

    /// <summary>
    /// The caller's own payment dashboard: amount currently due, upcoming, overdue,
    /// request statuses/due dates, history and notification-bell alerts. The caller can
    /// only ever read their own obligations (FR-PAY-001/004/005/007).
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _payments.GetDashboardAsync(_currentUser.Id);
        return Ok(dashboard);
    }

    /// <summary>Resident: outstanding balance + open requests (FR-PAY-001, BRULE-PAY-006).</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var result = await _payments.GetBalanceAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }

    /// <summary>Manager/admin/accountant: payment requests within scope (FR-PAY-005).</summary>
    [HttpGet("invoices")]
    [Authorize(Policy = AppPolicies.Financial)]
    public async Task<IActionResult> GetInvoices([FromQuery] InvoiceStatus? status)
    {
        var result = await _payments.GetInvoicesAsync(_currentUser.Id, _currentUser.Roles, status);
        return Ok(result);
    }

    /// <summary>
    /// Manager/admin/accountant: which residents owe money, how much and what is overdue
    /// (FR-PAY-005). Managers are scoped to their own properties.
    /// </summary>
    [HttpGet("outstanding")]
    [Authorize(Policy = AppPolicies.Financial)]
    public async Task<IActionResult> GetOutstanding()
    {
        var result = await _payments.GetOutstandingByResidentAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }

    /// <summary>Manager/admin raises a payment request with an explicit amount.</summary>
    [HttpPost("invoices")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var result = await _payments.CreateInvoiceAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>
    /// Manager/admin raises a rent request derived from the lease (<c>MonthlyRent</c>).
    /// The amount is never hard-coded (FR-PAY, BRULE-PAY-001).
    /// </summary>
    [HttpPost("requests/rent")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GenerateRentRequest([FromBody] GenerateRentRequestRequest request)
    {
        var result = await _payments.GenerateRentRequestAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>Resident pays a payment request (FR-PAY-002/003, BRULE-PAY-005).</summary>
    [HttpPost("invoices/{invoiceId:guid}/pay")]
    public async Task<IActionResult> PayInvoice(Guid invoiceId, [FromBody] PayInvoiceRequest request)
    {
        var result = await _payments.PayInvoiceAsync(_currentUser.Id, _currentUser.Roles, invoiceId, request);
        return result.ToActionResult();
    }

    /// <summary>Manager/admin cancels an unsettled payment request; the record is retained.</summary>
    [HttpPost("invoices/{invoiceId:guid}/cancel")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CancelInvoice(Guid invoiceId, [FromBody] CancelPaymentRequestRequest request)
    {
        var result = await _payments.CancelInvoiceAsync(_currentUser.Id, _currentUser.Roles, invoiceId, request);
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

    /// <summary>Admin trigger for the overdue/due-date sweep (FR-PAY-007).</summary>
    [HttpPost("run-due-dates")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> RunDueDates()
    {
        var overdue = await _payments.MarkOverdueAsync();
        var reminded = await _payments.SendDueDateRemindersAsync();
        return Ok(new { overdue, reminded });
    }
}
