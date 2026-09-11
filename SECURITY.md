# Security Policy

## Supported versions

Security fixes are applied to the current `main` branch. There are no maintained release branches yet.

## Reporting a vulnerability

Please do **not** open a public issue for a suspected vulnerability. Instead, report it privately through
GitHub's **Report a vulnerability** feature (Security → Advisories → *Report a vulnerability*) on this
repository. Include the affected endpoint or component, reproduction steps, and the impact you believe it has.

You will get an acknowledgement, an assessment, and — where the issue is confirmed — a fix or a documented
mitigation. Findings are also recorded in [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md)
so they are not lost.

## Security posture (implemented)

| Control | Implementation |
| --- | --- |
| Authentication | ASP.NET Core Identity + JWT bearer access tokens with rotating refresh tokens ([ADR-0003](docs/adr/0003-identity-jwt-auth.md)). |
| Password policy | Minimum 8 characters with upper, lower, digit and non-alphanumeric, 4 unique characters. |
| Brute-force protection | Account lockout after 5 failed attempts for 15 minutes. |
| Email verification | Required before sign-in (`RequireConfirmedEmail`). |
| Authorization | Policy-based RBAC plus service-level ownership scoping; deny-by-default ([ADR-0004](docs/adr/0004-rbac-roles-and-policies.md)). |
| Audit | Authentication and role-change events are persisted (`AuthEvent`). |
| Transport | HTTPS redirection; CORS restricted to the frontend dev origins. |
| Data boundaries | One `DbContext` per module, table ownership enforced ([ADR-0006](docs/adr/0006-module-boundaries-and-ownership.md), [ADR-0011](docs/adr/0011-sqlite-single-file-module-boundaries.md)). |

## Configuration and secrets

- **`src/PMP.Api/appsettings.json` is git-ignored** and must hold the real `ConnectionStrings:DefaultConnection`
  and `Jwt:Key`. Create it from [`src/PMP.Api/appsettings.example.json`](src/PMP.Api/appsettings.example.json).
- `Jwt:Key` must be a random secret of **at least 32 characters**; the application refuses to start otherwise.
- Never commit `.env` files, local SQLite databases (`pmp.db*`), uploaded documents, or logs — these are
  covered by [`.gitignore`](.gitignore).
- The demo accounts created by the seeder (`admin@pmp.com`, `manager@pmp.com`, …) are **development fixtures
  with well-known passwords**. They must never be used in a deployed environment. Production seeding must use
  distinct, secret-provided credentials, or be disabled.

## Known limitations (recorded, not hidden)

This is an in-progress platform. The following are **known and intentionally documented** rather than claimed
as secured:

- Payments are **simulated**; there is no payment-provider integration ([GAP-012](docs/IMPLEMENTATION_GAP_ANALYSIS.md)).
- The email channel is **record-only** — no email transport exists ([GAP-005](docs/IMPLEMENTATION_GAP_ANALYSIS.md)).
- Role revocation does not invalidate an already-issued access token before its 15-minute expiry
  ([GAP-003](docs/IMPLEMENTATION_GAP_ANALYSIS.md)).
- No independent security/NFR review has been performed yet; it is tracked as `IMP-044`.
- There is no CI/CD pipeline or deployment packaging yet ([GAP-009](docs/IMPLEMENTATION_GAP_ANALYSIS.md)).

Do not treat the absence of an entry above as proof that an area is free of issues.
