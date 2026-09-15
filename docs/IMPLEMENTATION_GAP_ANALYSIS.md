# PMP Implementation Gap Analysis

**Last Updated:** 2026-09-15 22:09 UTC (2026-09-16 02:09 Asia/Yerevan)
**Companion document:** [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)
**Method:** every item below was identified by comparing approved requirements/ADRs
([`docs/adr/`](docs/adr/), [`docs/requirements-compliance.md`](docs/requirements-compliance.md),
[`plans/pmp-development-plan.md`](plans/pmp-development-plan.md)) against the verified code baseline recorded
in §1 of the plan. Gaps are recorded, never silently dropped.

Priority key: **P1** = required for MVP/verification, **P2** = post-MVP required, **P3** = hardening/tech debt.
Status uses the plan vocabulary (`GAP`, `NOT_STARTED`, `IN_PROGRESS`, `BLOCKED`, `RESOLVED`).

| GAP | Title | Module | REQ / ADR | Priority | Related task | Status |
| --- | --- | --- | --- | --- | --- | --- |
| GAP-001 | Automated test coverage improved but still incomplete: 101 tests after IMP-040 and every module now has service-level tests; no background-job/attachment tests, no frontend tests | Cross-module | all FR sets; ADR-0005 | P1 | IMP-040 (done), IMP-041 | IN_PROGRESS |
| GAP-002 | Mobile platform access not implemented (no native app or PWA; responsive web only) | Mobile | FR-MOBILE-001..005; ADR-0007, ADR-0012 | P2 | IMP-027 | NOT_STARTED (deferred) |
| GAP-003 | Role revocation does not apply to already-issued 15-minute JWTs | Auth | FR-RBAC-006, BRULE-RBAC-006; ADR-0003/0004 | P1 | IMP-031 | RESOLVED |
| GAP-004 | No dedicated owner "edit my profile" endpoint that mutates Identity name fields | Auth | FR-AUTH-005, BRULE-AUTH-006 | P1 | IMP-030 | RESOLVED |
| GAP-005 | Email channel is record-only: notifications store channel/delivery status but no transport exists (no SMTP/MailKit/SendGrid/DependencyInjection of `IEmailSender`) | Communication | FR-COM-001, FR-COM-006 | P2 | IMP-023 | GAP |
| GAP-006 | "Active resident must have a lease" is a process convention, not an enforced invariant | Lease / Resident | BRULE-LEASE-001 | P2 | IMP-033 | GAP |
| GAP-007 | Role validation missing on two write paths: maintenance assignment does not verify the assignee is a Technician; property create does not verify the manager is a Property Manager | Maintenance / Property | BRULE-MNT-003, BRULE-PROP-003; ADR-0006 | P3 | IMP-034 | GAP |
| GAP-008 | No distinct "suspended" account state (only active/inactive) — requires a product decision on BRULE-AUTH-005 wording | Auth | BRULE-AUTH-005 | P3 | IMP-032 | BLOCKED |
| GAP-009 | No CI/CD pipeline and no deployment packaging (no `.github/`, Dockerfile, compose, environment matrix, production config/secrets strategy) | DevOps | — (ADR-0011 deployment surface) | P2 | IMP-042, IMP-043 | GAP |
| GAP-010 | Documentation drift: `plans/pmp-development-plan.md` points the SPA at `src/frontend/` (actual: [`frontend/`](frontend)); ADR-0007 remains `accepted` although ADR-0012 re-scoped the roadmap; MVP definition differs between ADR-0007 and [`README.md`](README.md); aggregate entity is `ManagedProperty` while other text says `Property`; compliance audit not refreshed since 2026-09-03 | Docs | ADR-0007, ADR-0012 | P3 | IMP-051 | RESOLVED |
| GAP-011 | No BRULE pages published for BR-009 (Booking) / BR-010 (Security), leaving those FR sets without business-rule traceability | Docs / Requirements | FR-BOOK-001..007, FR-SEC-001..007 | P3 | IMP-035 | GAP |
| GAP-012 | Payment is simulated: no payment-provider integration, so FR-PAY-002 "pay electronically" is satisfied only in the demo sense (no reversal/refund, no idempotency keys, no provider webhooks) | Payment | FR-PAY-002, FR-PAY-003; ADR-0012 (trade-off) | P2 | IMP-020 | GAP |
| GAP-015 | Single-currency assumption: a payment request now stores and displays `Currency`, but there is no FX, no per-property currency configuration, and balances/reports sum across currencies without conversion | Payment | FR-PAY-001..007; ADR-0012 | P3 | IMP-020 | GAP |
| GAP-016 | No financial-report export: the report/reconciliation endpoints return JSON only; no CSV/PDF export format is defined or tested | Payment | FR-ACCT-002; ADR-0012 | P3 | IMP-021 | GAP |
| GAP-017 | `GET /api/auth/me` returned only `userId` + `roles`, so the client could not render the signed-in user's name/email after a reload | Auth | FR-AUTH-005; ADR-0003 | P1 | — (resolved) | RESOLVED |
| GAP-018 | No app shell, layout route or shared navigation: pages rendered bare containers, navigation was hand-rolled per page and there was no permission-denied state | Frontend | FR-RBAC-002/004; ADR-0004 | P1 | IMP-007, IMP-026 | RESOLVED |
| GAP-019 | No design-system foundation (tokens, shared component/table/form primitives, icon family); styling was a single utility stylesheet | Frontend | — | P2 | IMP-007, IMP-026 | RESOLVED |
| GAP-020 | No dashboard statistics: there was no aggregate endpoint and the dashboard page performed no data fetch (static link grid) | Frontend / cross-module | no requirement IDs exist | P2 | IMP-007, IMP-052 | RESOLVED |
| GAP-021 | Frontend API coverage was incomplete vs the backend: the typed client omitted detail/update/availability/attachment calls the endpoints already expose (Property, Resident, Maintenance, Lease and Booking now all covered) | Property, Resident, Maintenance, Lease, Booking | FR-PROP/RES/MNT/LEASE/BOOK sets; ADR-0006 | P2 | IMP-003, IMP-004, IMP-005, IMP-022, IMP-024, IMP-026 | RESOLVED |
| GAP-022 | No password reset/change UI or client methods although the backend endpoints exist | Auth | FR-AUTH-003, FR-AUTH-004; ADR-0003 | P2 | IMP-001, IMP-007 | RESOLVED |
| GAP-013 | Platform NFRs (performance, availability, backup/restore of `pmp.db`, observability) have no requirement IDs and no verification — pure traceability gap | Cross-cutting | none exist | P3 | IMP-044, IMP-052 | GAP |
| GAP-014 | Maintenance notifications carry no request context (the request title is absent from the persisted payload), so a resident cannot identify which request a notification refers to | Maintenance / Communication | FR-MNT-005, FR-COM-001 | P3 | IMP-040 | GAP |
| GAP-023 | Internal `docs/` links are written repo-root-relative and with `:line` suffixes, so they do not resolve when rendered on GitHub (which resolves relative to the containing file and uses `#Lnnn`) | Docs | — | P3 | IMP-051, IMP-052 | GAP |
| GAP-024 | Resident first-unit assignment is Administrator-only: manager scoping grants access only to residents already occupying one of the manager's units, so a newly registered resident cannot be seen or assigned by a Property Manager | Resident | FR-RES-003, BRULE-RES-002; ADR-0008 | P2 | IMP-004 | GAP (decision-required) |
| GAP-025 | No invoice PDF/document export: payment invoices render as an on-screen details view only; no PDF/CSV invoice export format is defined or produced | Payment | FR-PAY-003; ADR-0012 | P3 | IMP-053 | RESOLVED |

---

## Gap detail

### GAP-001 — Automated test coverage incomplete (background jobs, attachments, frontend)
- **Affected module:** cross-module (the three hosted jobs; attachment upload paths; frontend).
- **Related requirement/ADR:** all FR sets; ADR-0005 (service `Result` pattern makes service-level testing the intended seam).
- **Current state:** `IMP-006` (2026-09-10) added 12 API integration tests, `IMP-020`/`IMP-021` added Payment
  unit + API tests, and **`IMP-040` (2026-09-11) added module-level service tests for every remaining module**
  (`AuthTokenServiceTests`, `ResidentServiceTests`, `LeaseServiceTests`, `CommunicationServiceTests`,
  `BookingServiceTests`, `SecurityServiceTests`) plus shared test doubles. The suite is now **101 passed /
  0 failed** (14 test files) and the TDD workflow is documented in [`docs/TESTING.md`](docs/TESTING.md).
  **Still uncovered:** the maintenance 7-day auto-close job, the lease-expiry and payment due-date background
  sweeps, attachment upload, the 7-day maintenance reopen path, and all frontend behaviour.
- **What remains:** add tests for the three hosted services and the attachment paths (reusing the
  [`TestDoubles.cs`](tests/PMP.Tests/TestDoubles.cs) pattern), and stand up frontend testing (`IMP-041`).
- **Priority:** P1.
- **Related tasks:** `IMP-040` (per-module expansion — done), `IMP-041` (frontend), `IMP-042` (CI).

### GAP-002 — BR-012 mobile platform not implemented
- **Affected module:** Mobile (no project exists).
- **Related requirement/ADR:** FR-MOBILE-001..005; ADR-0007 and ADR-0012 both keep it out of the delivered scope.
- **Current state:** React SPA is responsive and the API is JWT-based, but there is no PWA manifest/service
  worker and no native client.
- **What remains:** product decision (PWA vs native) — see the open question in
  [`plans/pmp-development-plan.md`](plans/pmp-development-plan.md:136) — then build the client and verify
  FR-MOBILE-001..005.
- **Priority:** P2 (must not start before IMP-006/IMP-040 are green).
- **Related task:** `IMP-027`.

### GAP-003 — Role-revocation latency (RESOLVED)
- **Affected module:** Auth (RBAC).
- **Related requirement/ADR:** FR-RBAC-006, BRULE-RBAC-006; ADR-0003, ADR-0004.
- **Current state (before):** role changes were written immediately and audited, but an already-issued access token
  kept the old role claims until it expired (15-minute TTL per [`JwtOptions.cs`](src/PMP.Modules.Auth/Options/JwtOptions.cs));
  deactivation additionally revoked refresh tokens.
- **Resolution (2026-09-15, `IMP-031`):** the JWT bearer handler now re-loads the caller's current roles (and active
  state) from the identity store on every request and replaces any stale role claims in the principal
  (`OnTokenValidated` in [`ServiceCollectionExtensions.cs`](src/PMP.Modules.Auth/Extensions/ServiceCollectionExtensions.cs:56)).
  A revoked or demoted role therefore takes effect on the next request (≤1 request latency); deactivated or deleted
  accounts are rejected immediately as well.
- **Verification:** new integration test
  [`RoleRevocationApiTests.RemovedRole_StopsAuthorizing_OnTheNextRequest`](tests/PMP.Tests/Integration/RoleRevocationApiTests.cs:17)
  provisions an isolated technician, proves the issued token authorizes `GET /api/maintenance`, removes the
  Technician role via `PUT /api/auth/users/{id}/roles`, then asserts the old token is rejected with 403.
  Full suite **117 passed / 0 failed**; build 0 warnings / 0 errors.
- **Priority:** P1 — resolved.
- **Related task:** `IMP-031`.

### GAP-004 — No owner self-service profile update (RESOLVED)
- **Affected module:** Auth (+ Resident for the profile data).
- **Related requirement/ADR:** FR-AUTH-005, BRULE-AUTH-006.
- **Current state (before):** residents read their own profile via `GET /api/residents/me`; edits happened through
  the manager/admin path `PUT /api/residents/{residentId}`. No endpoint let a user update their own Identity name
  fields.
- **Resolution (2026-09-15, `IMP-030`):** added `PUT /api/auth/me` (authenticated) which updates the caller's
  Identity `FirstName`/`LastName`/`PhoneNumber`, records a `ProfileUpdated` auth event, and keeps the resident
  profile in sync via `IResidentService.UpdateMyProfileAsync` (no-op for staff accounts). The endpoint is scoped
  to the caller by construction — it takes no user id parameter, so another user's data cannot be addressed.
  See [`AuthController.UpdateMyProfile`](src/PMP.Api/Controllers/AuthController.cs:179) and
  [`AuthService.UpdateMyProfileAsync`](src/PMP.Modules.Auth/Services/AuthService.cs:93).
- **Verification:** [`ProfileUpdateApiTests`](tests/PMP.Tests/Integration/ProfileUpdateApiTests.cs:11) asserts an
  unauthenticated request is rejected (401), a self-edit is reflected in both `/api/auth/me` and
  `/api/residents/me`, and a different user's identity is untouched. Full suite **119 passed / 0 failed**;
  build 0 warnings / 0 errors.
- **Priority:** P1 — resolved.
- **Related task:** `IMP-030`.

### GAP-005 — Email channel is record-only
- **Affected module:** Communication.
- **Related requirement/ADR:** FR-COM-001, FR-COM-006; ADR-0012.
- **Current state:** notification rows persist `NotificationChannel` (`InApp`/`Email`) and a delivery status,
  and in-app retrieval works; a scan of [`src/`](src) finds no `Smtp`/`MailKit`/`SendGrid`/`IEmailSender`
  usage, so no email is ever transmitted. [`docs/requirements-compliance.md`](docs/requirements-compliance.md:156)
  marks FR-COM-006 🟢 MET, which overstates the implementation (conflict C-4 in the plan).
- **What remains:** either implement a provider-backed `IEmailSender` (config-driven, disabled by default) and
  record real delivery outcomes, or explicitly descope email delivery with an ADR and adjust the compliance audit.
- **Priority:** P2.
- **Related task:** `IMP-023`.

### GAP-006 — Lease invariant not enforced
- **Affected module:** Lease / Resident.
- **Related requirement/ADR:** BRULE-LEASE-001; ADR-0012.
- **Current state:** leases can be created for units/residents, but nothing prevents an active resident from
  existing without an active lease; the compliance audit records this as process-level.
- **What remains:** decide the enforcement point (lease create/terminate, resident assignment, or a periodic
  check), handle pre-existing rows, and test the rejection path.
- **Priority:** P2.
- **Related task:** `IMP-033`.

### GAP-007 — Missing role validation on write paths
- **Affected module:** Maintenance, Property.
- **Related requirement/ADR:** BRULE-MNT-003 (one responsible technician), BRULE-PROP-003 (assigned manager); ADR-0006.
- **Current state:** assignment accepts any staff user id; property create accepts any manager id without
  checking the target user actually holds the role (noted as a hardening item in the compliance audit).
- **What remains:** validate the role at the service boundary, return the standard `Result` failure, and add tests.
- **Priority:** P3.
- **Related task:** `IMP-034`.

### GAP-008 — Account-state model underspecified (blocked)
- **Affected module:** Auth.
- **Related requirement/ADR:** BRULE-AUTH-005.
- **Current state:** the model has active/inactive only; there is no distinct "suspended" state.
- **What remains:** product decision required — is active/inactive sufficient, or must "suspended" be its own
  state with distinct semantics (e.g. cannot log in but sessions/messages preserved differently)?
  Implementation is intentionally **not started** to avoid inventing a requirement.
- **Priority:** P3.
- **Related task:** `IMP-032` (BLOCKED — needs clarification, see plan §15 rule).

### GAP-009 — No CI/CD or deployment packaging
- **Affected module:** DevOps / repository root.
- **Related requirement/ADR:** none (engineering enabler); ADR-0011 defines the SQLite deployment surface.
- **Current state:** no `.github/` workflows, no Dockerfile/compose for the current stack, no documented
  environment matrix; the app relies on `appsettings.Development.json` + seed data and a local `pmp.db`.
  A **stale, previously git-ignored `docker-compose.yml`** (PostgreSQL 16 with default `postgres/postgres`
  credentials — the pre-SQLite provisioning superseded by ADR-0011) was found untracked during the
  2026-09-11 hygiene pass and **removed**; the `.gitignore` rule that hid it was replaced with a
  `docker-compose.override.yml` rule so a current packaging file (IMP-043) can be committed.
- **What remains:** CI running build + `dotnet test` + frontend lint/build on push; a packaging path that runs
  migrations and seed without dev-only config; documented secrets/production configuration.
- **Priority:** P2.
- **Related tasks:** `IMP-042`, `IMP-043`.

### GAP-010 — Documentation drift (RESOLVED)
- **Affected module:** docs / repo layout.
- **Related requirement/ADR:** ADR-0007 (status not updated after ADR-0012), ADR-0012.
- **Current state (before):** see conflicts C-1..C-3 in §1 of the plan — `src/frontend/` vs [`frontend/`](frontend);
  ADR-0007 `accepted` while ADR-0012 supersedes its roadmap consequences; MVP definition mismatch between
  ADR-0007 (MVP excludes the RBAC admin UI) and the older README (RBAC admin UI listed as MVP);
  `ManagedProperty` vs `Property` naming; compliance audit last refreshed 2026-09-03 and inaccurate on
  FR-COM-006 (C-4).
- **Resolution (2026-09-11, `IMP-051`):** README rewritten and aligned to ADR-0007/ADR-0012; ADR-0007 marked
  *partially superseded by ADR-0012*; the historical development plan carries a status banner and its
  `src/frontend/` path drift is corrected; the glossary no longer says "four module DbContexts"; the compliance
  audit was corrected for FR-COM-006 (and its roll-up recomputed to 71 MET / 3 PARTIAL / 5 NOT MET). The
  `ManagedProperty` naming is documented in the README as "code is authoritative".
- **Verification:** all relative links in the rewritten README/docs index resolved; `dotnet test` unaffected
  (46 passed / 0 failed).
- **Priority:** P3 — resolved.
- **Related task:** `IMP-051`.

### GAP-011 — No business-rule documentation for BR-009/BR-010
- **Affected module:** Booking, Security.
- **Related requirement/ADR:** FR-BOOK-001..007, FR-SEC-001..007 (no `BRULE-*` pages published).
- **Current state:** implementation exists (facilities/availability/cancellation window; visitor check-in/out,
  access grants with expiry, logs) but there is no published rule set, so overlap/cancellation/expiry policies
  are only traceable to code.
- **What remains:** publish the BRULE pages in the Confluence `PMP` space, or record an explicit decision that
  these modules are governed by their FR pages only.
- **Priority:** P3.
- **Related task:** `IMP-035`.

### GAP-012 — Simulated payments
- **Affected module:** Payment.
- **Related requirement/ADR:** FR-PAY-002, FR-PAY-003; ADR-0012 documents the trade-off.
- **Current state:** `IMP-020` (2026-09-10) completed the payment **request/order and obligation** side — every
  resident obligation is a persisted request carrying resident/amount/currency/purpose/due date/status/creation
  date, the backend calculates the amount currently due per user, and declined attempts are recorded as
  `Failed` transactions so the history is complete. The actual money movement is still simulated: the
  "processor" completes a valid charge and declines a charge above the outstanding balance; there is no
  provider integration, so no refunds, reversals, idempotency keys, or webhook reconciliation exist.
- **What remains:** record the decision (simulated-for-demo vs provider integration), and if integration is
  required, add provider abstraction + idempotency + reconciliation tests. This was explicitly **out of scope**
  for the payment-request task.
- **Priority:** P2.
- **Related task:** `IMP-020`.

### GAP-014 — Maintenance notifications lack request context
- **Affected module:** Maintenance (content) / Communication (storage).
- **Related requirement/ADR:** FR-MNT-005, FR-COM-001 (notify residents on business events).
- **Current state:** the resident does receive persisted notifications for maintenance events — verified by
  `IMP-006` (notification count increases across the lifecycle) — but the alert text does not include the request
  title: an integration assertion on the request title failed against the stored payload, and the message strings
  are composed at the call sites of
  [`NotifyResidentAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:404) rather than carrying
  request context. The resident therefore cannot tell which request an alert refers to.
- **What remains:** include short request context (title or request number) in the notification title/message, and
  assert it in the integration suite. Exact payload wording has not been fully inspected, so the finding is limited
  to "request title is absent".
- **Priority:** P3.
- **Related task:** `IMP-040` (test) / a small Maintenance content fix.

### GAP-013 — Platform NFRs have no requirements or verification
- **Affected module:** cross-cutting.
- **Related requirement/ADR:** none exist (traceability gap — do not invent IDs).
- **Current state:** no performance/availability/backup/observability requirements are documented; there is no
  structured logging, health-check endpoint, or `pmp.db` backup/restore procedure.
- **What remains:** either obtain requirement IDs from the product owner or record an explicit decision to treat
  these as engineering standards; then verify whichever is agreed (health check, logging, backup procedure).
- **Priority:** P3.
- **Related tasks:** `IMP-044`, `IMP-052`.

### GAP-023 — Internal documentation links do not resolve on GitHub
- **Affected module:** docs.
- **Related requirement/ADR:** none (documentation quality).
- **Current state:** links inside `docs/` are written **repo-root-relative** and with `:line` suffixes
  (for example a link target of `src/PMP.Api/Program.cs:35`, or `docs/adr/`). GitHub resolves a relative link
  against the containing file and expects `#Lnnn` for lines, so from `docs/` these targets render as broken;
  they are authored for the in-editor agent workflow and are consistent with the project's operating rules.
- **What remains:** decide the convention and apply it consistently — either rewrite the `docs/` links as
  file-relative paths (and `#Lnnn` anchors) so they render on GitHub, or keep the root-relative convention and
  accept that GitHub markdown is a secondary surface (the plan is mirrored to Confluence). Links in the
  repository-root and top-level documents (README, CONTRIBUTING, SECURITY, `docs/README.md`,
  `docs/traceability.md`) are already file-relative and render correctly.
- **Priority:** P3.
- **Related tasks:** `IMP-051`, `IMP-052`.

### GAP-015 — Single-currency assumption
- **Affected module:** Payment.
- **Related requirement/ADR:** FR-PAY-001..007; ADR-0012 (Payment module scope).
- **Current state:** a payment request now stores the ISO-4217 `Currency` (defaulting to `USD`) and the UI
  formats each amount in that currency, but there is no foreign-exchange handling, no per-property currency
  configuration, and the financial report/reconciliation totals sum amounts across currencies without
  conversion, so mixed-currency data would be added arithmetically.
- **What remains:** decide whether multi-currency is in scope; if it is, add a currency source of truth
  (property/lease), reject or convert mixed-currency aggregation, and test it. If it is not, record that the
  platform is single-currency by design.
- **Priority:** P3 (the demo deployment is single-currency).
- **Related task:** `IMP-020`.

### GAP-016 — No financial-report export
- **Affected module:** Payment (BR-011 Accounting).
- **Related requirement/ADR:** FR-ACCT-002 ("generate/export financial reports"); ADR-0012.
- **Current state:** `GetFinancialReportAsync`/`GetReconciliationAsync` return JSON with collected,
  outstanding, overdue and reconciliation figures, and the Accountant access boundaries are now tested, but no
  export format (CSV/PDF) is defined or produced.
- **What remains:** define the export format, implement an export endpoint restricted to the `Financial`
  policy, and test it.
- **Priority:** P3.
- **Related task:** `IMP-021`.

### GAP-017 — `/api/auth/me` returned no user identity (RESOLVED)
- **Affected module:** Auth + the client app shell (all pages).
- **Related requirement/ADR:** FR-AUTH-005, BRULE-AUTH-006; ADR-0003.
- **Current state (before):** `AuthController.Me()` returned an anonymous `{ userId, roles }`; the client
  therefore blanked `firstName`/`lastName`/`email` on every reload, so the shell could not name the
  signed-in user (and staff had no profile endpoint to fall back on).
- **Resolution (2026-09-10):** a typed `MeResponse` (id, email, first/last name, roles) is returned by
  `GET /api/auth/me` via `AuthService.GetCurrentUserAsync()`, sourced from the Auth user record only (no
  cross-module dependency). The client `me()` is typed and `AuthContext` hydrates from it. Read-only:
  profile *editing* remains IMP-030.
- **Verification:** API integration theory `SeededUser_CanLogin_AndMeReturnsOwnIdentity` asserts each seeded
  role receives its own email, first name and role from `/me`; suite 47 passed / 0 failed, build 0 warnings.
- **Priority:** P1 — resolved.
- **Related task:** none (UI-readiness follow-up of the UI-blocking gap audit).

### GAP-018 — No app shell / navigation foundation (RESOLVED)
- **Affected module:** Frontend (all pages).
- **Current state (before):** `App.tsx` mapped bare pages with no layout route; only the dashboard rendered a
  topbar and sign-out; navigation was hand-rolled per page; `ProtectedRoute` silently redirected to `/`.
- **Resolution (2026-09-10):** a single declarative manifest (`frontend/src/navigation.tsx`) now drives both
  the sidebar and the route guards. `AppLayout.tsx` provides the shell — persistent sidebar (desktop),
  collapsible drawer with scrim (mobile), sticky top bar carrying the section title and a user menu
  (identity from `/auth/me`, role list, sign-out). A dedicated `/forbidden` view (`ForbiddenPage.tsx`) is
  rendered inside the shell and `ProtectedRoute` redirects there instead of hiding the restriction.
  The dashboard's duplicated topbar/sign-out was removed. Verified: `tsc -b && vite build` green,
  `oxlint` 0 errors (5 pre-existing warnings).
- **Priority:** P1 — resolved. **Related task:** IMP-007, IMP-026.

### GAP-019 — No design-system foundation (RESOLVED)
- **Affected module:** Frontend (all pages).
- **Current state (before):** styling was one utility stylesheet with no tokens, no shared primitives and no
  icon family.
- **Progress (2026-09-10):** design tokens (colour, radius, spacing, elevation, sidebar width) now live on
  `:root`, and the shell/`nav`/`icon-button`/`user-menu` primitives plus a first coherent icon family
  (`components/Icon.tsx`, 24x24 stroke) exist.
- **Resolution (2026-09-11):** [`frontend/src/index.css`](frontend/src/index.css) now carries the full primitive
  set on the tokens — buttons (`.btn` + `--primary`/`--ghost`/`--danger`/`--sm`), fields (`.field` +
  `.field-hint`), tables (`.table-wrap`/`.table`), the status pill
  (`.pill--ok|warn|danger|info|muted`), alerts (`.alert--error|success|info`) and one empty/loading/error shape
  (`.state`, `.state--empty`, `.state--loading`, `.state--error`). Legacy base styles
  (`input`/`button`/`.panel`/`.badge`/`.muted`/`.table`/`.pill`/`.field`) were converted from raw hex to the
  tokens (a few tint/strong tokens were added for the primitives), so the palette has a single source. Every
  screen still using the pre-GAP-019 `.badge` chip was migrated to the status pill (`AdminUsersPage`,
  `CommunicationPage`, `SecurityPage`), and the rebuilt Lease/Booking pages use the new state and alert
  primitives.
- **Icon family decision (recorded at the "revisit" point):** keep the in-house 24x24 stroke family
  ([`Icon.tsx`](frontend/src/components/Icon.tsx:1)) rather than adopt an icon library — no extra
  dependency/licensing surface, tree-shakeable, and the shapes already match the design language. Revisit only
  if the icon set outgrows what hand-authoring keeps coherent.
- **Verified:** `tsc -b && vite build` green; `oxlint` 0 errors / 4 pre-existing warnings.
- **Priority:** P2 — resolved. **Related task:** IMP-007, IMP-026.

### GAP-020 — Dashboard data unavailable (RESOLVED)
- **Affected module:** Frontend dashboard / cross-module.
- **Current state (before):** no aggregate endpoint existed and the dashboard page did no data fetch.
- **Resolution (2026-09-10):** the dashboard composes its figures **client-side from existing role-scoped
  endpoints** — no new API surface and no invented requirements. It now shows a role-appropriate KPI row
  (resident: amount due / overdue / open requests; manager+admin: outstanding, overdue, open requests,
  residents, units; accountant: collected, outstanding, overdue, reconciliation; technician: assigned,
  high/urgent, all open; plus unread notifications), a "needs your attention" list (payment alerts, overdue
  residents, high/urgent maintenance, out-of-balance reconciliation) and role-appropriate quick actions.
  It refreshes on mount and on window focus (no polling) and renders a loading/empty/error state.
- **Recorded decision:** occupancy totals are **not** shown — they would need an N+1 walk of buildings/units,
  so plain unit counts are used and an occupancy aggregate is deferred pending requirement IDs.
- **Priority:** P2 — resolved. **Related task:** IMP-007, IMP-052.

### GAP-021 — Frontend API coverage incomplete (RESOLVED)
- **Affected module:** Property, Resident, Maintenance, Lease, Booking.
- **Current state (before):** the backend exposed detail/update/availability/attachment endpoints the typed
  client did not call, so those screens were read-only or incomplete.
- **Progress (2026-09-10):** **Property done** — `getProperty`, `updateProperty`, `updateBuilding`,
  `getUnit`, `updateUnit` added to the client, and `PropertiesPage` gained inline edit flows for property,
  building and unit (with proper field labels and consistent status pills replacing the emoji occupancy
  marker). **Resident done** — `getResident`, `updateResident`, `moveOut` added, and `ResidentsPage` gained
  an edit dialog, a move-out dialog with an optional date, labelled fields, status pills and empty/notice
  states (the assign-unit dialog heading was also moved inside its form so the overlay renders correctly).
- **Progress (2026-09-11):** **Maintenance done** — the client gained `getMaintenanceRequest`,
  `setMaintenancePriority`, `uploadMaintenanceAttachment`, maintenance filters (`status`/`priority`) and an
  optional comment on assign/status/confirm; the shared client now supports `FormData` bodies so multipart
  uploads keep the browser-generated boundary instead of a JSON content type. `MaintenancePage` was rebuilt
  around the server state machine (ADR-0009): role-aware filters, status/priority pills, a detail panel that
  re-reads the request from `GET /maintenance/{id}` (attachments + history + timestamps), manager assign +
  priority changes, technician start/complete, resident confirm/reopen/cancel, comment capture, attachment
  upload for the requester/assignee/manager, and loading/empty/error/notice states. No backend change.
- **Progress (2026-09-11):** **Lease done** — the client gained `updateLease`, `getLeaseDocuments` and
  `uploadLeaseDocument` (multipart, so the browser boundary is preserved), `getLease` is now typed as
  `LeaseDetailDto` (documents + version history) instead of `history: unknown[]`, and `getLeases` accepts a
  status filter. `LeasesPage` was rebuilt around the lease lifecycle: role-aware status filter, a detail panel
  that re-reads `GET /leases/{id}`, term updates (each save is a new version the API records), document
  upload/list and terminate-with-reason, plus loading/empty/notice states. **Booking done** — the client gained
  `getFacility`, `getAvailability` and `updateFacility`; `getBookings` now supports `facilityId`/`mineOnly`
  filters and `createFacility` passes the cancellation window. `BookingsPage` was rebuilt: manager facility
  create/edit (hours, slot length, cancellation window, active), per-facility availability for a chosen day
  (free/booked slots), resident slot reservation with duration and overlap-aware start times, and
  cancel-with-reason. Every state is read back from the API — the client never assumes a slot is free.
- **Resolution:** all five modules now exercise the full endpoint surface. No backend change was required.
- **Recorded decision:** availability is read per facility/day on demand (no polling or caching), so the
  server's overlap check stays authoritative.
- **Left to other tasks:** Maintenance *service-level automated coverage* is owed by IMP-040; this gap covered
  the client/UI surface only.
- **Verified:** `tsc -b && vite build` green; `oxlint` 0 errors / 4 pre-existing warnings.
- **Priority:** P2 — resolved. **Related task:** IMP-003, IMP-004, IMP-005, IMP-022, IMP-024, IMP-026.

### GAP-022 — No password reset/change UI (RESOLVED)
- **Affected module:** Auth (login/account screens).
- **Current state (before):** `change-password`, `forgot-password` and `reset-password` existed on the backend
  but had no client methods, screens or routes.
- **Resolution (2026-09-10):** the three client calls were added; `/forgot-password` and `/reset-password`
  are public routes outside the shell (with a "Forgot your password?" link on the sign-in page) and
  `/account/password` is an authenticated route reached from the user menu. Forms validate locally
  (minimum length + confirmation match) and show field-level errors before any round trip. Because email
  delivery is not configured (GAP-005), the reset token returned by the API is surfaced in a clearly marked
  development panel rather than implying an email was sent.
- **Incidental fix:** the shared API client treated any empty success body as a JSON parse error; it now
  returns undefined for empty `200`/`204` responses, which the password/logout endpoints rely on.
- **Priority:** P2 — resolved. **Related task:** IMP-001, IMP-007.

### GAP-024 — Resident first-unit assignment is Administrator-only (manager scoping blocks bootstrap)
- **Affected module:** Resident (assignment + manager listing).
- **Related requirement/ADR:** FR-RES-003 (assign a resident to a unit), BRULE-RES-002; ADR-0008 (resident account provisioning).
- **Current state (observed by test, 2026-09-11):**
  [`ResidentService.CanAccessResidentAsync`](src/PMP.Modules.Resident/Services/ResidentService.cs:274) grants a
  Property Manager access to a resident only when that resident already has an active association to a unit the
  manager manages, and [`GetResidentsAsync`](src/PMP.Modules.Resident/Services/ResidentService.cs:64) filters the
  same way. A newly registered resident therefore has no managed-unit association, is not visible to a Property
  Manager, and cannot have their first unit assigned by one — only an Administrator can (Administrators bypass
  the scoping check). Characterized by the unit test
  `AssignUnitAsync_WhenManagerHasNoExistingAssociationWithTheResident_IsRejected` in
  [`ResidentServiceTests.cs`](tests/PMP.Tests/ResidentServiceTests.cs:1).
- **Why it matters:** if the intended flow is "resident registers → their property's manager assigns the unit",
  the current rule prevents it. If Administrator-only bootstrap is intended, it must be stated explicitly.
- **What remains:** a product/requirements decision — (a) allow a manager to establish the first association for
  any resident, or (b) confirm Administrator-only bootstrap and document it. No production code was changed
  (`IMP-040` recorded the behaviour with a test instead of silently resolving it).
- **Priority:** P2 — affects the FR-RES-003 assignment workflow.
- **Related task:** `IMP-004`.

### GAP-025 — No invoice PDF/document export (RESOLVED)
- **Affected module:** Payment (BR-005 — payment invoices/receipts).
- **Related requirement/ADR:** FR-PAY-003 (generate payment confirmations); ADR-0012.
- **Current state (before):** a successful payment generated a `PaymentInvoice` with a stable `INV-YYYY-NNNNNN`
  number, viewable only through the on-screen details view; no portable export existed and the project had no
  PDF-generation infrastructure to reuse.
- **Resolution (2026-09-15):** added `GET /api/invoices/{id}/export` which returns a portable **CSV** receipt
  (header + one data row: number, payment date, resident, unit, property, purpose, amount, currency, method,
  status, transaction reference, confirmation number). Format decision: CSV — no PDF library is present and CSV
  is sufficient for a portable payment confirmation. Access reuses `GetPaymentInvoiceAsync`, so residents export
  only their own invoice (another resident receives 400).
  See [`InvoicesController.ExportInvoice`](src/PMP.Api/Controllers/InvoicesController.cs:78).
- **Verification:** [`PaymentApiTests`](tests/PMP.Tests/Integration/PaymentApiTests.cs:274) asserts the CSV body
  carries the invoice number, transaction reference and the amount (invariant culture), and that another resident
  cannot export the invoice. Full suite **121 passed / 0 failed**; build 0 warnings / 0 errors.
- **Priority:** P3 — resolved (CSV export; PDF remains a possible future format).
- **Related task:** `IMP-053`.

---

## Unverifiable / decision-required items (not counted as gaps)

| Item | Why it is not a gap yet |
| --- | --- |
| MVP definition boundary (ADR-0007 vs ADR-0012 vs README) | Conflict is recorded (C-1, GAP-010); needs a documentation decision, not implementation work. |
| "Reopen a completed request within 7 days" behaviour | Verified present in code — [`MaintenanceService`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:391) allows the requester to move Completed → In Progress within 7 days — but it is still not covered by a test (remains in `GAP-001`; `IMP-040` closed without it), so it is not a new gap. |
| Confluence "Project Plan" (page 7897101, `PMP` space) | Project-management artifact (timeline, budget, risk, backlog) — not an engineering implementation plan, so this repo plan does not duplicate it. |
| Security/Visitor operator role | Compliance audit records that Property Manager/Administrator act as the security operator; treated as a documented decision, only revisited if FR-SEC work is re-scoped (`IMP-025`). |
