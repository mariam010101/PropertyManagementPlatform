# PMP Implementation Gap Analysis

**Last Updated:** 2026-09-10
**Companion document:** [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)
**Method:** every item below was identified by comparing approved requirements/ADRs
([`docs/adr/`](docs/adr/), [`docs/requirements-compliance.md`](docs/requirements-compliance.md),
[`plans/pmp-development-plan.md`](plans/pmp-development-plan.md)) against the verified code baseline recorded
in §1 of the plan. Gaps are recorded, never silently dropped.

Priority key: **P1** = required for MVP/verification, **P2** = post-MVP required, **P3** = hardening/tech debt.
Status uses the plan vocabulary (`GAP`, `NOT_STARTED`, `BLOCKED`, `IN_PROGRESS`).

| GAP | Title | Module | REQ / ADR | Priority | Related task | Status |
| --- | --- | --- | --- | --- | --- | --- |
| GAP-001 | Automated test coverage is still thin outside the MVP API slice: 21 tests total (12 API integration for the MVP + 9 unit); no Resident/Payment/Lease/Communication/Booking/Security service tests, no frontend tests, no lockout/background-job tests | Cross-module | all FR sets; ADR-0005 | P1 | IMP-040, IMP-041 | IN_PROGRESS |
| GAP-002 | Mobile platform access not implemented (no native app or PWA; responsive web only) | Mobile | FR-MOBILE-001..005; ADR-0007, ADR-0012 | P2 | IMP-027 | NOT_STARTED (deferred) |
| GAP-003 | Role revocation does not apply to already-issued 15-minute JWTs | Auth | FR-RBAC-006, BRULE-RBAC-006; ADR-0003/0004 | P1 | IMP-031 | GAP |
| GAP-004 | No dedicated owner "edit my profile" endpoint that mutates Identity name fields | Auth | FR-AUTH-005, BRULE-AUTH-006 | P1 | IMP-030 | GAP |
| GAP-005 | Email channel is record-only: notifications store channel/delivery status but no transport exists (no SMTP/MailKit/SendGrid/DependencyInjection of `IEmailSender`) | Communication | FR-COM-001, FR-COM-006 | P2 | IMP-023 | GAP |
| GAP-006 | "Active resident must have a lease" is a process convention, not an enforced invariant | Lease / Resident | BRULE-LEASE-001 | P2 | IMP-033 | GAP |
| GAP-007 | Role validation missing on two write paths: maintenance assignment does not verify the assignee is a Technician; property create does not verify the manager is a Property Manager | Maintenance / Property | BRULE-MNT-003, BRULE-PROP-003; ADR-0006 | P3 | IMP-034 | GAP |
| GAP-008 | No distinct "suspended" account state (only active/inactive) — requires a product decision on BRULE-AUTH-005 wording | Auth | BRULE-AUTH-005 | P3 | IMP-032 | BLOCKED |
| GAP-009 | No CI/CD pipeline and no deployment packaging (no `.github/`, Dockerfile, compose, environment matrix, production config/secrets strategy) | DevOps | — (ADR-0011 deployment surface) | P2 | IMP-042, IMP-043 | GAP |
| GAP-010 | Documentation drift: `plans/pmp-development-plan.md` points the SPA at `src/frontend/` (actual: [`frontend/`](frontend)); ADR-0007 remains `accepted` although ADR-0012 re-scoped the roadmap; MVP definition differs between ADR-0007 and [`README.md`](README.md:130); aggregate entity is `ManagedProperty` while other text says `Property`; compliance audit not refreshed since 2026-09-03 | Docs | ADR-0007, ADR-0012 | P3 | IMP-051 | GAP |
| GAP-011 | No BRULE pages published for BR-009 (Booking) / BR-010 (Security), leaving those FR sets without business-rule traceability | Docs / Requirements | FR-BOOK-001..007, FR-SEC-001..007 | P3 | IMP-035 | GAP |
| GAP-012 | Payment is simulated: no payment-provider integration, so FR-PAY-002 "pay electronically" is satisfied only in the demo sense (no reversal/refund, no idempotency keys, no provider webhooks) | Payment | FR-PAY-002, FR-PAY-003; ADR-0012 (trade-off) | P2 | IMP-020 | GAP |
| GAP-013 | Platform NFRs (performance, availability, backup/restore of `pmp.db`, observability) have no requirement IDs and no verification — pure traceability gap | Cross-cutting | none exist | P3 | IMP-044, IMP-052 | GAP |
| GAP-014 | Maintenance notifications carry no request context (the request title is absent from the persisted payload), so a resident cannot identify which request a notification refers to | Maintenance / Communication | FR-MNT-005, FR-COM-001 | P3 | IMP-040 | GAP |

---

## Gap detail

### GAP-001 — Automated test coverage still thin outside the MVP API slice
- **Affected module:** cross-module (Resident, Payment, Lease, Communication, Booking, Security services; the three hosted jobs; frontend).
- **Related requirement/ADR:** all FR sets; ADR-0005 (service `Result` pattern makes service-level testing the intended seam).
- **Current state:** `IMP-006` (2026-09-10) added 12 API integration tests
  ([`PmpApiFixture.cs`](tests/PMP.Tests/Integration/PmpApiFixture.cs:1),
  [`MvpEndToEndApiTests.cs`](tests/PMP.Tests/Integration/MvpEndToEndApiTests.cs:1)) that verify the MVP slice over
  real HTTP against a throwaway SQLite database — journey, authorization negatives, notification persistence and
  the refresh/logout lifecycle. `dotnet test` → 21 passed / 0 failed. Still uncovered: service-level tests for
  Resident/Payment/Lease/Communication/Booking/Security, post-MVP endpoint coverage, the maintenance 7-day
  auto-close job, the lease/payment background sweeps, attachment upload, and all frontend behaviour
  ([`UnitTest1.cs`](tests/PMP.Tests/UnitTest1.cs) placeholder still present).
- **What remains:** extend the integration host to the post-MVP endpoints, add per-module service tests reusing
  the [`TestDoubles.cs`](tests/PMP.Tests/TestDoubles.cs) pattern, add tests for the three hosted services,
  remove the placeholder, and stand up frontend testing.
- **Priority:** P1.
- **Related tasks:** `IMP-040` (per-module expansion, next task), `IMP-041` (frontend), `IMP-042` (CI).

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

### GAP-003 — Role-revocation latency
- **Affected module:** Auth (RBAC).
- **Related requirement/ADR:** FR-RBAC-006, BRULE-RBAC-006; ADR-0003, ADR-0004.
- **Current state:** role changes are written immediately and audited, but an already-issued access token keeps
  the old role claims until it expires (15-minute TTL per [`JwtOptions.cs`](src/PMP.Modules.Auth/Options/JwtOptions.cs));
  deactivation additionally revokes refresh tokens.
- **What remains:** choose one mechanism (shortened access-token TTL + refresh, per-request active-role recheck,
  or Identity security-stamp validation) and add a test proving a removed role is unusable on the next request.
- **Priority:** P1.
- **Related task:** `IMP-031`.

### GAP-004 — No owner self-service profile update
- **Affected module:** Auth (+ Resident for the profile data).
- **Related requirement/ADR:** FR-AUTH-005, BRULE-AUTH-006.
- **Current state:** residents read their own profile via `GET /api/residents/me`; edits happen through the
  manager/admin path `PUT /api/residents/{residentId}`. No endpoint lets a user update their own Identity
  name fields.
- **What remains:** add an authenticated self-update endpoint (identity + resident fields), audit the change,
  and test that a user cannot update another user's data.
- **Priority:** P1.
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
- **Current state:** no `.github/` workflows, no Dockerfile/compose, no documented environment matrix; the app
  relies on `appsettings.Development.json` + seed data and a local `pmp.db`.
- **What remains:** CI running build + `dotnet test` + frontend lint/build on push; a packaging path that runs
  migrations and seed without dev-only config; documented secrets/production configuration.
- **Priority:** P2.
- **Related tasks:** `IMP-042`, `IMP-043`.

### GAP-010 — Documentation drift
- **Affected module:** docs / repo layout.
- **Related requirement/ADR:** ADR-0007 (status not updated after ADR-0012), ADR-0012.
- **Current state:** see conflicts C-1..C-3 in §1 of the plan — `src/frontend/` vs [`frontend/`](frontend);
  ADR-0007 still `accepted` while ADR-0012 supersedes its roadmap consequences; MVP definition mismatch between
  ADR-0007 (MVP excludes the RBAC admin UI) and [`README.md`](README.md:130) (RBAC admin UI listed as MVP);
  `ManagedProperty` vs `Property` naming; compliance audit last refreshed 2026-09-03 and now inaccurate on
  FR-COM-006 (C-4).
- **What remains:** a single documentation sync pass once MVP verification (IMP-006) establishes fresh evidence.
- **Priority:** P3.
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
- **Current state:** `PayInvoiceAsync` simulates card/bank payment and issues a confirmation number; there is no
  provider integration, so no refunds, reversals, idempotency keys, or webhook reconciliation exist.
- **What remains:** record the decision (simulated-for-demo vs provider integration), and if integration is
  required, add provider abstraction + idempotency + reconciliation tests.
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

---

## Unverifiable / decision-required items (not counted as gaps)

| Item | Why it is not a gap yet |
| --- | --- |
| MVP definition boundary (ADR-0007 vs ADR-0012 vs README) | Conflict is recorded (C-1, GAP-010); needs a documentation decision, not implementation work. |
| "Reopen a completed request within 7 days" behaviour | Verified present in code — [`MaintenanceService`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:391) allows the requester to move Completed → In Progress within 7 days — but it is not yet covered by a test (`IMP-040`), so it is not a new gap. |
| Confluence "Project Plan" (page 7897101, `PMP` space) | Project-management artifact (timeline, budget, risk, backlog) — not an engineering implementation plan, so this repo plan does not duplicate it. |
| Security/Visitor operator role | Compliance audit records that Property Manager/Administrator act as the security operator; treated as a documented decision, only revisited if FR-SEC work is re-scoped (`IMP-025`). |
