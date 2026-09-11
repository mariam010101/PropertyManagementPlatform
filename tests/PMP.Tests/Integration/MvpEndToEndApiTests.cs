using System.Net;
using System.Text.Json;
using PMP.Modules.Auth.Contracts;
using PMP.Modules.Maintenance.Contracts;
using PMP.Modules.Maintenance.Enums;
using PMP.Modules.Property.Contracts;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-006 — MVP end-to-end verification over the real HTTP surface.
///
/// Covers the MVP slice described in ADR-0007 (Auth, Property, Resident, Maintenance)
/// and the RBAC decision in ADR-0004: register → email confirmation → login → unit
/// occupancy → resident submits → manager assigns → technician progresses → resident
/// confirms, plus per-role authorization negatives and refresh-token lifecycle.
/// </summary>
[Collection(PmpApiCollection.Name)]
public class MvpEndToEndApiTests
{
    private const string AdminEmail = "admin@pmp.com";
    private const string AdminPassword = "Admin123!";
    private const string ManagerEmail = "manager@pmp.com";
    private const string ManagerPassword = "Manager123!";
    private const string TechnicianEmail = "tech@pmp.com";
    private const string TechnicianPassword = "Tech123!";
    private const string ResidentEmail = "resident@pmp.com";
    private const string ResidentPassword = "Resident123!";

    private readonly PmpApiFixture _api;

    public MvpEndToEndApiTests(PmpApiFixture api) => _api = api;

    // ---------- FR-AUTH-007: unauthorized access is prevented ----------

    [Theory]
    [InlineData("/api/properties")]
    [InlineData("/api/auth/users")]
    [InlineData("/api/maintenance")]
    public async Task AnonymousRequest_ToProtectedEndpoint_Returns401(string path)
    {
        var result = await _api.GetAsync(path);
        Assert.True(result.Code == 401, $"Expected 401 for anonymous {path} but got {result}");
    }

    // ---------- FR-AUTH-002 / FR-AUTH-008: seeded accounts authenticate with roles ----------

    [Theory]
    [InlineData(AdminEmail, AdminPassword, "Administrator", "System")]
    [InlineData(ManagerEmail, ManagerPassword, "PropertyManager", "Anna")]
    [InlineData(TechnicianEmail, TechnicianPassword, "Technician", "Tom")]
    [InlineData(ResidentEmail, ResidentPassword, "Resident", "Rita")]
    public async Task SeededUser_CanLogin_AndMeReturnsOwnIdentity(string email, string password, string expectedRole, string expectedFirstName)
    {
        var auth = await _api.LoginAsync(email, password);

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken), "No access token was issued.");
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken), "No refresh token was issued.");
        Assert.True(auth.ExpiresInSeconds > 0, "Access token expiry was not reported.");
        Assert.Contains(expectedRole, auth.Roles);
        Assert.Null(auth.EmailConfirmationToken);

        // FR-AUTH-008 evidence: the issued token is accepted by a protected endpoint.
        // GAP-017 evidence: /me returns the caller's own identity, so the UI shell can render
        // the signed-in user after a reload instead of a blank name.
        var me = await _api.GetAsync("/api/auth/me", auth.AccessToken);
        Assert.True(me.Code == 200, $"GET /api/auth/me failed for {email}: {me}");
        Assert.Contains(auth.UserId.ToString(), me.Body);
        Assert.Contains(auth.Email, me.Body);
        Assert.Contains(expectedFirstName, me.Body);
        Assert.Contains(expectedRole, me.Body);
    }

    // ---------- FR-AUTH-001 / email-verification gate / FR-RES-001 ----------

    [Fact]
    public async Task Registration_RequiresEmailConfirmation_BeforeFirstLogin()
    {
        var email = $"it-{Guid.NewGuid():N}@pmp.test";
        const string password = "Integration123!";

        var register = await _api.PostAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            FirstName = "Ivy",
            LastName = "Integration",
            PhoneNumber = "+37400000001",
        });

        Assert.True(register.Code == 200, $"Registration failed: {register}");
        var registered = _api.Deserialize<AuthResponse>(register.Body);
        Assert.NotEqual(Guid.Empty, registered.UserId);
        Assert.False(string.IsNullOrWhiteSpace(registered.EmailConfirmationToken),
            "Registration must expose the confirmation token until email delivery is wired in.");

        // The account must not be usable before the email is confirmed.
        var prematureLogin = await _api.PostAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        Assert.True(prematureLogin.Code == 400, $"Unconfirmed login must be rejected but got {prematureLogin}");

        var confirmToken = Uri.EscapeDataString(registered.EmailConfirmationToken!);
        var confirm = await _api.PostAsync($"/api/auth/confirm-email?userId={registered.UserId}&token={confirmToken}");
        Assert.True(confirm.Code == 200, $"Email confirmation failed: {confirm}");

        var login = await _api.LoginAsync(email, password);
        Assert.Contains("Resident", login.Roles);

        // FR-RES-001: registration auto-provisions the resident profile (owned by the caller).
        var profile = await _api.GetAsync("/api/residents/me", login.AccessToken);
        Assert.True(profile.Code == 200, $"GET /api/residents/me failed: {profile}");
        Assert.Contains(email, profile.Body);
    }

    // ---------- FR-AUTH-006 / ADR-0003: refresh rotation + logout revokes the session ----------

    [Fact]
    public async Task RefreshToken_IsRotated_AndLogoutRevokesTheSession()
    {
        var first = await _api.LoginAsync(ResidentEmail, ResidentPassword);

        var refresh = await _api.PostAsync("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = first.RefreshToken });
        Assert.True(refresh.Code == 200, $"Refresh failed: {refresh}");
        var rotated = _api.Deserialize<AuthResponse>(refresh.Body);
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);

        // ADR-0003: refresh tokens are rotated on use, so the consumed token is no longer valid.
        var reuse = await _api.PostAsync("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = first.RefreshToken });
        Assert.True(reuse.Code == 400, $"A consumed refresh token must be rejected but got {reuse}");

        // FR-AUTH-006: logout terminates the session.
        var logout = await _api.PostAsync("/api/auth/logout", new RefreshTokenRequest { RefreshToken = rotated.RefreshToken }, rotated.AccessToken);
        Assert.True(logout.Code == 200, $"Logout failed: {logout}");

        var afterLogout = await _api.PostAsync("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = rotated.RefreshToken });
        Assert.True(afterLogout.Code == 400, $"A revoked refresh token must be rejected but got {afterLogout}");
    }

    // ---------- FR-RBAC-002/004: authorization boundaries per role ----------

    [Fact]
    public async Task RoleBoundaries_AreEnforced_OnAdminAndManagerEndpoints()
    {
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);
        var technician = await _api.LoginAsync(TechnicianEmail, TechnicianPassword);

        var residentOnAdminUsers = await _api.GetAsync("/api/auth/users", resident.AccessToken);
        Assert.True(residentOnAdminUsers.Code == 403, $"Resident must not list users but got {residentOnAdminUsers}");

        var technicianCreatingProperty = await _api.PostAsync("/api/properties", new CreatePropertyRequest
        {
            Name = "Technician Property",
            Address = "Nowhere 1",
            ManagerUserId = technician.UserId,
        }, technician.AccessToken);
        Assert.True(technicianCreatingProperty.Code == 403,
            $"Technician must not create properties but got {technicianCreatingProperty}");

        var residentCreatingProperty = await _api.PostAsync("/api/properties", new CreatePropertyRequest
        {
            Name = "Resident Property",
            Address = "Nowhere 2",
            ManagerUserId = resident.UserId,
        }, resident.AccessToken);
        Assert.True(residentCreatingProperty.Code == 403,
            $"Resident must not create properties but got {residentCreatingProperty}");
    }

    // ---------- FR-PROP-004 + FR-MNT-003 + FR-MNT-004/008: the MVP journey ----------

    [Fact]
    public async Task MvpJourney_SubmitAssignProgressConfirm_ClosesTheRequest()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var technician = await _api.LoginAsync(TechnicianEmail, TechnicianPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);

        // FR-MNT-005 baseline: how many notifications the resident already has.
        var notificationsBefore = await GetNotificationCountAsync(resident.AccessToken);

        var (occupiedUnit, vacantUnit) = await GetSeededUnitsAsync(manager.AccessToken);
        Assert.True(occupiedUnit.IsOccupied, $"Unit {occupiedUnit.UnitNumber} should be occupied (FR-PROP-004).");
        Assert.False(vacantUnit.IsOccupied, $"Unit {vacantUnit.UnitNumber} should be vacant (FR-PROP-004).");

        // FR-MNT-001: the resident submits a request for their own unit.
        var submit = await _api.PostAsync("/api/maintenance", new CreateMaintenanceRequestRequest
        {
            Title = "Leaking kitchen tap",
            Description = "Water dripping under the sink.",
            UnitId = occupiedUnit.Id,
            Priority = MaintenancePriority.High,
        }, resident.AccessToken);

        Assert.True(submit.Code == 200, $"Submit failed: {submit}");
        var created = _api.Deserialize<MaintenanceRequestDto>(submit.Body);
        Assert.Equal(MaintenanceStatus.Submitted, created.Status);
        Assert.Equal(occupiedUnit.Id, created.UnitId);
        Assert.Equal(MaintenancePriority.High, created.Priority);

        // Manager scoping sees the request; an unassigned technician does not (least privilege).
        var managerList = await _api.GetAsync($"/api/maintenance?status=Submitted", manager.AccessToken);
        Assert.Contains(created.Id.ToString(), managerList.Body);

        var technicianListBefore = await _api.GetAsync("/api/maintenance", technician.AccessToken);
        Assert.DoesNotContain(created.Id.ToString(), technicianListBefore.Body);

        // FR-MNT-003: manager assigns the technician.
        var assign = await _api.PostAsync($"/api/maintenance/{created.Id}/assign", new AssignMaintenanceRequest
        {
            TechnicianUserId = technician.UserId,
            Comment = "Please attend today.",
        }, manager.AccessToken);

        Assert.True(assign.Code == 200, $"Assign failed: {assign}");
        var assigned = _api.Deserialize<MaintenanceRequestDto>(assign.Body);
        Assert.Equal(MaintenanceStatus.Assigned, assigned.Status);
        Assert.Equal(technician.UserId, assigned.AssignedToUserId);

        // The assigned technician now sees it.
        var technicianListAfter = await _api.GetAsync("/api/maintenance", technician.AccessToken);
        Assert.Contains(created.Id.ToString(), technicianListAfter.Body);

        // FR-MNT-004: technician drives the state machine.
        var inProgress = await UpdateStatusAsync(created.Id, MaintenanceStatus.InProgress, technician.AccessToken);
        Assert.Equal(MaintenanceStatus.InProgress, inProgress.Status);

        var completed = await UpdateStatusAsync(created.Id, MaintenanceStatus.Completed, technician.AccessToken);
        Assert.Equal(MaintenanceStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);

        // Invalid transitions are rejected (ADR-0009).
        var invalid = await _api.PostAsync($"/api/maintenance/{created.Id}/status",
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.Submitted }, technician.AccessToken);
        Assert.True(invalid.Code == 400, $"Completed→Submitted must be rejected but got {invalid}");

        // FR-MNT-008: resident confirmation closes the request.
        var confirm = await _api.PostAsync($"/api/maintenance/{created.Id}/confirm",
            new ConfirmCompletionRequest { Comment = "Fixed, thank you." }, resident.AccessToken);

        Assert.True(confirm.Code == 200, $"Confirm failed: {confirm}");
        var closed = _api.Deserialize<MaintenanceRequestDto>(confirm.Body);
        Assert.Equal(MaintenanceStatus.Closed, closed.Status);
        Assert.NotNull(closed.ConfirmedAt);
        Assert.NotNull(closed.ClosedAt);

        // FR-MNT-006: the full history is retained.
        Assert.True(closed.History.Count >= 5, $"Expected at least 5 history entries but found {closed.History.Count}.");

        // FR-MNT-004 (BRULE-MNT-004): closed requests are not modified.
        var confirmAgain = await _api.PostAsync($"/api/maintenance/{created.Id}/confirm", new ConfirmCompletionRequest(), resident.AccessToken);
        Assert.True(confirmAgain.Code == 400, $"A closed request must not be confirmed again but got {confirmAgain}");

        // FR-MNT-005 / FR-COM-001: the lifecycle persisted notifications for the resident
        // through the Communication module (cross-module adapter wired in Program.cs).
        var notificationsAfter = await GetNotificationCountAsync(resident.AccessToken);
        Assert.True(notificationsAfter > notificationsBefore,
            $"Expected new persisted notifications for the resident but the count stayed at {notificationsBefore}.");
    }

    // ---------- BRULE-MNT-001: residents cannot submit for units that are not theirs ----------

    [Fact]
    public async Task Resident_CannotSubmitRequest_ForAnotherUnit()
    {
        var manager = await _api.LoginAsync(ManagerEmail, ManagerPassword);
        var resident = await _api.LoginAsync(ResidentEmail, ResidentPassword);

        var (_, vacantUnit) = await GetSeededUnitsAsync(manager.AccessToken);

        var submit = await _api.PostAsync("/api/maintenance", new CreateMaintenanceRequestRequest
        {
            Title = "Request for a unit I do not rent",
            Description = "Should be rejected.",
            UnitId = vacantUnit.Id,
        }, resident.AccessToken);

        Assert.True(submit.Code == 400, $"Foreign-unit submission must be rejected but got {submit}");
        Assert.Contains("unit", submit.Body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- helpers ----------

    private async Task<MaintenanceRequestDto> UpdateStatusAsync(Guid requestId, MaintenanceStatus status, string accessToken)
    {
        var result = await _api.PostAsync($"/api/maintenance/{requestId}/status",
            new UpdateMaintenanceStatusRequest { Status = status, Comment = $"Moving to {status}." }, accessToken);

        Assert.True(result.Code == 200, $"Status change to {status} failed: {result}");
        return _api.Deserialize<MaintenanceRequestDto>(result.Body);
    }

    private async Task<int> GetNotificationCountAsync(string accessToken)
    {
        var result = await _api.GetAsync("/api/communication/notifications", accessToken);
        Assert.True(result.Code == 200, $"Notification lookup failed: {result}");

        using var document = JsonDocument.Parse(result.Body);
        return document.RootElement.GetArrayLength();
    }

    private async Task<(UnitDto Occupied, UnitDto Vacant)> GetSeededUnitsAsync(string managerToken)
    {
        var propertiesResult = await _api.GetAsync("/api/properties", managerToken);
        Assert.True(propertiesResult.Code == 200, $"GET /api/properties failed: {propertiesResult}");
        var properties = _api.Deserialize<List<PropertyDto>>(propertiesResult.Body);
        var property = properties.FirstOrDefault(p => p.UnitCount >= 2)
            ?? throw new InvalidOperationException($"Seeded property with units not found: {propertiesResult.Body}");

        var buildingsResult = await _api.GetAsync($"/api/properties/{property.Id}/buildings", managerToken);
        Assert.True(buildingsResult.Code == 200, $"GET buildings failed: {buildingsResult}");
        var building = _api.Deserialize<List<BuildingDto>>(buildingsResult.Body).First();

        var unitsResult = await _api.GetAsync($"/api/buildings/{building.Id}/units", managerToken);
        Assert.True(unitsResult.Code == 200, $"GET units failed: {unitsResult}");
        var units = _api.Deserialize<List<UnitDto>>(unitsResult.Body);

        var occupied = units.FirstOrDefault(u => u.UnitNumber == "A-101")
            ?? throw new InvalidOperationException($"Seeded unit A-101 not found: {unitsResult.Body}");
        var vacant = units.FirstOrDefault(u => u.UnitNumber == "A-102")
            ?? throw new InvalidOperationException($"Seeded unit A-102 not found: {unitsResult.Body}");

        return (occupied, vacant);
    }
}

/// <summary>Collection name shared by the integration suite so one API host serves all tests.</summary>
public static class PmpApiCollection
{
    public const string Name = "pmp-api-integration";
}

[CollectionDefinition(PmpApiCollection.Name)]
public class PmpApiCollectionDefinition : ICollectionFixture<PmpApiFixture>
{
}
