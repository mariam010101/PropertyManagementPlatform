using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Auth.Options;
using PMP.Modules.Auth.Services;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-001 / IMP-002 — the access token is the seam the API's authorization policies
/// trust. These tests prove the identity and role claims policies depend on, and that
/// the token is signed with the configured issuer/audience/key.
/// </summary>
public class AuthTokenServiceTests
{
    private const string Issuer = "PMP";
    private const string Audience = "PMP";
    private const string Key = "PMP_UNIT_TEST_SIGNING_KEY_0123456789ABCDEFG";

    private static TokenService CreateService(int expiryMinutes = 15) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            Key = Key,
            AccessTokenExpiryMinutes = expiryMinutes,
        }));

    private static ApplicationUser NewUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "anna.manager@pmp.test",
        Email = "anna.manager@pmp.test",
        FirstName = "Anna",
        LastName = "Manager",
        IsActive = true,
    };

    private static ClaimsPrincipal Validate(string token, string key = Key)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuerSigningKey = true,
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }

    [Fact]
    public void CreateAccessToken_CarriesSubjectEmailDisplayNameAndRoles()
    {
        var user = NewUser();

        var principal = Validate(CreateService().CreateAccessToken(user, [AppRoles.PropertyManager]));

        Assert.Equal(user.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(user.Email, principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Anna Manager", principal.FindFirst("name")?.Value);
        Assert.True(principal.IsInRole(AppRoles.PropertyManager));
        Assert.False(principal.IsInRole(AppRoles.Administrator));
    }

    [Fact]
    public void CreateAccessToken_HonorsConfiguredExpiry()
    {
        var token = CreateService(expiryMinutes: 30).CreateAccessToken(NewUser(), [AppRoles.Resident]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // The payload carries no "nbf", so the practical check is the absolute expiry.
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalMinutes, 29.0, 30.01);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
    }

    [Fact]
    public void CreateAccessToken_CannotBeValidatedWithADifferentSigningKey()
    {
        var token = CreateService().CreateAccessToken(NewUser(), [AppRoles.Resident]);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(token, key: "PMP_OTHER_SIGNING_KEY_0123456789ABCDEFGHIJKLM"));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsDistinctHighEntropyValues()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.True(Convert.FromBase64String(first).Length >= 64);
    }
}
