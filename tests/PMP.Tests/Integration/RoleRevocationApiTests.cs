using PMP.Modules.Auth.Contracts;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-031 / GAP-003 — a removed or demoted role must stop authorizing the affected
/// request on the next call, without waiting for the access token's 15-minute expiry.
/// </summary>
[Collection(PmpApiCollection.Name)]
public class RoleRevocationApiTests
{
    private readonly PmpApiFixture _api;

    public RoleRevocationApiTests(PmpApiFixture api) => _api = api;

    [Fact]
    public async Task RemovedRole_StopsAuthorizing_OnTheNextRequest()
    {
        var admin = await _api.LoginAsync("admin@pmp.com", "Admin123!");

        // Provision an isolated technician so the seeded accounts are untouched.
        var email = $"it-revoke-{Guid.NewGuid():N}@pmp.test";
        var staff = await _api.PostAsync("/api/auth/staff", new CreateStaffRequest
        {
            Email = email,
            Password = "Staff123!",
            FirstName = "Temp",
            LastName = "Technician",
            Role = "Technician",
        }, admin.AccessToken);

        Assert.True(staff.Code == 200, $"Staff creation failed: {staff}");
        var created = _api.Deserialize<AuthResponse>(staff.Body);

        // The freshly issued token carries the Technician role and authorizes a staff endpoint.
        var before = await _api.GetAsync("/api/maintenance", created.AccessToken);
        Assert.True(before.Code == 200, $"Technician token must access maintenance but got {before}");

        // An administrator removes the Technician role after the token was issued.
        var demote = await _api.PutAsync(
            $"/api/auth/users/{created.UserId}/roles",
            new SetUserRolesRequest { Roles = Array.Empty<string>() },
            admin.AccessToken);

        Assert.True(demote.Code == 200, $"Role removal failed: {demote}");

        // The stale role claim must no longer authorize on the next request.
        var after = await _api.GetAsync("/api/maintenance", created.AccessToken);
        Assert.True(after.Code == 403, $"A demoted user's old token must be rejected but got {after}");
    }
}
