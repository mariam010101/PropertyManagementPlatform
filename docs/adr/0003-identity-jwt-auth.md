# ASP.NET Core Identity with JWT access + refresh tokens

**Status:** accepted

Authentication uses **ASP.NET Core Identity** for user storage/password hashing plus **JWT bearer access tokens** (short-lived) and **rotating refresh tokens** (persisted in the auth schema), so the same mechanism serves the web SPA and the future mobile clients (FR-MOBILE-005).

**Considered options**

- **Cookie-based sessions** — rejected: cookies are awkward for a token API and mobile clients.
- **External identity provider** (e.g., Azure AD B2C, Auth0) — rejected: no external identity requirement; keeps the platform self-contained.

**Consequences**

- The API is stateless per request (token validated from claims).
- Refresh tokens are rotated on use and revoked on logout; lockout and email verification are enforced by Identity.
