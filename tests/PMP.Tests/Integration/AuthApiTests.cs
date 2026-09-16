using PMP.Modules.Auth.Contracts;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-001 — auth behaviours over the real HTTP surface that the MVP journey does
/// not already prove: account lockout after repeated failures, and login rejection
/// for deactivated accounts.
/// </summary>
[Collection(PmpApiCollection.Name)]
public class AuthApiTests
{
    private readonly PmpApiFixture _api;

    public AuthApiTests(PmpApiFixture api) => _api = api;

    [Fact]
    public async Task Login_IsLockedOut_AfterRepeatedFailedAttempts()
    {
        var email = $"it-lockout-{Guid.NewGuid():N}@pmp.test";
        const string password = "Integration123!";

        var registered = await RegisterAndConfirmAsync(email, password);

        // Identity lockout is configured to 5 failed attempts (ServiceCollectionExtensions).
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await _api.PostAsync("/api/auth/login",
                new LoginRequest { Email = email, Password = "Wrong123!" });
            Assert.True(failed.Code == 400, $"Failed attempt {attempt + 1} should be rejected but got {failed}");
        }

        // The account is now locked, even with the correct password.
        var locked = await _api.PostAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });
        Assert.True(locked.Code == 400, $"Locked account must be rejected but got {locked}");
        Assert.Contains("locked", locked.Body, StringComparison.OrdinalIgnoreCase);

        Assert.NotEqual(Guid.Empty, registered.UserId);
    }

    [Fact]
    public async Task DeactivatedUser_IsBlockedFromLogin()
    {
        var admin = await _api.LoginAsync("admin@pmp.com", "Admin123!");
        var email = $"it-inactive-{Guid.NewGuid():N}@pmp.test";
        const string password = "Integration123!";

        var registered = await RegisterAndConfirmAsync(email, password);
        var login = await _api.LoginAsync(email, password);

        var deactivate = await _api.PostAsync($"/api/auth/users/{login.UserId}/deactivate", null, admin.AccessToken);
        Assert.True(deactivate.Code == 200, $"Deactivation failed: {deactivate}");

        var blocked = await _api.PostAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });
        Assert.True(blocked.Code == 400, $"Deactivated account must be blocked but got {blocked}");
        Assert.Contains("deactivated", blocked.Body, StringComparison.OrdinalIgnoreCase);

        Assert.NotEqual(Guid.Empty, registered.UserId);
    }

    private async Task<AuthResponse> RegisterAndConfirmAsync(string email, string password)
    {
        var register = await _api.PostAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            FirstName = "Auth",
            LastName = "Test",
            PhoneNumber = "+37400000099",
        });
        Assert.True(register.Code == 200, $"Registration failed: {register}");

        var registered = _api.Deserialize<AuthResponse>(register.Body);
        var confirm = await _api.PostAsync(
            $"/api/auth/confirm-email?userId={registered.UserId}&token={Uri.EscapeDataString(registered.EmailConfirmationToken!)}");
        Assert.True(confirm.Code == 200, $"Email confirmation failed: {confirm}");

        return registered;
    }
}
