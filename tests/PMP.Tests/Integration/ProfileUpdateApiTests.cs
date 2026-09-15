using PMP.Modules.Auth.Contracts;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-030 / GAP-004 — an authenticated user can edit their own identity profile
/// (name fields) and the change is scoped to the caller.
/// </summary>
[Collection(PmpApiCollection.Name)]
public class ProfileUpdateApiTests
{
    private readonly PmpApiFixture _api;

    public ProfileUpdateApiTests(PmpApiFixture api) => _api = api;

    [Fact]
    public async Task Unauthenticated_ProfileUpdate_IsRejected()
    {
        var result = await _api.PutAsync("/api/auth/me", new UpdateMyProfileRequest
        {
            FirstName = "X",
            LastName = "Y",
        });

        Assert.True(result.Code == 401, $"Expected 401 but got {result}");
    }

    [Fact]
    public async Task User_CanUpdateOwnProfile_AndTheChangeIsScopedToTheCaller()
    {
        var email = $"it-profile-{Guid.NewGuid():N}@pmp.test";
        const string password = "Profile123!";

        var register = await _api.PostAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            FirstName = "Before",
            LastName = "Update",
            PhoneNumber = "+37400000001",
        });
        Assert.True(register.Code == 200, $"Registration failed: {register}");
        var registered = _api.Deserialize<AuthResponse>(register.Body);

        var confirm = await _api.PostAsync(
            $"/api/auth/confirm-email?userId={registered.UserId}&token={Uri.EscapeDataString(registered.EmailConfirmationToken!)}");
        Assert.True(confirm.Code == 200, $"Email confirmation failed: {confirm}");

        var login = await _api.LoginAsync(email, password);

        var update = await _api.PutAsync("/api/auth/me", new UpdateMyProfileRequest
        {
            FirstName = "After",
            LastName = "Changed",
            PhoneNumber = "+37400000002",
        }, login.AccessToken);
        Assert.True(update.Code == 200, $"Profile update failed: {update}");

        // Identity name fields were updated.
        var me = await _api.GetAsync("/api/auth/me", login.AccessToken);
        Assert.True(me.Code == 200, $"GET /api/auth/me failed: {me}");
        Assert.Contains("After", me.Body);
        Assert.Contains("Changed", me.Body);

        // The resident profile is kept in sync.
        var profile = await _api.GetAsync("/api/residents/me", login.AccessToken);
        Assert.True(profile.Code == 200, $"GET /api/residents/me failed: {profile}");
        Assert.Contains("After", profile.Body);
        Assert.Contains("Changed", profile.Body);

        // The change is scoped to the caller: another user's identity is untouched.
        var other = await _api.LoginAsync("manager@pmp.com", "Manager123!");
        var otherMe = await _api.GetAsync("/api/auth/me", other.AccessToken);
        Assert.True(otherMe.Code == 200, $"GET /api/auth/me failed for manager: {otherMe}");
        Assert.DoesNotContain("After", otherMe.Body);
        Assert.Contains("Anna", otherMe.Body);
    }
}
