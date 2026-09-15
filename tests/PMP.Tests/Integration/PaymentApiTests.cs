using System.Globalization;
using PMP.Modules.Auth.Contracts;
using PMP.Modules.Lease.Contracts;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Payment.Contracts;
using PMP.Modules.Payment.Enums;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-020/IMP-021 — payment requests, per-user obligations, the notification bell feed,
/// cancellation, declined payments and role-scoped access, verified over the real HTTP
/// surface (Kestrel + JWT + SQLite) rather than in-process shims.
/// </summary>
[Collection(PmpApiCollection.Name)]
public class PaymentApiTests
{
    private const string ManagerEmail = "manager@pmp.com";
    private const string ManagerPassword = "Manager123!";
    private const string ResidentEmail = "resident@pmp.com";
    private const string ResidentPassword = "Resident123!";
    private const string TechnicianEmail = "tech@pmp.com";
    private const string TechnicianPassword = "Tech123!";

    private readonly PmpApiFixture _api;

    public PaymentApiTests(PmpApiFixture api) => _api = api;

    // ---------- FR-AUTH-007 / FR-ACCT-004: access is denied by default ----------

    [Theory]
    [InlineData("/api/payments/dashboard")]
    [InlineData("/api/payments/outstanding")]
    [InlineData("/api/payments/history")]
    public async Task Anonymous_IsRejected_OnPaymentEndpoints(string path)
    {
        var result = await _api.GetAsync(path);
        Assert.True(result.Code == 401, $"Expected 401 for anonymous {path} but got {result}");
    }

    [Fact]
    public async Task NonFinancialRoles_CannotReadTheOutstandingReport()
    {
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var technician = await _api.LoginAsync(TechnicianEmail, TechnicianPassword);

        var residentResult = await _api.GetAsync("/api/payments/outstanding", resident.AccessToken);
        var technicianResult = await _api.GetAsync("/api/payments/outstanding", technician.AccessToken);

        Assert.True(residentResult.Code == 403, $"Resident must not read the outstanding report but got {residentResult}");
        Assert.True(technicianResult.Code == 403, $"Technician must not read the outstanding report but got {technicianResult}");
    }

    [Fact]
    public async Task Accountant_ReadsFinancials_ButIsRestrictedFromNonFinancialModules()
    {
        var accountant = await _api.LoginAsync("accountant@pmp.com", "Accountant123!");

        var outstanding = await _api.GetAsync("/api/payments/outstanding", accountant.AccessToken);
        Assert.True(outstanding.Code == 200, $"Accountant must read financial records but got {outstanding}");

        var properties = await _api.GetAsync("/api/properties", accountant.AccessToken);
        var maintenance = await _api.GetAsync("/api/maintenance", accountant.AccessToken);

        Assert.True(properties.Code == 403, $"Accountant must not read property data but got {properties}");
        Assert.True(maintenance.Code == 403, $"Accountant must not read maintenance data but got {maintenance}");
    }

    // ---------- per-user obligation is computed on the backend ----------

    [Fact]
    public async Task ResidentDashboard_ReturnsOwnObligations_WithRequestMetadata()
    {
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);

        var dashboard = await GetDashboardAsync(resident.AccessToken);

        // The backend computes the amount currently due from the resident's requests.
        Assert.Equal(dashboard.Requests.Where(r => r.OutstandingBalance > 0).Sum(r => r.OutstandingBalance), dashboard.AmountCurrentlyDue);
        Assert.Equal(dashboard.AmountCurrentlyDue > 0, dashboard.HasOutstanding);

        // A request identifies the resident, amount, currency, purpose, due date, status and creation date.
        Assert.All(dashboard.Requests, r => Assert.Equal(resident.UserId, r.ResidentUserId));
        Assert.All(dashboard.Requests, r => Assert.False(string.IsNullOrWhiteSpace(r.Currency)));
        Assert.All(dashboard.Requests, r => Assert.False(string.IsNullOrWhiteSpace(r.Purpose)));
        Assert.All(dashboard.Requests, r => Assert.NotEqual(default, r.DueDate));
        Assert.All(dashboard.Requests, r => Assert.NotEqual(default, r.CreatedAt));
    }

    [Fact]
    public async Task Resident_CannotReadAnotherResidentsPayments()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var seededResident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, seededResident.UserId);

        // A manager raises a request for the seeded resident.
        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        // A different, freshly registered resident must not see it and cannot pay it.
        var otherToken = await RegisterAndLoginResidentAsync();
        var otherDashboard = await GetDashboardAsync(otherToken);

        Assert.DoesNotContain(otherDashboard.Requests, r => r.Id == request.Id);
        Assert.Equal(0m, otherDashboard.AmountCurrentlyDue);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            otherToken);
        Assert.True(pay.Code == 400, $"A resident must not pay someone else's request but got {pay}");

        await CancelAsync(manager.AccessToken, request.Id);
    }

    // ---------- request creation derives the amount from the lease ----------

    [Fact]
    public async Task Manager_GeneratesRentRequest_FromLease_AndResidentSeesItInDashboardAndBell()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);

        var before = (await GetDashboardAsync(resident.AccessToken)).AmountCurrentlyDue;

        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        // The amount comes from the lease data — never hard-coded.
        Assert.Equal(lease.MonthlyRent, request.Amount);
        Assert.Equal(PaymentPurpose.Rent, request.Purpose);
        Assert.Equal(InvoiceStatus.Open, request.Status);

        var dashboard = await GetDashboardAsync(resident.AccessToken);
        Assert.Equal(before + lease.MonthlyRent, dashboard.AmountCurrentlyDue);

        var visible = dashboard.Requests.Single(r => r.Id == request.Id);
        Assert.Equal(lease.MonthlyRent, visible.OutstandingBalance);

        // The bell surfaces the request and links directly to it.
        Assert.Contains(dashboard.Alerts, a => a.RequestId == request.Id);

        await CancelAsync(manager.AccessToken, request.Id);
    }

    // ---------- manager / administrator view ----------

    [Fact]
    public async Task Manager_SeesWhichResidentsOweMoney()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);

        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var result = await _api.GetAsync("/api/payments/outstanding", manager.AccessToken);
        Assert.True(result.Code == 200, $"Outstanding report failed: {result}");
        var outstanding = _api.Deserialize<List<ResidentOutstandingDto>>(result.Body);

        var entry = outstanding.Single(o => o.ResidentUserId == resident.UserId);
        Assert.True(entry.TotalOutstanding >= lease.MonthlyRent, $"Expected at least {lease.MonthlyRent} but found {entry.TotalOutstanding}");
        Assert.False(string.IsNullOrWhiteSpace(entry.ResidentName));
        Assert.False(string.IsNullOrWhiteSpace(entry.UnitNumber));
        Assert.False(string.IsNullOrWhiteSpace(entry.Currency));

        await CancelAsync(manager.AccessToken, request.Id);
    }

    [Fact]
    public async Task Manager_CancelsRequest_AndResidentCanNoLongerPayIt()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);

        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var before = (await GetDashboardAsync(resident.AccessToken)).AmountCurrentlyDue;

        var cancel = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/cancel",
            new CancelPaymentRequestRequest { Reason = "Raised in error." },
            manager.AccessToken);
        Assert.True(cancel.Code == 200, $"Cancel failed: {cancel}");

        var dashboard = await GetDashboardAsync(resident.AccessToken);
        Assert.DoesNotContain(dashboard.Requests, r => r.Id == request.Id);
        Assert.Equal(before - lease.MonthlyRent, dashboard.AmountCurrentlyDue);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            resident.AccessToken);
        Assert.True(pay.Code == 400, $"A cancelled request must not be payable but got {pay}");
    }

    // ---------- failed payment attempts are preserved ----------

    [Fact]
    public async Task DeclinedPayment_IsRecordedInHistory_AndAppearsInTheBell()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);

        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        // Paying more than is owed is declined by the (simulated) processor.
        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent + 100m },
            resident.AccessToken);
        Assert.True(pay.Code == 400, $"An overpayment must be declined but got {pay}");

        // The failed attempt is retained in history...
        var historyResult = await _api.GetAsync("/api/payments/history", resident.AccessToken);
        Assert.True(historyResult.Code == 200, $"History failed: {historyResult}");
        var history = _api.Deserialize<List<PaymentTransactionDto>>(historyResult.Body);
        Assert.Contains(history, t => t.InvoiceId == request.Id && t.Status == PaymentStatus.Failed);

        // ...and raised to the notification bell.
        var dashboard = await GetDashboardAsync(resident.AccessToken);
        Assert.Contains(dashboard.Alerts, a => a.Type == PaymentAlertType.FailedPayment && a.RequestId == request.Id);

        // The request itself is untouched and still payable.
        var visible = dashboard.Requests.Single(r => r.Id == request.Id);
        Assert.Equal(lease.MonthlyRent, visible.OutstandingBalance);

        await CancelAsync(manager.AccessToken, request.Id);
    }

    // ---------- payment invoices (AC-01..AC-08) ----------

    [Theory]
    [InlineData("/api/invoices")]
    [InlineData("/api/invoices/my")]
    public async Task Anonymous_IsRejected_OnInvoiceEndpoints(string path)
    {
        var result = await _api.GetAsync(path);
        Assert.True(result.Code == 401, $"Expected 401 for anonymous {path} but got {result}");
    }

    [Fact]
    public async Task Resident_PaysAndReadsOwnInvoice()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);
        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            resident.AccessToken);
        Assert.True(pay.Code == 200, $"Payment failed: {pay}");

        var my = await _api.GetAsync("/api/invoices/my", resident.AccessToken);
        Assert.True(my.Code == 200, $"GET /api/invoices/my failed: {my}");
        var invoices = _api.Deserialize<List<PaymentInvoiceDto>>(my.Body);
        var invoice = invoices.FirstOrDefault(i => i.InvoiceId == request.Id);
        Assert.NotNull(invoice);
        Assert.Equal(lease.MonthlyRent, invoice.Amount);
        Assert.Equal(resident.UserId, invoice.ResidentUserId);
        Assert.Equal(PaymentInvoiceStatus.Issued, invoice.Status);
        Assert.Matches(@"^INV-\d{4}-\d{6}$", invoice.InvoiceNumber);
        Assert.False(string.IsNullOrWhiteSpace(invoice.ConfirmationNumber));

        var single = await _api.GetAsync($"/api/invoices/{invoice.Id}", resident.AccessToken);
        Assert.True(single.Code == 200, $"GET /api/invoices/{{id}} failed: {single}");
        var detail = _api.Deserialize<PaymentInvoiceDto>(single.Body);
        Assert.Equal(invoice.InvoiceNumber, detail.InvoiceNumber);
    }

    [Fact]
    public async Task Resident_CanExportOwnInvoice_AsCsv()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);
        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            resident.AccessToken);
        Assert.True(pay.Code == 200, $"Payment failed: {pay}");

        var my = await _api.GetAsync("/api/invoices/my", resident.AccessToken);
        var invoice = _api.Deserialize<List<PaymentInvoiceDto>>(my.Body).First(i => i.InvoiceId == request.Id);

        var export = await _api.GetAsync($"/api/invoices/{invoice.Id}/export", resident.AccessToken);
        Assert.True(export.Code == 200, $"CSV export failed: {export}");

        // A self-contained receipt: header row plus the invoice's own values.
        Assert.Contains("InvoiceNumber", export.Body);
        Assert.Contains("PaymentDate", export.Body);
        Assert.Contains(invoice.InvoiceNumber, export.Body);
        Assert.Contains(invoice.TransactionReference, export.Body);
        Assert.Contains(lease.MonthlyRent.ToString("0.00", CultureInfo.InvariantCulture), export.Body);
    }

    [Fact]
    public async Task Resident_CannotExportAnotherResidentsInvoice()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);
        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            resident.AccessToken);
        Assert.True(pay.Code == 200, $"Payment failed: {pay}");

        var my = await _api.GetAsync("/api/invoices/my", resident.AccessToken);
        var invoice = _api.Deserialize<List<PaymentInvoiceDto>>(my.Body).First(i => i.InvoiceId == request.Id);

        var other = await RegisterAndLoginResidentAsync();
        var export = await _api.GetAsync($"/api/invoices/{invoice.Id}/export", other);
        Assert.True(export.Code == 400, $"A resident must not export another resident's invoice but got {export}");
    }

    [Fact]
    public async Task Resident_CannotReadAnotherResidentsInvoice()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);
        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            resident.AccessToken);
        Assert.True(pay.Code == 200, $"Payment failed: {pay}");

        var my = await _api.GetAsync("/api/invoices/my", resident.AccessToken);
        var invoice = _api.Deserialize<List<PaymentInvoiceDto>>(my.Body).First(i => i.InvoiceId == request.Id);

        var other = await RegisterAndLoginResidentAsync();
        var direct = await _api.GetAsync($"/api/invoices/{invoice.Id}", other);
        Assert.True(direct.Code == 400, $"A resident must not read another resident's invoice but got {direct}");

        var others = await _api.GetAsync("/api/invoices/my", other);
        Assert.True(others.Code == 200);
        Assert.DoesNotContain(_api.Deserialize<List<PaymentInvoiceDto>>(others.Body), i => i.Id == invoice.Id);
    }

    [Fact]
    public async Task Resident_CannotAccessAccountantInvoiceListing()
    {
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var result = await _api.GetAsync("/api/invoices", resident.AccessToken);
        Assert.True(result.Code == 403, $"Resident must not access the financial invoice listing but got {result}");
    }

    [Fact]
    public async Task Accountant_CanReadInvoices_TechnicianCannot()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var lease = await GetSeededLeaseAsync(manager.AccessToken, resident.UserId);
        var request = await CreateRentRequestAsync(manager.AccessToken, lease.Id);

        var pay = await _api.PostAsync(
            $"/api/payments/invoices/{request.Id}/pay",
            new PayInvoiceRequest { Amount = lease.MonthlyRent },
            resident.AccessToken);
        Assert.True(pay.Code == 200, $"Payment failed: {pay}");

        var accountant = await _api.LoginAsync("accountant@pmp.com", "Accountant123!");
        var list = await _api.GetAsync("/api/invoices", accountant.AccessToken);
        Assert.True(list.Code == 200, $"Accountant must read invoices but got {list}");
        var invoices = _api.Deserialize<List<PaymentInvoiceDto>>(list.Body);
        var invoice = invoices.FirstOrDefault(i => i.InvoiceId == request.Id);
        Assert.NotNull(invoice);
        Assert.False(string.IsNullOrWhiteSpace(invoice.InvoiceNumber));
        Assert.False(string.IsNullOrWhiteSpace(invoice.ResidentName));

        var technician = await _api.LoginAsync(TechnicianEmail, TechnicianPassword);
        var denied = await _api.GetAsync("/api/invoices", technician.AccessToken);
        Assert.True(denied.Code == 403, $"Technician must not access invoices but got {denied}");
    }

    // ---------- helpers ----------

    private async Task<PaymentDashboardDto> GetDashboardAsync(string accessToken)
    {
        var result = await _api.GetAsync("/api/payments/dashboard", accessToken);
        Assert.True(result.Code == 200, $"Dashboard failed: {result}");
        return _api.Deserialize<PaymentDashboardDto>(result.Body);
    }

    private async Task<LeaseDto> GetSeededLeaseAsync(string managerToken, Guid residentUserId)
    {
        var result = await _api.GetAsync("/api/leases", managerToken);
        Assert.True(result.Code == 200, $"GET /api/leases failed: {result}");
        var leases = _api.Deserialize<List<LeaseDto>>(result.Body);

        return leases.FirstOrDefault(l => l.ResidentUserId == residentUserId && l.Status == LeaseStatus.Active)
            ?? throw new InvalidOperationException($"No active seeded lease for resident {residentUserId}: {result.Body}");
    }

    private async Task<InvoiceDto> CreateRentRequestAsync(string managerToken, Guid leaseId)
    {
        var result = await _api.PostAsync(
            "/api/payments/requests/rent",
            new GenerateRentRequestRequest { LeaseAgreementId = leaseId },
            managerToken);

        Assert.True(result.Code == 200, $"Rent request failed: {result}");
        return _api.Deserialize<InvoiceDto>(result.Body);
    }

    private async Task CancelAsync(string managerToken, Guid invoiceId)
    {
        var result = await _api.PostAsync(
            $"/api/payments/invoices/{invoiceId}/cancel",
            new CancelPaymentRequestRequest { Reason = "Test cleanup." },
            managerToken);
        Assert.True(result.Code == 200, $"Cleanup cancel failed: {result}");
    }

    private async Task<string> RegisterAndLoginResidentAsync()
    {
        var email = $"pay-{Guid.NewGuid():N}@pmp.test";
        const string password = "Payment123!";

        var register = await _api.PostAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            FirstName = "Pat",
            LastName = "Payer",
        });
        Assert.True(register.Code == 200, $"Registration failed: {register}");
        var registered = _api.Deserialize<AuthResponse>(register.Body);

        var confirmToken = Uri.EscapeDataString(registered.EmailConfirmationToken!);
        var confirm = await _api.PostAsync($"/api/auth/confirm-email?userId={registered.UserId}&token={confirmToken}");
        Assert.True(confirm.Code == 200, $"Email confirmation failed: {confirm}");

        return (await _api.LoginAsync(email, password)).AccessToken;
    }
}
