using Microsoft.EntityFrameworkCore;
using PMP.Modules.Communication.Contracts;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Communication.Services;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Entities;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Payment.Contracts;
using PMP.Modules.Payment.Data;
using PMP.Modules.Payment.Entities;
using PMP.Modules.Payment.Enums;
using PMP.Modules.Payment.Services;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-020 — payment request/order lifecycle, per-user obligation calculation,
/// overdue detection, cancellation, declined payments and manager scoping.
/// </summary>
public class PaymentServiceTests
{
    // ---------- amount derivation (never hard-coded) ----------

    [Fact]
    public async Task GenerateRentRequest_DerivesAmountFromLeaseData()
    {
        var ctx = await CreateContextAsync(monthlyRent: 1500m);

        var result = await ctx.Service.GenerateRentRequestAsync(
            ctx.ManagerId, [AppRoles.PropertyManager],
            new GenerateRentRequestRequest { LeaseAgreementId = ctx.Lease.Id });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1500m, result.Data!.Amount);
        Assert.Equal(PaymentPurpose.Rent, result.Data.Purpose);
        Assert.Equal(PaymentCurrency.Default, result.Data.Currency);
        Assert.Equal(InvoiceStatus.Open, result.Data.Status);
        Assert.Equal(ctx.ResidentId, result.Data.ResidentUserId);

        // The request is persisted and the resident is told about it.
        Assert.Single(await ctx.PaymentDb.Invoices.ToListAsync());
        Assert.Contains(ctx.Communication.Sent, n => n.Title == "New payment request");
    }

    [Fact]
    public async Task CreateInvoice_RejectsNonManagerRoles()
    {
        var ctx = await CreateContextAsync();

        var asTechnician = await ctx.Service.CreateInvoiceAsync(
            ctx.ManagerId, [AppRoles.Technician],
            new CreateInvoiceRequest { LeaseAgreementId = ctx.Lease.Id, Amount = 10m });

        var asResident = await ctx.Service.CreateInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident],
            new CreateInvoiceRequest { LeaseAgreementId = ctx.Lease.Id, Amount = 10m });

        Assert.False(asTechnician.Succeeded);
        Assert.False(asResident.Succeeded);
        Assert.Empty(await ctx.PaymentDb.Invoices.ToListAsync());
    }

    [Fact]
    public async Task CreateInvoice_RejectsUnknownPaymentPurpose()
    {
        var ctx = await CreateContextAsync();

        var result = await ctx.Service.CreateInvoiceAsync(
            ctx.ManagerId, [AppRoles.PropertyManager],
            new CreateInvoiceRequest { LeaseAgreementId = ctx.Lease.Id, Amount = 25m, Purpose = "Bitcoin" });

        Assert.False(result.Succeeded);
        Assert.Contains("purpose", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- per-user obligation / dashboard ----------

    [Fact]
    public async Task Dashboard_ComputesCurrentDue_OverdueAndUpcoming()
    {
        var ctx = await CreateContextAsync();
        var now = DateTimeOffset.UtcNow;

        ctx.PaymentDb.Invoices.AddRange(
            NewInvoice(ctx, amount: 500m, dueDate: now.AddDays(-5)),
            NewInvoice(ctx, amount: 300m, dueDate: now.AddDays(10)));
        await ctx.PaymentDb.SaveChangesAsync();

        var dashboard = await ctx.Service.GetDashboardAsync(ctx.ResidentId);

        Assert.Equal(800m, dashboard.AmountCurrentlyDue);
        Assert.Equal(500m, dashboard.OverdueAmount);
        Assert.Equal(300m, dashboard.UpcomingAmount);
        Assert.Equal(1, dashboard.OverdueRequestCount);
        Assert.Equal(2, dashboard.OutstandingRequestCount);
        Assert.True(dashboard.HasOutstanding);
        Assert.Equal(dashboard.Requests.Single(r => r.OutstandingBalance == 300m).DueDate, dashboard.NextDueDate);

        // The past-due unsettled request is reported overdue even before the sweep runs.
        Assert.Contains(dashboard.Requests, r => r.Status == InvoiceStatus.Overdue);
        Assert.Contains(dashboard.Alerts, a => a.Type == PaymentAlertType.Overdue && a.Severity == "critical");
        Assert.Contains(dashboard.Alerts, a => a.Type == PaymentAlertType.NewRequest);
    }

    [Fact]
    public async Task Dashboard_OnlyReturnsCallersOwnRequests()
    {
        var ctx = await CreateContextAsync();
        var otherResident = Guid.NewGuid();

        ctx.PaymentDb.Invoices.AddRange(
            NewInvoice(ctx, amount: 100m, residentId: ctx.ResidentId),
            NewInvoice(ctx, amount: 999m, residentId: otherResident));
        await ctx.PaymentDb.SaveChangesAsync();

        var dashboard = await ctx.Service.GetDashboardAsync(ctx.ResidentId);

        Assert.All(dashboard.Requests, r => Assert.Equal(ctx.ResidentId, r.ResidentUserId));
        Assert.Equal(100m, dashboard.AmountCurrentlyDue);
        Assert.DoesNotContain(dashboard.Requests, r => r.Amount == 999m);
    }

    // ---------- overdue detection ----------

    [Fact]
    public async Task MarkOverdue_PersistsTransition_AndNotifiesOnlyOnce()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 250m, dueDate: DateTimeOffset.UtcNow.AddDays(-2));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var first = await ctx.Service.MarkOverdueAsync();
        var second = await ctx.Service.MarkOverdueAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second); // already overdue; no repeat

        var reloaded = await ctx.PaymentDb.Invoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Overdue, reloaded.Status);
        Assert.NotNull(reloaded.OverdueNotifiedAt);
        Assert.Single(ctx.Communication.Sent, n => n.Title == "Payment overdue");
    }

    [Fact]
    public async Task MarkOverdue_LeavesFutureRequestsUntouched()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 250m, dueDate: DateTimeOffset.UtcNow.AddDays(5));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var count = await ctx.Service.MarkOverdueAsync();

        Assert.Equal(0, count);
        Assert.Equal(InvoiceStatus.Open, (await ctx.PaymentDb.Invoices.SingleAsync(i => i.Id == invoice.Id)).Status);
    }

    // ---------- cancellation ----------

    [Fact]
    public async Task Cancel_UnsettledRequest_IsRetainedButDropsOutOfTheDashboard()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 400m, dueDate: DateTimeOffset.UtcNow.AddDays(3));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var cancel = await ctx.Service.CancelInvoiceAsync(
            ctx.ManagerId, [AppRoles.PropertyManager], invoice.Id, new CancelPaymentRequestRequest { Reason = "Tenant moved out." });

        Assert.True(cancel.Succeeded, cancel.Error);
        Assert.Equal(InvoiceStatus.Cancelled, cancel.Data!.Status);

        // The record is retained for history...
        var retained = await ctx.PaymentDb.Invoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Cancelled, retained.Status);
        Assert.NotNull(retained.CancelledAt);

        // ...but no longer counts as an obligation.
        var dashboard = await ctx.Service.GetDashboardAsync(ctx.ResidentId);
        Assert.Equal(0m, dashboard.AmountCurrentlyDue);
        Assert.DoesNotContain(dashboard.Requests, r => r.Id == invoice.Id);

        // And it can no longer be paid.
        var pay = await ctx.Service.PayInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident], invoice.Id, new PayInvoiceRequest { Amount = 400m });
        Assert.False(pay.Succeeded);
    }

    [Fact]
    public async Task Cancel_RejectsAlreadyPaidRequest()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 100m, dueDate: DateTimeOffset.UtcNow.AddDays(1));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var pay = await ctx.Service.PayInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident], invoice.Id, new PayInvoiceRequest { Amount = 100m });
        Assert.True(pay.Succeeded);

        var cancel = await ctx.Service.CancelInvoiceAsync(
            ctx.ManagerId, [AppRoles.PropertyManager], invoice.Id, new CancelPaymentRequestRequest());

        Assert.False(cancel.Succeeded);
    }

    // ---------- payment attempts (paid + failed) ----------

    [Fact]
    public async Task Pay_FullAmount_MarksRequestPaidWithConfirmation()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 1000m, dueDate: DateTimeOffset.UtcNow.AddDays(4));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var pay = await ctx.Service.PayInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident], invoice.Id, new PayInvoiceRequest { Amount = 1000m });

        Assert.True(pay.Succeeded, pay.Error);
        Assert.Equal(PaymentStatus.Completed, pay.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(pay.Data.ConfirmationNumber));

        var settled = await ctx.PaymentDb.Invoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, settled.Status);
        Assert.Equal(1000m, settled.PaidAmount);
        Assert.NotNull(settled.PaidAt);

        var dashboard = await ctx.Service.GetDashboardAsync(ctx.ResidentId);
        Assert.Equal(0m, dashboard.AmountCurrentlyDue);
        Assert.Single(dashboard.History);
    }

    [Fact]
    public async Task Pay_PartialAmount_MarksPartiallyPaid()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 1000m, dueDate: DateTimeOffset.UtcNow.AddDays(4));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var pay = await ctx.Service.PayInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident], invoice.Id, new PayInvoiceRequest { Amount = 250m });

        Assert.True(pay.Succeeded, pay.Error);
        Assert.Equal(InvoiceStatus.PartiallyPaid, (await ctx.PaymentDb.Invoices.SingleAsync(i => i.Id == invoice.Id)).Status);
        Assert.Equal(750m, (await ctx.Service.GetDashboardAsync(ctx.ResidentId)).AmountCurrentlyDue);
    }

    [Fact]
    public async Task Pay_DeclinedAttempt_IsRecordedAsFailed_AndRaisedToTheBell()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 1000m, dueDate: DateTimeOffset.UtcNow.AddDays(4));
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var pay = await ctx.Service.PayInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident], invoice.Id,
            new PayInvoiceRequest { Amount = 2000m }); // exceeds outstanding → declined

        Assert.False(pay.Succeeded);

        // The request still stands unchanged...
        var request = await ctx.PaymentDb.Invoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Open, request.Status);
        Assert.Equal(0m, request.PaidAmount);

        // ...but the failed attempt is preserved in history and history is append-only.
        var failed = await ctx.PaymentDb.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrWhiteSpace(failed.FailureReason));

        var dashboard = await ctx.Service.GetDashboardAsync(ctx.ResidentId);
        Assert.Contains(dashboard.History, t => t.Status == PaymentStatus.Failed);
        Assert.Contains(dashboard.Alerts, a =>
            a.Type == PaymentAlertType.FailedPayment && a.RequestId == invoice.Id && a.Severity == "critical");
    }

    [Fact]
    public async Task Pay_RejectsPayingAnotherUsersRequest()
    {
        var ctx = await CreateContextAsync();
        var invoice = NewInvoice(ctx, amount: 100m, residentId: Guid.NewGuid());
        ctx.PaymentDb.Invoices.Add(invoice);
        await ctx.PaymentDb.SaveChangesAsync();

        var pay = await ctx.Service.PayInvoiceAsync(
            ctx.ResidentId, [AppRoles.Resident], invoice.Id, new PayInvoiceRequest { Amount = 100m });

        Assert.False(pay.Succeeded);
        Assert.Empty(await ctx.PaymentDb.PaymentTransactions.ToListAsync());
    }

    // ---------- manager / administrator view ----------

    [Fact]
    public async Task OutstandingByResident_IsScopedToTheManagersProperties()
    {
        var ctx = await CreateContextAsync();

        // A second property, managed by someone else, also owes money.
        var otherManager = Guid.NewGuid();
        var otherResident = Guid.NewGuid();
        var otherProperty = new ManagedProperty { Name = "Other", Address = "2 Far", ManagerUserId = otherManager };
        ctx.PropertyDb.Properties.Add(otherProperty);
        var otherBuilding = new Building { PropertyId = otherProperty.Id, Name = "Tower B", Property = otherProperty };
        ctx.PropertyDb.Buildings.Add(otherBuilding);
        var otherUnit = new ResidentialUnit
        {
            BuildingId = otherBuilding.Id,
            UnitNumber = "B-201",
            UnitType = "OneBedroom",
            OperationalStatus = UnitOperationalStatus.Active,
            Building = otherBuilding,
        };
        ctx.PropertyDb.ResidentialUnits.Add(otherUnit);
        ctx.ResidentDb.ResidentProfiles.Add(new ResidentProfile
        {
            UserId = otherResident, FirstName = "Oscar", LastName = "Other", Email = "oscar@pmp.test", IsActive = true,
        });
        await ctx.PropertyDb.SaveChangesAsync();
        await ctx.ResidentDb.SaveChangesAsync();

        ctx.PaymentDb.Invoices.AddRange(
            NewInvoice(ctx, amount: 300m, dueDate: DateTimeOffset.UtcNow.AddDays(-1)),
            NewInvoice(ctx, amount: 700m, residentId: otherResident, unitId: otherUnit.Id, propertyId: otherProperty.Id));
        await ctx.PaymentDb.SaveChangesAsync();

        var forManager = await ctx.Service.GetOutstandingByResidentAsync(ctx.ManagerId, [AppRoles.PropertyManager]);
        var forAdmin = await ctx.Service.GetOutstandingByResidentAsync(Guid.NewGuid(), [AppRoles.Administrator]);

        Assert.Single(forManager);
        Assert.Equal(ctx.ResidentId, forManager[0].ResidentUserId);
        Assert.Equal(300m, forManager[0].TotalOutstanding);
        Assert.True(forManager[0].HasOverdue);

        Assert.Equal(2, forAdmin.Count);
    }

    [Fact]
    public async Task OutstandingByResident_ReturnsNothing_ForNonFinancialRoles()
    {
        var ctx = await CreateContextAsync();
        ctx.PaymentDb.Invoices.Add(NewInvoice(ctx, amount: 100m));
        await ctx.PaymentDb.SaveChangesAsync();

        var result = await ctx.Service.GetOutstandingByResidentAsync(ctx.ResidentId, [AppRoles.Resident]);

        Assert.Empty(result);
    }

    // ---------- helpers ----------

    private static Invoice NewInvoice(
        PaymentTestContext ctx,
        decimal amount,
        DateTimeOffset? dueDate = null,
        Guid? residentId = null,
        Guid? unitId = null,
        Guid? propertyId = null,
        InvoiceStatus status = InvoiceStatus.Open)
    {
        var due = dueDate ?? DateTimeOffset.UtcNow.AddDays(5);
        return new Invoice
        {
            LeaseAgreementId = ctx.Lease.Id,
            ResidentUserId = residentId ?? ctx.ResidentId,
            UnitId = unitId ?? ctx.Unit.Id,
            PropertyId = propertyId ?? ctx.Property.Id,
            PeriodStart = due.AddDays(-10),
            PeriodEnd = due,
            DueDate = due,
            Amount = amount,
            Currency = PaymentCurrency.Default,
            Purpose = PaymentPurpose.Rent,
            Status = status,
        };
    }

    private static async Task<PaymentTestContext> CreateContextAsync(decimal monthlyRent = 1200m)
    {
        var paymentDb = new PaymentDbContext(NewOptions<PaymentDbContext>());
        var leaseDb = new LeaseDbContext(NewOptions<LeaseDbContext>());
        var propertyDb = new PropertyDbContext(NewOptions<PropertyDbContext>());
        var residentDb = new ResidentDbContext(NewOptions<ResidentDbContext>());
        var communication = new FakeCommunicationService();

        var managerId = Guid.NewGuid();
        var residentId = Guid.NewGuid();

        var property = new ManagedProperty { Name = "Sunrise", Address = "1 Greenway", ManagerUserId = managerId };
        propertyDb.Properties.Add(property);
        var building = new Building { PropertyId = property.Id, Name = "Tower A", Property = property };
        propertyDb.Buildings.Add(building);
        var unit = new ResidentialUnit
        {
            BuildingId = building.Id,
            UnitNumber = "A-101",
            UnitType = "TwoBedroom",
            OperationalStatus = UnitOperationalStatus.Active,
            Building = building,
        };
        propertyDb.ResidentialUnits.Add(unit);
        await propertyDb.SaveChangesAsync();

        residentDb.ResidentProfiles.Add(new ResidentProfile
        {
            UserId = residentId, FirstName = "Rita", LastName = "Resident", Email = "rita@pmp.test", IsActive = true,
        });
        await residentDb.SaveChangesAsync();

        var lease = new LeaseAgreement
        {
            ResidentUserId = residentId,
            UnitId = unit.Id,
            StartDate = DateTimeOffset.UtcNow.AddMonths(-3),
            EndDate = DateTimeOffset.UtcNow.AddMonths(9),
            MonthlyRent = monthlyRent,
            Status = LeaseStatus.Active,
        };
        leaseDb.LeaseAgreements.Add(lease);
        await leaseDb.SaveChangesAsync();

        var service = new PaymentService(paymentDb, leaseDb, propertyDb, residentDb, communication);

        return new PaymentTestContext(
            service, paymentDb, propertyDb, residentDb, leaseDb, communication,
            managerId, residentId, property, building, unit, lease);
    }

    private static DbContextOptions<T> NewOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"pmp-payment-{Guid.NewGuid():N}")
            .Options;

    private sealed record PaymentTestContext(
        PaymentService Service,
        PaymentDbContext PaymentDb,
        PropertyDbContext PropertyDb,
        ResidentDbContext ResidentDb,
        LeaseDbContext LeaseDb,
        FakeCommunicationService Communication,
        Guid ManagerId,
        Guid ResidentId,
        ManagedProperty Property,
        Building Building,
        ResidentialUnit Unit,
        LeaseAgreement Lease);

    private sealed class FakeCommunicationService : ICommunicationService
    {
        public List<(Guid UserId, string Title, string Body, string EventType)> Sent { get; } = [];

        public Task<Result> SendUserNotificationAsync(Guid recipientUserId, string title, string body, string eventType, NotificationChannel channel)
        {
            Sent.Add((recipientUserId, title, body, eventType));
            return Task.FromResult(Result.Ok());
        }

        public Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(Guid userId, bool unreadOnly = false) =>
            Task.FromResult<IReadOnlyList<NotificationDto>>([]);

        public Task<UnreadCountDto> GetUnreadCountAsync(Guid userId) => Task.FromResult(new UnreadCountDto());

        public Task<Result> MarkNotificationReadAsync(Guid userId, Guid notificationId) => Task.FromResult(Result.Ok());

        public Task<Result> MarkAllReadAsync(Guid userId) => Task.FromResult(Result.Ok());

        public Task<Result<AnnouncementDto>> PublishAnnouncementAsync(Guid actorId, IReadOnlyList<string> roles, PublishAnnouncementRequest request) =>
            Task.FromResult(Result.Fail<AnnouncementDto>("Not used in this test."));

        public Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId) =>
            Task.FromResult<IReadOnlyList<AnnouncementDto>>([]);

        public Task<IReadOnlyList<AnnouncementDto>> GetResidentAnnouncementsAsync(Guid residentUserId) =>
            Task.FromResult<IReadOnlyList<AnnouncementDto>>([]);
    }
}
