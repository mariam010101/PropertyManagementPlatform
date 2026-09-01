using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PMP.Api.Services;

/// <summary>
/// Resolves the authenticated actor's id and roles from the JWT claims.
/// </summary>
public class CurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid Id
    {
        get
        {
            var sub = Find(JwtRegisteredClaimNames.Sub) ?? Find(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public IReadOnlyList<string> Roles =>
        _http.HttpContext?.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    private string? Find(string claimType) => _http.HttpContext?.User.FindFirst(claimType)?.Value;
}
