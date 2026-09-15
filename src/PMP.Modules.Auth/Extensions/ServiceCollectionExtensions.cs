using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PMP.Modules.Auth.Data;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Auth.Options;
using PMP.Modules.Auth.Services;
using PMP.Shared.Common;

namespace PMP.Modules.Auth.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<AuthDbContext>(options => options.UseSqlite(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be configured and at least 32 characters long.");
        }

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Security posture (locked during grilling):
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };

                // IMP-031 / GAP-003: a revoked or demoted role must stop authorizing on the
                // next request. Access tokens embed role claims at issue time and are otherwise
                // valid for up to 15 minutes, so re-load the caller's current roles (and active
                // state) from the identity store on every request and replace any stale claims.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();

                        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (userIdValue is null || !Guid.TryParse(userIdValue, out var userId))
                        {
                            context.Fail("Invalid token subject.");
                            return;
                        }

                        var user = await userManager.FindByIdAsync(userId.ToString());
                        if (user is null || !user.IsActive)
                        {
                            context.Fail("The account is deactivated or no longer exists.");
                            return;
                        }

                        if (context.Principal?.Identity is not ClaimsIdentity identity)
                        {
                            return;
                        }

                        var roleClaimType = identity.RoleClaimType;
                        foreach (var stale in identity.Claims.Where(c => c.Type == roleClaimType).ToList())
                        {
                            identity.RemoveClaim(stale);
                        }

                        var currentRoles = await userManager.GetRolesAsync(user);
                        foreach (var role in currentRoles)
                        {
                            identity.AddClaim(new Claim(roleClaimType, role));
                        }
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.AdministratorOnly, p => p.RequireRole(AppRoles.Administrator));
            options.AddPolicy(AppPolicies.StaffOnly, p => p.RequireRole(AppRoles.PropertyManager, AppRoles.Technician));
            options.AddPolicy(AppPolicies.ManagerOrAdmin, p => p.RequireRole(AppRoles.PropertyManager, AppRoles.Administrator));
            options.AddPolicy(AppPolicies.ResidentOrStaff, p => p.RequireRole(AppRoles.Resident, AppRoles.PropertyManager, AppRoles.Technician, AppRoles.Administrator));
            options.AddPolicy(AppPolicies.Authenticated, p => p.RequireAuthenticatedUser());
            options.AddPolicy(AppPolicies.Financial, p => p.RequireRole(AppRoles.Accountant, AppRoles.PropertyManager, AppRoles.Administrator));
            options.AddPolicy(AppPolicies.AccountantOrAdmin, p => p.RequireRole(AppRoles.Accountant, AppRoles.Administrator));
        });

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
