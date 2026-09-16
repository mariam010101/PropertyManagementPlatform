using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PMP.Modules.Auth.Contracts;
using PMP.Modules.Auth.Data;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Auth.Options;
using PMP.Modules.Auth.Services;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-001 / IMP-002 — behaviour of the auth service that is not exercised by the
/// token tests or the end-to-end suite: auth-event persistence, role-change audit
/// and refresh-token revocation on deactivation. Uses the real ASP.NET Identity
/// stack against an in-memory <see cref="AuthDbContext"/>.
/// </summary>
public class AuthServiceTests
{
    private const string TestKey = "PMP_UNIT_TEST_SIGNING_KEY_0123456789ABCDEFG";

    private sealed record Harness(
        AuthService Service,
        AuthDbContext Db,
        UserManager<ApplicationUser> Users);

    private static async Task<Harness> Arrange()
    {
        var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-{Guid.NewGuid():N}")
            .Options);

        var users = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, IdentityRole<Guid>, AuthDbContext, Guid>(db),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        // The real host registers default token providers (AddDefaultTokenProviders).
        // Register a deterministic stand-in so email-confirmation tokens can be generated.
        users.RegisterTokenProvider(TokenOptions.DefaultProvider, new StubTokenProvider());

        var roles = new RoleManager<IdentityRole<Guid>>(
            new RoleStore<IdentityRole<Guid>, AuthDbContext, Guid>(db),
            Array.Empty<IRoleValidator<IdentityRole<Guid>>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole<Guid>>>.Instance);

        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var tokens = new TokenService(Options.Create(new JwtOptions
        {
            Issuer = "PMP",
            Audience = "PMP",
            Key = TestKey,
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7,
        }));

        // The tested paths (register, role assignment, deactivation) do not use
        // SignInManager, so a null dependency is safe for this focused seam.
        var service = new AuthService(users, null!, roles, db, tokens, Options.Create(new JwtOptions
        {
            Issuer = "PMP",
            Audience = "PMP",
            Key = TestKey,
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7,
        }));

        return new Harness(service, db, users);
    }

    private static RegisterRequest NewRegistration(string email = "anna.resident@pmp.test") => new()
    {
        Email = email,
        Password = "Resident123!",
        FirstName = "Anna",
        LastName = "Resident",
        PhoneNumber = "+37400000000",
    };

    [Fact]
    public async Task Register_PersistsAuthEvent_AndAssignsResidentRole()
    {
        var harness = await Arrange();

        var result = await harness.Service.RegisterResidentAsync(NewRegistration(), ip: "127.0.0.1");

        Assert.True(result.Succeeded, result.Error);
        Assert.NotEqual(Guid.Empty, result.Data!.UserId);
        Assert.NotNull(result.Data!.RefreshToken);

        // FR-AUTH-007: every auth event is persisted.
        Assert.Contains(harness.Db.AuthEvents, e =>
            e.EventType == "Register" && e.Email == "anna.resident@pmp.test" && e.IpAddress == "127.0.0.1");

        // Residents self-register with the Resident role.
        var roles = await harness.Service.GetUserRolesAsync(result.Data!.UserId);
        Assert.Contains(AppRoles.Resident, roles);
    }

    [Fact]
    public async Task SetUserRoles_PersistsRolesUpdatedAuditEvent()
    {
        var harness = await Arrange();
        var registered = await harness.Service.RegisterResidentAsync(NewRegistration(), ip: null);
        Assert.True(registered.Succeeded, registered.Error);

        var actor = Guid.NewGuid();
        var result = await harness.Service.SetUserRolesAsync(actor, registered.Data!.UserId,
            new SetUserRolesRequest { Roles = new[] { AppRoles.Resident, AppRoles.PropertyManager } });

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(harness.Db.AuthEvents, e =>
            e.EventType == "RolesUpdated" && e.UserId == registered.Data!.UserId);

        var roles = await harness.Service.GetUserRolesAsync(registered.Data!.UserId);
        Assert.Contains(AppRoles.PropertyManager, roles);
        Assert.Contains(AppRoles.Resident, roles);
    }

    [Fact]
    public async Task DeactivateUser_RevokesRefreshTokens_AndPersistsAuditEvent()
    {
        var harness = await Arrange();
        var registered = await harness.Service.RegisterResidentAsync(NewRegistration(), ip: null);
        Assert.True(registered.Succeeded, registered.Error);
        Assert.True(harness.Db.RefreshTokens.Any(t => t.Token == registered.Data!.RefreshToken && t.RevokedAt == null));

        var actor = Guid.NewGuid();
        var result = await harness.Service.DeactivateUserAsync(actor, registered.Data!.UserId);

        Assert.True(result.Succeeded, result.Error);

        // A deactivated account cannot mint new access tokens from an existing refresh token.
        var stored = harness.Db.RefreshTokens.Single(t => t.Token == registered.Data!.RefreshToken);
        Assert.NotNull(stored.RevokedAt);

        Assert.Contains(harness.Db.AuthEvents, e =>
            e.EventType == "AccountDeactivated" && e.UserId == registered.Data!.UserId);
    }

    private sealed class StubTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user)
            => Task.FromResult("stub-token");

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager, ApplicationUser user)
            => Task.FromResult(true);

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
            => Task.FromResult(true);
    }
}
