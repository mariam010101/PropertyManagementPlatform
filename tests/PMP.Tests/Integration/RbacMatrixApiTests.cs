namespace PMP.Tests.Integration;

/// <summary>
/// IMP-002 — the full least-privilege matrix for the MVP surface. Each policy is
/// deny-by-default: a caller that does not hold the required role receives 403
/// while a permitted caller receives 200.
/// </summary>
[Collection(PmpApiCollection.Name)]
public class RbacMatrixApiTests
{
    private readonly PmpApiFixture _api;

    public RbacMatrixApiTests(PmpApiFixture api) => _api = api;

    [Theory]
    // Resident: residents may read maintenance and their own profile, nothing else.
    [InlineData("resident@pmp.com", "Resident123!", "/api/properties", 403)]
    [InlineData("resident@pmp.com", "Resident123!", "/api/residents", 403)]
    [InlineData("resident@pmp.com", "Resident123!", "/api/auth/users", 403)]
    [InlineData("resident@pmp.com", "Resident123!", "/api/maintenance", 200)]
    [InlineData("resident@pmp.com", "Resident123!", "/api/residents/me", 200)]
    // Technician: staff maintenance access only, no property/resident/admin access.
    [InlineData("tech@pmp.com", "Tech123!", "/api/properties", 403)]
    [InlineData("tech@pmp.com", "Tech123!", "/api/residents", 403)]
    [InlineData("tech@pmp.com", "Tech123!", "/api/auth/users", 403)]
    [InlineData("tech@pmp.com", "Tech123!", "/api/maintenance", 200)]
    // PropertyManager: property + resident + maintenance access, but not admin.
    [InlineData("manager@pmp.com", "Manager123!", "/api/properties", 200)]
    [InlineData("manager@pmp.com", "Manager123!", "/api/residents", 200)]
    [InlineData("manager@pmp.com", "Manager123!", "/api/maintenance", 200)]
    [InlineData("manager@pmp.com", "Manager123!", "/api/auth/users", 403)]
    // Administrator: full MVP surface.
    [InlineData("admin@pmp.com", "Admin123!", "/api/auth/users", 200)]
    [InlineData("admin@pmp.com", "Admin123!", "/api/properties", 200)]
    [InlineData("admin@pmp.com", "Admin123!", "/api/residents", 200)]
    [InlineData("admin@pmp.com", "Admin123!", "/api/maintenance", 200)]
    public async Task Role_HasOnlyExpectedAccess(string email, string password, string path, int expectedStatus)
    {
        var auth = await _api.LoginAsync(email, password);

        var result = await _api.GetAsync(path, auth.AccessToken);

        Assert.True(result.Code == expectedStatus,
            $"{email} on {path} expected {expectedStatus} but got {result}");
    }
}
