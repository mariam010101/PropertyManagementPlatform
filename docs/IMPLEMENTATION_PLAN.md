# PMP Implementation Progress

Overall: 77%
MVP: 89%

Completed: 8
Verified: 2
In Progress: 11
Not Started: 7
Blocked: 1
Gaps: 1

Last Updated: 2026-09-15

Next Recommended Task: IMP-041

---

## 0. How to read this plan

This file is the authoritative living implementation roadmap for PMP. It is derived from the
approved ADRs ([`docs/adr/`](docs/adr/)), the functional requirements audit
([`docs/requirements-compliance.md`](docs/requirements-compliance.md)), the sharpened development plan
([`plans/pmp-development-plan.md`](plans/pmp-development-plan.md)), and direct verification of the repository.

Status vocabulary (§7 of the operating rules): `NOT_STARTED`, `IN_PROGRESS`, `BLOCKED`, `IMPLEMENTED`,
`NEEDS_REVIEW`, `VERIFIED`, `COMPLETE`, `GAP`, `DEFERRED`.

Dashboard counts: `Completed`/`Verified`/`In Progress`/`Not Started`/`Blocked`/`Gaps` count **tasks** by status
(`Completed` = `COMPLETE`; `Verified` = `VERIFIED`, i.e. implemented + tested but with a documented
open item; Gap status = `IMP-033`). Separately,
[`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) documents **25** gaps
(GAP-001..GAP-025) — some gaps span multiple tasks.

> Correction (2026-09-10): the first published revision reported `Gaps: 4`, which double-counted one task.
> The verified count is **3** tasks with status `GAP` (1 + 15 + 9 + 1 + 3 = 29 tasks).

Completion % is **not** derived from file/endpoint/document counts. It is derived from verification state:
`100%` = implemented + acceptance criteria met + tests pass + integration complete + ADR/REQ compliant.
The only task so far that reaches that bar is `IMP-006`; every other task still carries at least one open
acceptance criterion and is therefore reported below 100%.

## 1. Verified baseline (evidence captured 2026-09-11; IMP-040 re-run)

| Check | Result | Evidence |
| --- | --- | --- |
| Solution builds | ✅ | `dotnet test PropertyManagement.sln` exit code 0 |
| Test suite | ✅ 101 passed / 0 failed (unit + API integration), 14 test files — every module now has service-level tests (IMP-040) | [`tests/PMP.Tests`](tests/PMP.Tests) |
| MVP API integration harness | ✅ real Kestrel host + throwaway SQLite, driven over HTTP (no test-only hooks in the API) | [`PmpApiFixture.cs`](tests/PMP.Tests/Integration/PmpApiFixture.cs:1), [`MvpEndToEndApiTests.cs`](tests/PMP.Tests/Integration/MvpEndToEndApiTests.cs:1) |
| Modules wired in host | ✅ 9 modules + shared kernel | [`Program.cs`](src/PMP.Api/Program.cs:35) |
| Cross-module adapters | ✅ `IUnitOccupancyProvider`→`UnitOccupancyProvider`, `INotificationService`→`PersistedNotificationService` | [`Program.cs`](src/PMP.Api/Program.cs:49) |
| API surface | ✅ Controllers for Auth, Properties, Residents, Maintenance, Communication, Leases, Payments, Bookings, Security | [`Controllers/`](src/PMP.Api/Controllers) |
| EF Core migrations | ✅ 11 migrations (Auth, Property, Resident, Maintenance, Communication, Lease, Payment + `PaymentRequestsAndObligations`, Booking, Security) | [`src/`](src) |
| Background services | ✅ 3 hosted services (maintenance auto-close, lease lifecycle, payment due dates) | [`AutoCloseHostedService.cs`](src/PMP.Modules.Maintenance/Background/AutoCloseHostedService.cs:15), [`LeaseLifecycleHostedService.cs`](src/PMP.Modules.Lease/Background/LeaseLifecycleHostedService.cs:12), [`PaymentDueDateHostedService.cs`](src/PMP.Modules.Payment/Background/PaymentDueDateHostedService.cs:12) |
| Email confirmation enforced | ✅ `RequireConfirmedEmail = true`, checked at login | [`ServiceCollectionExtensions.cs`](src/PMP.Modules.Auth/Extensions/ServiceCollectionExtensions.cs:49), [`AuthService.cs`](src/PMP.Modules.Auth/Services/AuthService.cs:144) |
| Email **transport** (SMTP/provider) | ❌ none (notifications record channel only) | no `Smtp`/`MailKit`/`SendGrid`/`IEmailSender` anywhere in [`src/`](src) |
| Roles | 5: Administrator, PropertyManager, Resident, Technician, Accountant (no Security role) | [`AppRoles.cs`](src/PMP.Shared/Common/AppRoles.cs:8) |
| Frontend | ✅ React 19 + TS + Vite, 12 pages, role-guarded routes | [`App.tsx`](frontend/src/App.tsx:25) |
| Frontend tests | ❌ no test runner/script/deps | [`package.json`](frontend/package.json:6) |
| CI/CD, Docker, deployment assets | ❌ none in repository root | repository listing |
| Dead template scaffolding / placeholders | ✅ none — `Class1.cs` ×5, `UnitTest1.cs` and `body.json` removed, plus the unused `LoggingNotificationService` stub | repository listing (IMP-050, 2026-09-11) |
| `TODO`/`FIXME`/`NotImplementedException` in `src/` | ✅ 0 matches | regex scan of [`src/`](src) |
| Documentation consistency | ✅ ADR-0007 marked partially superseded; `FR-COM-006` corrected to PARTIAL; dev-plan drift banner + path fixed; README rewritten as the project entry point | IMP-051, 2026-09-11 |
| Requirements traceability registry | ✅ [`docs/traceability.md`](docs/traceability.md) added; [`docs/README.md`](docs/README.md) index added | IMP-052, 2026-09-11 |

**Documented state** (compliance audit, ADR-0012): 71/78 FRs MET, 3 PARTIAL (FR-AUTH-005, FR-RBAC-006,
FR-COM-006), 5 NOT MET (BR-012); BR-001..BR-011 have working modules.
**Actual state**: the MVP slice is verified end-to-end over HTTP by the API integration suite (IMP-006), and
every module now has service-level tests for its core rules (IMP-040): Auth (token contents/signature),
Property, Resident, Maintenance, Payment, Lease, Communication, Booking and Security. Still untested: the
hosted background jobs (maintenance auto-close, lease expiry, payment due dates) and the attachment upload
paths (`GAP-001`); there is **no CI**, **no frontend test runner**, and the BR-012 mobile platform does not
exist. Progress therefore reflects verified implementation, not breadth.

### Documented-state vs actual-code conflicts (recorded, not silently resolved)

| # | Conflict | Resolution taken |
| --- | --- | --- |
| C-1 | ADR-0007 (MVP scope) was still `accepted` while ADR-0012 added the deferred modules and re-scoped MVP; the MVP definition also differed between ADR-0007 (no RBAC admin UI) and [`README.md`](README.md) (RBAC admin UI counted as MVP). | **Resolved 2026-09-11 (IMP-051):** ADR-0007 is now marked *partially superseded by ADR-0012*; ADR-0012 is authoritative for module scope, ADR-0007 remains authoritative for the MVP slice, and the README states the MVP definition explicitly. |
| C-2 | `plans/pmp-development-plan.md` locates the SPA at `src/frontend/`; the SPA actually lives at [`frontend/`](frontend). | Code is truth; doc drift recorded as GAP-010. |
| C-3 | Property aggregate entity is [`ManagedProperty`](src/PMP.Modules.Property/Entities/ManagedProperty.cs:9) (`ManagedProperty` FK naming inside the Property module); older references (editor state, prose) use `Property`. | Code is truth; naming drift recorded as GAP-010. |
| C-4 | Compliance audit marks FR-COM-006 (email + in-app notifications) 🟢 MET, but email is only *recorded* with a delivery status — no transport exists. | Recorded as GAP-005; FR-COM-006 downgraded to partial in this plan (IMP-023). |

## 2. Progress model (weights are auditable)

Each task carries an effort weight. Completion = Σ(weight × verified %) across all tasks.

| Group | Weight | Earned | % |
| --- | --- | --- | --- |
| MVP (IMP-001..007) | 60 | 53.1 | **89%** |
| Post-MVP + remaining FRs (IMP-020..035, IMP-053) | 61 | 50.2 | **82%** |
| Cross-cutting: tests, CI, DevOps, security, docs (IMP-040..052) | 38 | 19.0 | 50% |
| **Total** | **159** | **122.3** | **77%** |

### Module / layer progress

| Area | Weight basis | % | Primary blocker to reaching 100% |
| --- | --- | --- | --- |
| Auth (BR-001, BR-008) | IMP-001, IMP-002, IMP-030, IMP-031, IMP-032 | 85% | unit-level lockout tests, blocked suspension state |
| Property (BR-003) | IMP-003 | 90% | no manager-role validation on create; duplicate-unit rule covered by unit test only |
| Resident (BR-002) | IMP-004 | 95% | search/profile-edit paths not exercised at unit level; occupancy-history retention now covered by `ResidentServiceTests` |
| Maintenance (BR-004) | IMP-005 | 95% | 7-day auto-close job and attachment path untested |
| Payment + Accounting (BR-005, BR-011) | IMP-020, IMP-021 | 90% | real payment-provider integration (GAP-012); no frontend tests (IMP-041) |
| Lease (BR-006) | IMP-022 | 70% | BRULE-LEASE-001 invariant (IMP-033), document-storage hardening; core rules now tested |
| Communication (BR-007) | IMP-023 | 70% | no email transport (GAP-005); core rules now tested |
| Booking (BR-009) | IMP-024 | 70% | no BRULE page published (GAP-011); core rules now tested |
| Security/Visitors (BR-010) | IMP-025 | 70% | no dedicated Security role, no access-control hardware integration; core rules now tested |
| Mobile (BR-012) | IMP-027 | 0% | not started (post-MVP) |
| Backend (MVP backend tasks IMP-001..006) | IMP-001..006 | 90% | unit-level coverage for lockout/auto-close; hardening |
| Frontend (web) | IMP-007, IMP-026, IMP-041 | 75% | no frontend test tooling |
| Database/API | migrations + endpoint surface | 80% | MVP surface verified end-to-end; post-MVP endpoints unverified |
| Testing | IMP-006, IMP-040, IMP-041 | 70% | all nine modules have service tests (IMP-040); background-job/attachment paths and frontend tests still missing |
| Security (NFR/hardening) | IMP-031, IMP-034, IMP-044 | 27% | role-revocation fixed (IMP-031); remaining hardening tasks not started |
| DevOps | IMP-042, IMP-043 | 0% | no pipeline, no packaging/deployment assets |

## 3. Scope boundaries

- **MVP (ADR-0007 + ADR-0012 clarification):** Auth & User Management, Property Management, Resident
  Management, Maintenance Management, RBAC (roles/policies + admin UI).
- **Post-MVP (ADR-0012, all delivered in code):** Communication, Lease, Payment + Financial Reporting/Accountant,
  Booking, Security & Visitor.
- **Explicitly out of scope until instructed (ADR-0007 / ADR-0012):** BR-012 Mobile Platform Access
  (IMP-027 stays `NOT_STARTED` and must not consume MVP effort), real payment-provider integration,
  access-control hardware integration.
- Post-MVP work must not start ahead of incomplete MVP verification (IMP-040 for the remaining MVP-level
  test coverage). `IMP-027` (mobile) also stays blocked behind IMP-040.

## 4. MVP tasks

| ID | Task | Module | Pri | Deps | REQ | ADR | Status | % | W |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| IMP-001 | Auth & identity: register/login/refresh/logout, password reset & change, email-confirmation gate, auth event audit, staff provisioning | Auth | P1 | — | FR-AUTH-001..008 | 0003, 0008 | NEEDS_REVIEW | 85 | 10 |
| IMP-002 | RBAC: 5 roles, policies, admin user list/role assignment/deactivate/reactivate + admin UI | Auth | P1 | IMP-001 | FR-RBAC-001..005 | 0004 | NEEDS_REVIEW | 85 | 8 |
| IMP-003 | Property: Property→Building→Unit CRUD, operational status, derived occupancy, manager scoping, search | Property | P1 | IMP-001 | FR-PROP-001..007 | 0006, 0010, 0011 | NEEDS_REVIEW | 90 | 8 |
| IMP-004 | Resident: profile auto-provisioning, edit, effective-dated unit assignment, move-out, deactivate, occupancy history, search | Resident | P1 | IMP-001, IMP-003 | FR-RES-001..007 | 0008, 0010 | NEEDS_REVIEW | 95 | 8 |
| IMP-005 | Maintenance: state machine, priorities, technician assignment, attachments, history, resident confirm + 7-day auto-close, persisted notifications | Maintenance | P1 | IMP-001, IMP-003, IMP-004 | FR-MNT-001..008 | 0009 | NEEDS_REVIEW | 95 | 10 |
| IMP-006 | MVP end-to-end verification: API integration tests over the MVP slice (register → confirm → login → assign unit → submit → assign → complete → confirm) incl. policy/authorization assertions | Cross-module | P1 | IMP-001..005 | FR-AUTH/RES/PROP/MNT sets | 0003, 0004 | COMPLETE | 100 | 6 |
| IMP-007 | Frontend MVP pages + role-guarded routing (login, register, dashboard, properties, residents, maintenance, admin users) | Frontend | P1 | IMP-001..005 | FR-AUTH/RES/PROP/MNT sets | — | NEEDS_REVIEW | 75 | 10 |

**Acceptance criteria**

- **IMP-001:** all FR-AUTH endpoints exist and enforce `RequireConfirmedEmail`; login blocked for unconfirmed/inactive users; refresh/logout revoke; every auth event persisted; unit tests cover login/lockout/refresh revocation paths.
- **IMP-002:** policies in [`AppPolicies`](src/PMP.Shared/Common/AppPolicies.cs) deny by default; admin endpoints Administrator-only; role changes audited; tests assert least-privilege for each role.
- **IMP-003:** occupancy derived from active resident associations (not stored); queries restricted to assigned properties for managers; duplicate unit numbers rejected; unit tests + API tests pass.
- **IMP-004:** resident profile auto-created on registration; assignments effective-dated and retained on move-out/deactivate; inactive units reject assignment; tests cover history retention.
- **IMP-005:** transition map rejects invalid transitions; closed requests immutable; 7-day auto-close verified by test; status-change notifications persisted.
- **IMP-006:** ✅ met (2026-09-10) — the integration host boots the real API (`PmpApiFixture`, Kestrel + throwaway SQLite) and `MvpEndToEndApiTests` asserts the full MVP journey, refresh/logout lifecycle, the email-confirmation gate and per-role authorization negatives; `dotnet test PropertyManagement.sln` → 21 passed / 0 failed.
- **IMP-007:** every MVP page is reachable only under its role guard and renders data from real endpoints; no unauthenticated access.

## 5. Post-MVP and remaining-requirement tasks

| ID | Task | Module | Pri | Deps | REQ | ADR | Status | % | W |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| IMP-020 | Payment requests/orders: lifecycle, per-user obligations, dashboard + notification bell, pay/confirm, overdue sweep, cancel, failed attempts | Payment | P2 | IMP-004, IMP-022 | FR-PAY-001..007 | 0012 | VERIFIED | 90 | 10 |
| IMP-021 | Financial reporting, reconciliation, outstanding-by-resident, Accountant scoped access | Payment | P2 | IMP-020 | FR-ACCT-001..004 | 0004, 0012 | VERIFIED | 85 | 6 |
| IMP-053 | Payment invoices (receipts): generated on successful payment, idempotent 1:1 with the payment, unique INV-YYYY-NNNNNN numbering, resident + accountant/manager/admin access | Payment | P2 | IMP-020, IMP-021 | FR-PAY-003, FR-ACCT-001 | 0004, 0012 | COMPLETE | 100 | 6 |
| IMP-022 | Lease: agreements, version history, secure documents, expiry/archive sweep | Lease | P2 | IMP-003, IMP-004 | FR-LEASE-001..007 | 0012 | IN_PROGRESS | 70 | 8 |
| IMP-023 | Communication: persisted notifications (InApp/Email channels), announcements, read state | Communication | P2 | IMP-001 | FR-COM-001..006 | 0012 | IN_PROGRESS | 70 | 8 |
| IMP-024 | Booking: facilities, availability, overlap-free reservations, cancellation window | Booking | P2 | IMP-003, IMP-004 | FR-BOOK-001..007 | 0012 | IN_PROGRESS | 70 | 6 |
| IMP-025 | Security & visitors: visitor register/check-in/out, access grants with expiry, logs | Security | P2 | IMP-003, IMP-024 | FR-SEC-001..007 | 0012 | IN_PROGRESS | 70 | 6 |
| IMP-026 | Frontend post-MVP pages/routes (payments, leases, communication, bookings, security) | Frontend | P2 | IMP-020..025 | FR-PAY/LEASE/COM/BOOK/SEC sets | — | NEEDS_REVIEW | 75 | 8 |
| IMP-027 | Mobile platform access (native app or PWA) on the existing API | Mobile | P2 | IMP-007, IMP-026 | FR-MOBILE-001..005 | 0007, 0012 | NOT_STARTED (deferred) | 0 | 12 |
| IMP-030 | Dedicated owner "edit my profile" endpoint that also updates Identity name fields | Auth | P1 | IMP-001 | FR-AUTH-005, BRULE-AUTH-006 | 0003 | COMPLETE | 100 | 2 |
| IMP-031 | Immediate role-revocation: per-request active-role recheck of already-issued JWTs | Auth | P1 | IMP-002 | FR-RBAC-006, BRULE-RBAC-006 | 0003, 0004 | COMPLETE | 100 | 3 |
| IMP-032 | Distinct "suspended" account state (if the requirement is confirmed) | Auth | P3 | — | BRULE-AUTH-005 | — | BLOCKED | 0 | 1 |
| IMP-033 | Enforce "active resident must have an active lease" as a hard invariant | Lease/Resident | P2 | IMP-022 | BRULE-LEASE-001 | 0012 | GAP | 0 | 2 |
| IMP-034 | Hardening: validate Technician role on maintenance assignment; validate manager role on property create | Maintenance/Property | P3 | IMP-003, IMP-005 | BRULE-MNT-003, BRULE-PROP-003 | 0006 | NOT_STARTED | 0 | 2 |
| IMP-035 | Publish BRULE pages for BR-009/BR-010 in Confluence (or record the decision that they are not required) | Docs | P3 | — | FR-BOOK/FR-SEC | — | NOT_STARTED | 0 | 1 |

**Acceptance criteria (abbreviated for tasks above 0%)**

- **IMP-020:** ✅ met for the request/obligation scope (2026-09-10) — every payment a resident owes is a persisted request/order carrying resident, amount, currency, purpose, due date, status and creation date; the backend computes the amount currently due / overdue / upcoming per user; the explicit lifecycle is `Open → PartiallyPaid → Paid` with `Overdue` and `Cancelled`; declined attempts are recorded as `Failed` transactions and preserved. Verified by 15 unit tests ([`PaymentServiceTests.cs`](tests/PMP.Tests/PaymentServiceTests.cs:1)) and 11 API integration tests ([`PaymentApiTests.cs`](tests/PMP.Tests/Integration/PaymentApiTests.cs:1)). **Open (not part of this task):** real payment-provider integration — see GAP-012.
- **IMP-021:** ✅ met except export (2026-09-10) — report/reconciliation/outstanding endpoints are restricted to the `Financial` policy; the Accountant can read financial records but is rejected (403) from property/maintenance data (test: `Accountant_ReadsFinancials_ButIsRestrictedFromNonFinancialModules`). **Open:** an export format is still neither defined nor tested (GAP-016).
- **IMP-053:** ✅ met (2026-09-14) — a successful payment creates exactly one `PaymentInvoice` (unique FK to the payment) with a deterministic `INV-YYYY-NNNNNN` number; failed/declined payments create none; a paid request cannot be paid again (no duplicate); residents read only their own invoices; the Accountant/manager/admin listing is scoped (manager → own properties) and policy-restricted (403 for resident/technician); the database enforces unique `PaymentTransactionId` and `InvoiceNumber`. Verified by unit + SQLite-constraint + API integration tests — full suite 116 passed / 0 failed; frontend `InvoicesPage` added with build/lint green. **Out of scope (future):** email delivery (GAP-025 CSV export added 2026-09-15; PDF remains unbuilt).
- **IMP-022:** each lease change creates a version row; documents access-controlled; expiry sweep marks past-end leases and notices within 30 days; test coverage added.
- **IMP-023:** notification history queryable with delivery status; announcements filtered to the resident's property; email transport decision recorded (implement or explicitly descope FR-COM-006 email delivery).
- **IMP-024:** overlap rejection and cancellation-window rules unit-tested (SQLite client-side evaluation documented in ADR-0012).
- **IMP-025:** visitor check-in/out times recorded; expired grants treated as expired; access log immutable; role/ownership decision recorded (currently Property Manager/Admin act as security operator).
- **IMP-026:** each post-MVP page enforces the same server-side role guard as its endpoints.
- **IMP-027:** not to be started until IMP-040 is green (IMP-006 is complete).
- **IMP-030:** ✅ met (2026-09-15) — `PUT /api/auth/me` updates the caller's own Identity `FirstName`/`LastName`/`PhoneNumber`, records a `ProfileUpdated` audit event, and syncs the resident profile; it is caller-scoped by construction. Verified by [`ProfileUpdateApiTests`](tests/PMP.Tests/Integration/ProfileUpdateApiTests.cs:11) (401 unauthenticated; self-edit reflected in `/api/auth/me` and `/api/residents/me`; another user's identity untouched).
- **IMP-031:** ✅ met (2026-09-15) — the JWT bearer handler re-loads the caller's current roles (and active state) on every request and replaces stale role claims, so revoking a role takes effect on the next request (≤1 request latency). Verified by [`RoleRevocationApiTests`](tests/PMP.Tests/Integration/RoleRevocationApiTests.cs:10): an issued technician token stops authorizing `GET /api/maintenance` (403) after the Technician role is removed.
- **IMP-032:** requires a product decision on BRULE-AUTH-005 (whether "suspended" is a distinct state or satisfied by active/inactive). Do not implement until answered.
- **IMP-033:** assignment/creation paths reject state where an active resident has no lease; decision recorded on retroactive data handling.
- **IMP-034:** assigning a non-Technician user is rejected; creating a property for a non-manager user is rejected; tests added.

## 6. Cross-cutting, testing, DevOps and tech-debt tasks

| ID | Task | Area | Pri | Deps | REQ/ADR | Status | % | W |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| IMP-040 | Automated test expansion: per-module service tests for Resident, Auth, Payment, Lease, Communication, Booking, Security; shared test host/fixtures | Testing | P1 | IMP-001..005 | ADR-0005 | COMPLETE | 100 | 12 |
| IMP-041 | Frontend test infrastructure (Vitest/RTL) + core page/route tests | Testing | P2 | IMP-007 | — | NOT_STARTED | 0 | 4 |
| IMP-042 | CI pipeline: build + `dotnet test` + frontend lint/build on every push | DevOps | P2 | IMP-006, IMP-040 | — | NOT_STARTED | 0 | 4 |
| IMP-043 | Deployment packaging: Dockerfile/compose or equivalent, production SQLite path, secrets/JWT config, environment matrix | DevOps | P2 | IMP-042 | ADR-0011 | NOT_STARTED | 0 | 5 |
| IMP-044 | Security & NFR review and hardening: secrets handling, upload path safety, rate limiting/lockout tuning, HTTPS/HSTS, structured logging/observability | Security | P1 | IMP-001..005 | 0003, 0004 | NOT_STARTED | 0 | 6 |
| IMP-050 | Remove template scaffolding and stray artifacts (`Class1.cs` ×5, `UnitTest1.cs`, `body.json`, unused `LoggingNotificationService`) | Tech debt | P3 | — | — | COMPLETE | 100 | 2 |
| IMP-051 | Documentation sync: README status/paths, MVP definition (C-1), compliance-audit refresh, ADR-0007 superseded marker, `ManagedProperty` naming, BRULE-* coverage notes | Tech debt | P3 | — | ADR-0012 | COMPLETE | 100 | 2 |
| IMP-052 | Traceability registry: REQ → ADR → IMP → code → API → test, kept current with this plan | Tech debt | P2 | — | — | COMPLETE | 100 | 3 |

**Acceptance criteria (abbreviated for tasks above 0%)**

- **IMP-040:** ✅ met (2026-09-11) — every one of the nine modules has service-level tests at the ADR-0005 `Result` seam (Auth token, Property, Resident, Maintenance, Payment, Lease, Communication, Booking, Security); the InMemory-per-test builders plus the `PmpApiFixture` Kestrel/throwaway-SQLite host form the shared test host; suite runnable via `dotnet test PropertyManagement.sln` → **101 passed / 0 failed**, build 0 warnings / 0 errors. The TDD workflow and testing strategy are documented in [`docs/TESTING.md`](docs/TESTING.md) and the `.roo/skills/tdd` skill. **Open under other tasks:** background-job/attachment coverage (`GAP-001`), frontend tests (`IMP-041`), CI (`IMP-042`).
- **IMP-041:** `npm test` exists and covers login/route-guard and one data page; failures break the build.
- **IMP-042:** pipeline runs on push/PR and fails on build/test/lint errors.
- **IMP-043:** app runs from a clean container/packaged deployment with migrations + seed and no dev-only config.
- **IMP-044:** findings documented; each accepted finding becomes a task or an explicit accepted risk.
- **IMP-050/051:** ✅ met (2026-09-11) — the scaffolding/stray files and the unused notification stub were removed; ADR-0007 is marked partially superseded, `FR-COM-006` is corrected to PARTIAL, the historical development plan carries a drift banner and the README is rewritten as the project entry point.
- **IMP-052:** ✅ met (2026-09-11) — [`docs/traceability.md`](docs/traceability.md) documents the identifier scheme and the BR → FR → BRULE → ADR → IMP → code → test chain, and [`docs/README.md`](docs/README.md) indexes every artefact; the live matrix in §7 remains the single copy.

## 7. Traceability matrix (requirement → ADR → task → code/API → test)

`API` = controller surface, `Code` = service/entity, `Test` = existing automated coverage.

| Requirement set | ADR | Task(s) | API | Code | Test |
| --- | --- | --- | --- | --- | --- |
| FR-AUTH-001..008 | 0003, 0008 | IMP-001, IMP-030 | [`AuthController.cs`](src/PMP.Api/Controllers/AuthController.cs:28) | [`AuthService.cs`](src/PMP.Modules.Auth/Services/AuthService.cs), [`TokenService.cs`](src/PMP.Modules.Auth/Services/TokenService.cs) | ✅ API integration (IMP-006) + 4 token unit tests (IMP-040): login/confirm gate/refresh/logout, token signature/expiry/subject/roles; ✅ own-profile update (IMP-030); ⚠️ lockout untested |
| FR-RBAC-001..006 | 0004 | IMP-002, IMP-031 | [`AuthController.cs`](src/PMP.Api/Controllers/AuthController.cs:127) | [`AppPolicies.cs`](src/PMP.Shared/Common/AppPolicies.cs), [`AppRoles.cs`](src/PMP.Shared/Common/AppRoles.cs:8) | ✅ API integration (IMP-006): anonymous 401 + per-role 403; ✅ role revocation on next request (IMP-031); ⚠️ full policy matrix untested |
| FR-PROP-001..007 | 0006, 0010, 0011 | IMP-003, IMP-034 | [`PropertiesController.cs`](src/PMP.Api/Controllers/PropertiesController.cs:25) | [`PropertyService.cs`](src/PMP.Modules.Property/Services/PropertyService.cs) | ✅ API integration (IMP-006) + 2 unit tests ([`PropertyServiceTests.cs`](tests/PMP.Tests/PropertyServiceTests.cs:10)) |
| FR-RES-001..007 | 0008, 0010 | IMP-004 | [`ResidentsController.cs`](src/PMP.Api/Controllers/ResidentsController.cs:24) | [`ResidentService.cs`](src/PMP.Modules.Resident/Services/ResidentService.cs), [`UnitOccupancyProvider.cs`](src/PMP.Modules.Resident/Abstractions/UnitOccupancyProvider.cs) | ✅ API integration (IMP-006) + 9 unit tests (IMP-040): auto-provisioning, assignment/transfer, move-out, deactivation, manager scoping, history retention |
| FR-MNT-001..008 | 0009 | IMP-005, IMP-034 | [`MaintenanceController.cs`](src/PMP.Api/Controllers/MaintenanceController.cs:27) | [`MaintenanceService.cs`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs) | ✅ API integration (IMP-006) + 3 unit tests ([`MaintenanceServiceTests.cs`](tests/PMP.Tests/MaintenanceServiceTests.cs:15)); ⚠️ auto-close job untested |
| FR-PAY-001..007 | 0012 | IMP-020, IMP-053 | [`PaymentsController.cs`](src/PMP.Api/Controllers/PaymentsController.cs:32), [`InvoicesController.cs`](src/PMP.Api/Controllers/InvoicesController.cs:23) | [`PaymentService.cs`](src/PMP.Modules.Payment/Services/PaymentService.cs), [`PaymentInvoice.cs`](src/PMP.Modules.Payment/Entities/PaymentInvoice.cs:1) | ✅ 22 unit ([`PaymentServiceTests.cs`](tests/PMP.Tests/PaymentServiceTests.cs:1)) + 2 SQLite-constraint ([`PaymentInvoiceConstraintTests.cs`](tests/PMP.Tests/PaymentInvoiceConstraintTests.cs:1)) + 17 API integration ([`PaymentApiTests.cs`](tests/PMP.Tests/Integration/PaymentApiTests.cs:1)) |
| FR-ACCT-001..004 | 0004, 0012 | IMP-021 | [`PaymentsController.cs`](src/PMP.Api/Controllers/PaymentsController.cs:74) | [`PaymentService.cs`](src/PMP.Modules.Payment/Services/PaymentService.cs) | ✅ API integration: resident/technician 403 on financials; Accountant 200 financials / 403 property+maintenance |
| FR-LEASE-001..007 | 0012 | IMP-022, IMP-033 | [`LeasesController.cs`](src/PMP.Api/Controllers/LeasesController.cs:32) | [`LeaseService.cs`](src/PMP.Modules.Lease/Services/LeaseService.cs) | ✅ 11 unit tests (IMP-040): overlap, versioning, termination + notice, expiry sweep, access scoping |
| FR-COM-001..006 | 0012 | IMP-023 | [`CommunicationController.cs`](src/PMP.Api/Controllers/CommunicationController.cs:30) | [`CommunicationService.cs`](src/PMP.Modules.Communication/Services/CommunicationService.cs), [`PersistedNotificationService.cs`](src/PMP.Modules.Communication/Services/PersistedNotificationService.cs) | ✅ 8 unit tests (IMP-040): recipient scoping, delivery status, announcements by property |
| FR-BOOK-001..007 | 0012 | IMP-024 | [`BookingsController.cs`](src/PMP.Api/Controllers/BookingsController.cs:30) | [`BookingService.cs`](src/PMP.Modules.Booking/Services/BookingService.cs) | ✅ 12 unit tests (IMP-040): role/occupancy checks, overlap, operating hours, cancellation window, notifications |
| FR-SEC-001..007 | 0012 | IMP-025 | [`SecurityController.cs`](src/PMP.Api/Controllers/SecurityController.cs:31) | [`SecurityService.cs`](src/PMP.Modules.Security/Services/SecurityService.cs) | ✅ 10 unit tests (IMP-040): security-role gates, visitor state machine, access-grant validation/revocation, scoping |
| FR-MOBILE-001..005 | 0007, 0012 | IMP-027 | n/a (reuses existing API) | n/a | n/a — see GAP-002 |
| BRULE-* (per module) | module ADRs | listed in [`requirements-compliance.md`](docs/requirements-compliance.md) | — | — | partial only (see GAP-001, GAP-006, GAP-007) |
| Platform NFRs (performance, availability, observability) | — | IMP-044 | — | — | — **traceability gap, no REQ IDs exist** (GAP-013) |

## 8. Task detail record (filled in after each implementation)

```
Status:
Completion:
Evidence:
Verification:
Remaining:
```

### IMP-006 — MVP end-to-end verification: API integration test suite

```
Status:        COMPLETE
Completion:    100%
Evidence:      tests/PMP.Tests/Integration/PmpApiFixture.cs — boots the real API host
               (dotnet <PMP.Api.dll>, throwaway SQLite in a temp dir, ConnectionStrings__/Jwt__ via
               environment variables, readiness probe, server-log capture on failure).
               tests/PMP.Tests/Integration/MvpEndToEndApiTests.cs — 12 tests.
               dotnet test PropertyManagement.sln → 21 passed / 0 failed, 0 skipped (4 s).
Verification:  Anonymous → 401 on /api/properties, /api/auth/users, /api/maintenance (FR-AUTH-007).
               Four seeded role accounts log in; tokens carry the expected role (FR-AUTH-002/008).
               Register → unconfirmed login rejected (400) → confirm-email → login succeeds → resident profile
               auto-provisioned and readable via /api/residents/me (FR-AUTH-001, FR-RES-001).
               Refresh rotates the token; the consumed token is rejected (400); logout revokes the session and
               the revoked token is rejected (FR-AUTH-006, ADR-0003).
               Resident/technician receive 403 on admin/manager endpoints (FR-RBAC-002/004, ADR-0004).
               Derived occupancy: A-101 occupied, A-102 vacant (FR-PROP-004).
               Journey: submit → manager assigns (technician could not see the request before assignment) →
               In Progress → Completed → resident confirm → Closed; ≥5 history entries; Completed→Submitted
               rejected (400); re-confirm of a closed request rejected (400); new persisted notifications for the
               resident (FR-MNT-001/003/004/005/006/008, BRULE-MNT-004, ADR-0009).
               Resident submitting for a foreign unit → 400 (BRULE-MNT-001).
Remaining:     Nothing outstanding for this task. Follow-ups are recorded elsewhere: baseline lockout test and
               post-MVP/module-level coverage (IMP-040), and the notification payload having no request context
               (GAP-014).
```

### IMP-020 — Payment requests/orders: lifecycle, per-user obligations, dashboard + bell

```
Status:        VERIFIED
Completion:    90%
Evidence:      src/PMP.Modules.Payment/Enums/PaymentEnums.cs — explicit InvoiceStatus lifecycle
               (Open → PartiallyPaid → Paid | Overdue | Cancelled) + PaymentPurpose + PaymentAlertType.
               src/PMP.Modules.Payment/Entities/Invoice.cs — request now carries Currency, Purpose,
               CancelledAt/CancellationReason and OverdueNotifiedAt; effective-status + outstanding helpers.
               Migration 20260910211847_PaymentRequestsAndObligations (additive, backfills USD/Rent).
               src/PMP.Modules.Payment/Services/PaymentService.cs — GenerateRentRequestAsync (amount from
               lease MonthlyRent), GetDashboardAsync (amount due / overdue / upcoming / next due / requests /
               history / bell alerts), GetOutstandingByResidentAsync, CancelInvoiceAsync, MarkOverdueAsync,
               declined-attempt recording.
               src/PMP.Api/Controllers/PaymentsController.cs — GET dashboard, GET outstanding,
               POST requests/rent, POST invoices/{id}/cancel, POST run-due-dates.
               Frontend: frontend/src/components/PaymentNotificationBell.tsx (badge + alert list + direct
               navigation), frontend/src/pages/PaymentsPage.tsx (dashboard, stats, requests, history,
               manager outstanding table, rent-request + cancel actions), index.css.
Verification:  dotnet build PropertyManagement.sln → 0 warnings / 0 errors.
               dotnet test PropertyManagement.sln → 47 passed / 0 failed.
               Unit: amount derived from lease (never hard-coded); dashboard due/overdue/upcoming maths;
               a past-due request reports Overdue before the sweep; MarkOverdue persists once and notifies
               once; cancellation retains the record and blocks payment; full/partial payment statuses;
               a declined attempt is preserved as Failed and raised to the bell; a resident cannot pay
               another resident's request; manager outstanding is scoped to the manager's properties.
               API: anonymous 401; resident/technician 403 on outstanding; Accountant 200 financials /
               403 property+maintenance; the resident's dashboard contains only their own requests and
               equals the sum of their outstanding balances; a manager-generated rent request equals the
               lease monthly rent and appears in the resident's dashboard + bell; the manager outstanding
               report lists the resident; cancellation removes the obligation and makes it unpayable;
               a declined payment appears in history + bell.
Remaining:     10% — real payment-provider integration (idempotency, refunds, webhooks) is explicitly out of
               scope of this task and remains GAP-012; the payment UI has no automated frontend tests (IMP-041).
```

### IMP-021 — Financial reporting, reconciliation, Accountant scoped access

```
Status:        VERIFIED
Completion:    85%
Evidence:      src/PMP.Modules.Payment/Services/PaymentService.cs — GetFinancialReportAsync now reports
               OverdueInvoices/OverdueTotal using effective status; GetReconciliationAsync unchanged in
               behaviour; both restricted to the Financial policy.
Verification:  API integration ([PaymentApiTests.cs]) — resident and technician receive 403 on
               /api/payments/outstanding; the Accountant receives 200 on financial endpoints and 403 on
               /api/properties and /api/maintenance (FR-ACCT-004).
Remaining:     15% — no report export format is defined or tested; see the gap analysis entry.
```

### IMP-053 — Payment invoices (receipts) for successful payments

```
Status:        COMPLETE
Completion:    100%
Evidence:      src/PMP.Modules.Payment/Entities/PaymentInvoice.cs — receipt entity (stable number, 1:1 payment
               FK, denormalized resident/unit/property, amount/currency/purpose/method/status/payment date,
               confirmation). src/PMP.Modules.Payment/Entities/InvoiceNumberSequence.cs — per-year counter.
               PaymentDbContext — payment_invoices + invoice_number_sequences, unique indexes on
               PaymentTransactionId and InvoiceNumber, restrict FKs. PaymentService.PayInvoiceAsync creates the
               receipt inside the successful-payment transaction; GetMyPaymentInvoicesAsync /
               GetPaymentInvoicesAsync / GetPaymentInvoiceAsync enforce ownership and scope.
               src/PMP.Api/Controllers/InvoicesController.cs — GET /api/invoices/my, GET /api/invoices
               (Financial policy + filters), GET /api/invoices/{id}.
               Frontend: InvoicesPage (resident + financial views), client methods, /invoices route.
Verification:  dotnet test PropertyManagement.sln → 116 passed / 0 failed. Frontend tsc -b && vite build green;
               oxlint 0 errors. Migration 20260914214226_AddPaymentInvoices applied by DbSeeder.
Remaining:     Email invoice delivery remains out of scope for this task. CSV invoice export was added under
               GAP-025 (2026-09-15).
```

## 9. Next task selection policy

Order: (1) blocking/foundation, (2) required MVP, (3) high-priority functional requirements,
(4) required cross-module integration, (5) required testing, (6) security/NFR, (7) DevOps/deployment,
(8) technical debt, (9) post-MVP. Dependencies must be satisfied first.

**Next recommended task: `IMP-041 — Frontend test infrastructure (Vitest/RTL) + core page/route tests`.**
Rationale: with IMP-030 and IMP-031 closed, GAP-003 and GAP-004 are resolved and no P1 functional gaps remain.
Per the selection order, the remaining *required testing* item — IMP-041, closing the frontend slice of GAP-001 —
precedes security/NFR (IMP-044) and DevOps (IMP-042/043). IMP-033 (lease invariant) and the remaining post-MVP
gaps follow.

## 10. Confluence live dashboard (do not duplicate)

This plan is mirrored 1:1 to a single, continuously updated Confluence page:

| Property | Value |
| --- | --- |
| Title | `PMP Implementation Plan` |
| Page ID | `27262978` |
| Space | `PMP` (space id `688146`) |
| URL | https://mariamghevondyan05-1785014440825.atlassian.net/wiki/spaces/PMP/pages/27262978/PMP+Implementation+Plan |
| Created | 2026-09-10 (version 1); synchronized 2026-09-11 at version 7 (IMP-040) |
| Update rule | Update this **same** page after every completed/verified task — never create a second implementation-plan page |

Synchronization rule: `docs/IMPLEMENTATION_PLAN.md` and the Confluence page must always agree. If they diverge,
the verified repository state plus approved ADRs/requirements win, and **both** are corrected (the incorrect
version is never silently kept). The Confluence page titled `Project Plan` is a separate project-management
artifact (timeline/budget/risk/backlog) and must not be overwritten by this dashboard.

## 11. Change log

| Date | Change |
| --- | --- |
| 2026-09-10 | Initial plan created from ADRs, compliance audit, development plan and a verified code baseline; 13 gaps recorded; overall 54%, MVP 70%. |
| 2026-09-10 | Mirrored to the Confluence page `PMP Implementation Plan` (page id `27262978`, space `PMP`) as the live human dashboard; page recorded here so future updates target the same page. |
| 2026-09-10 | `IMP-006` implemented and verified — 12 new API integration tests, suite 9 → 21 passing. Dashboard counts corrected to Completed 1 / In Progress 15 / Not Started 9 / Blocked 1 / Gaps 3; MVP 70% → 87%, overall 54% → 61%. Added `GAP-014` (maintenance notification payload carries no request context). |
| 2026-09-10 | `IMP-020`/`IMP-021` implemented and verified — payment requests/orders now carry currency + purpose, have an explicit lifecycle (`Open`/`PartiallyPaid`/`Paid`/`Overdue`/`Cancelled`), per-user obligation calculation, an overdue sweep, cancellation, declined-attempt records, a resident dashboard with a notification bell and a manager "residents who owe" view. 26 new tests (15 unit + 11 API integration); suite 21 → 47 passing; solution build 0 warnings/0 errors. Added `GAP-015` (single-currency assumption) and `GAP-016` (no report export). Recorded that real card/bank processing remains `GAP-012`. Dashboard: Completed 1 / Verified 2 / In Progress 13 / Not Started 9 / Blocked 1 / Gaps 3; Post-MVP 67% → 72%, overall 61% → 63%. |
| 2026-09-10 | UI-readiness audit recorded — **6 new gaps** documented (GAP-017..GAP-022, UI-blocking). **`GAP-017` RESOLVED**: `GET /api/auth/me` now returns the caller's own identity via a typed `MeResponse` (id, email, first/last name, roles) sourced from the Auth record only, and the client shell hydrates from it, so the signed-in user survives a reload. Verified by API integration theory `SeededUser_CanLogin_AndMeReturnsOwnIdentity` (suite 47 passed / 0 failed, build 0 warnings / 0 errors, frontend build green). **Open:** GAP-018 (app shell/nav), GAP-019 (design-system foundation), GAP-020 (dashboard data), GAP-021 (frontend API coverage), GAP-022 (password UI) — being worked in the current cycle in that order, ahead of `IMP-040`. |
| 2026-09-10 | **`GAP-018` RESOLVED** (with a first slice of `GAP-019`) — one declarative navigation manifest (`frontend/src/navigation.tsx`) now drives both the sidebar and the route guards; `AppLayout.tsx` adds the shell (persistent sidebar on desktop, collapsible drawer + scrim on mobile, sticky top bar with section title and a user menu showing identity/roles/sign-out); `/forbidden` + a redirect there replace the silent bounce; the dashboard's duplicated topbar was removed. Design tokens and the first primitives/icon family landed (`components/Icon.tsx`). Verified: frontend `tsc -b && vite build` green, `oxlint` 0 errors (5 pre-existing warnings); backend unchanged (47 tests still passing). **Next:** GAP-020 (dashboard data), then GAP-022 (password UI), then GAP-021 (frontend API coverage). |
| 2026-09-10 | **`GAP-020` RESOLVED** — the dashboard is now data-driven and role-appropriate, composed client-side from existing role-scoped endpoints (no new API surface). It shows a KPI row per role (amount due/overdue, outstanding, overdue, collected, reconciliation, open requests, assigned work, unread), a "needs your attention" list and quick actions, refreshes on mount + window focus (no polling), and has loading/empty/error states. The link-card grid was dropped since the sidebar now owns navigation. Recorded decision: occupancy totals are deferred (would need an N+1 walk) in favour of plain unit counts. Verified: frontend build green, `oxlint` 0 errors. **Next:** GAP-022 (password UI), then GAP-021 (frontend API coverage). |
| 2026-09-10 | **`GAP-022` RESOLVED** — `change-password`, `forgot-password` and `reset-password` client methods added; `/forgot-password` and `/reset-password` are public routes (sign-in page links to recovery) and `/account/password` sits inside the shell via the user menu. Local validation (min length + confirmation match) with field-level errors; the reset token is surfaced in a marked development panel because email delivery is not configured (GAP-005). Also fixed the shared client to accept empty `200`/`204` bodies. Verified: frontend build green, `oxlint` 0 errors. **Remaining in the UI-readiness sequence:** GAP-021 (frontend API coverage) and the rest of GAP-019. |
| 2026-09-11 | **Confluence mirror synchronized — page v4.** The same page (`PMP Implementation Plan`, id `27262978`, space `PMP`) was updated to record the UI-readiness cycle: GAP-017/GAP-018/GAP-020/GAP-022 RESOLVED, GAP-019 and GAP-021 IN_PROGRESS, GAP-021 Maintenance slice verified, gaps 16 → 22, plus a v4 change-log entry, the refreshed module table and the updated next-step note. Overall 63% / MVP 87% carried over unchanged (no IMP acceptance criteria closed). No duplicate page was created. |
| 2026-09-10 | **`GAP-021` (Property module) done** — client gained `getProperty`, `updateProperty`, `updateBuilding`, `getUnit`, `updateUnit`; `PropertiesPage` gained inline edit flows for property/building/unit with labelled fields and status pills (emoji occupancy marker removed). Frontend build green, `oxlint` 0 errors. **Remaining:** Resident, Maintenance, Lease and Booking coverage (one module per step). |
| 2026-09-10 | **`GAP-021` (Resident module) done** — client gained `getResident`, `updateResident`, `moveOut`; `ResidentsPage` gained an edit dialog, a move-out dialog with an optional date, labelled search, status pills and empty/notice states (the assign-unit dialog heading now sits inside its form so the overlay renders correctly). Frontend build green, `oxlint` 0 errors. **Remaining:** Maintenance, Lease and Booking coverage; the rest of GAP-019; the Confluence checkpoint. |
| 2026-09-11 | **`GAP-021` (Maintenance module) done** — client gained `getMaintenanceRequest`, `setMaintenancePriority`, `uploadMaintenanceAttachment`, `status`/`priority` filters and an optional comment on assign/status/confirm; `request()` now accepts a `FormData` body so multipart uploads keep the browser boundary. `MaintenancePage` rebuilt against the ADR-0009 state machine: role-aware filters, status/priority pills, detail panel that re-reads `GET /maintenance/{id}` (attachments, history, timestamps), manager assign + priority change, technician start/complete, resident confirm/reopen/cancel, comment capture, attachment upload gated to requester/assignee/manager, and loading/empty/error states. Taste pre-flight applied: filters apply immediately (no separate submit step, with a "shown" count), status colours reuse the shared `pill--*` vocabulary so the state machine reads identically across modules, detail sections use a token-styled `.indent h4` heading treatment instead of unstyled `h4`/`dl`, and every control keeps a visible label. Verified: frontend `tsc -b && vite build` green, `oxlint` 0 errors (4 pre-existing warnings, none in the new code — down from 5); backend unchanged (no API edits) and `dotnet test PropertyManagement.sln` re-run → 47 passed / 0 failed. Behaviour verified against the running API (http://localhost:5070) with real tokens: `POST /maintenance/{id}/priority` → priority changed Medium→High; `POST /maintenance/{id}/attachments` (multipart, requester token) → HTTP 200 and `GET /maintenance/{id}` then reports `attachmentCount=1`/`attachments=1`/`history=1`; `GET /maintenance?status=Submitted` returns only matching rows (0 mismatches) and `?priority=Urgent` filters correctly. **Note:** the verification created one maintenance request (unit A-1xx, submitted by the seeded resident) plus one attachment in the dev database, left consistent (row + stored file). **Remaining:** Lease and Booking client/UI coverage, the rest of GAP-019. |
| 2026-09-11 | **`GAP-021` (Lease + Booking) done — GAP-021 now fully RESOLVED.** Lease: the client gained `updateLease`, `getLeaseDocuments` and `uploadLeaseDocument` (multipart, browser boundary preserved), `getLease` is now typed `LeaseDetailDto` (documents + version history) instead of `history: unknown[]`, and `getLeases` takes a status filter. `LeasesPage` rebuilt: role-aware status filter, detail panel that re-reads `GET /leases/{id}`, term updates (each save is a new API-recorded version), document upload/list and terminate-with-reason, plus loading/empty/notice states. Booking: the client gained `getFacility`, `getAvailability` and `updateFacility`; `getBookings` supports `facilityId`/`mineOnly` and `createFacility` now passes the cancellation window. `BookingsPage` rebuilt: manager facility create/edit (hours, slot length, cancellation window, active), per-facility availability for a chosen day (free/booked slots), resident slot reservation with duration and overlap-aware start times, and cancel-with-reason. **Verified:** frontend `tsc -b && vite build` green, `oxlint` 0 errors (4 pre-existing warnings, none in the new code); **behaviour smoke-tested against the running API (http://localhost:5070) with a real manager token** — `GET /api/leases` → 1 Active lease; `GET /api/leases/{id}` → `documents:[]`/`history:[]`; `GET /api/leases/{id}/documents` → 200 `[]`; `GET /api/bookings/facilities` → 1 facility (Community Gym, 60-min slots, 2h cancellation window); `GET /api/bookings/facilities/{id}` → detail; `GET /api/bookings/facilities/{id}/availability?date=…` → open 480 / close 1320 / slot 60 / 0 bookings; `GET /api/bookings` → 1. The check was **read-only** (only a login/refresh row was written) so no dev data was mutated. Backend unchanged. **Remaining:** none for GAP-021 (Maintenance *service-level* coverage stays with IMP-040). |
| 2026-09-11 | **`GAP-019` RESOLVED** — the design system is complete. [`frontend/src/index.css`](frontend/src/index.css) now carries the full token-based primitive set: buttons (`.btn` + `--primary`/`--ghost`/`--danger`/`--sm`), fields (`.field` + `.field-hint`), tables (`.table-wrap`/`.table`), the status pill (`.pill--ok|warn|danger|info|muted`), alerts (`.alert--error|success|info`) and one empty/loading/error shape (`.state` + `--empty`/`--loading`/`--error`, with a `prefers-reduced-motion` guard); the legacy base styles were converted from raw hex to tokens (a few tint/strong tokens added) so the palette has a single source. Every remaining pre-GAP-019 `.badge` chip was migrated to the status pill (`AdminUsersPage`, `CommunicationPage`, `SecurityPage`), and `.error`/`.hint` are now aliases of the alert primitive so existing screens keep working. **Icon family decision recorded:** keep the in-house 24x24 stroke family ([`Icon.tsx`](frontend/src/components/Icon.tsx:1)) rather than adopt an icon library — no extra dependency/licensing surface, tree-shakeable, and the shapes already match the design language. **Verified:** frontend build green, `oxlint` 0 errors (4 pre-existing warnings). Overall 63% / MVP 87% carried over unchanged (the gap closures close no IMP acceptance criteria on their own). |
| 2026-09-11 | **Confluence mirror synchronized — page v5.** The same page (`PMP Implementation Plan`, id `27262978`, space `PMP`) now records GAP-019 and GAP-021 as RESOLVED, the Lease + Booking checkpoint (client methods, rebuilt screens, read-only live API smoke checks), the icon-family decision and a v5 change-log entry; the page's module-table percentages were reconciled to this plan (Frontend 75%, Database/API 80%, IMP-026 75%, IMP-052 50%). Overall 63% / MVP 87% and the task counts are identical on both sides. No duplicate page was created. |
| 2026-09-11 | **Documentation professionalization + repository hygiene (IMP-050, IMP-051, IMP-052 COMPLETE).** README rewritten as a system-analyst case study (overview, objectives, stakeholders, capabilities, analyst perspective, traceability, FR/NFR, architecture diagram, stack, structure, API, database, security, testing, ADRs, development approach, status, roadmap, run steps, documentation index, deliverables). Added [`docs/traceability.md`](docs/traceability.md) and [`docs/README.md`](docs/README.md); added [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`SECURITY.md`](SECURITY.md); replaced the frontend Vite-template README. Corrected `FR-COM-006` to PARTIAL (email record-only) in the compliance audit (roll-up 72→71 MET, 2→3 PARTIAL) and refreshed its date; marked ADR-0007 partially superseded by ADR-0012; fixed the glossary (`four`→nine module DbContexts) and the historical dev-plan drift (SPA path, schema wording) with a status banner. Removed `Class1.cs` ×5, `UnitTest1.cs`, `body.json` and the unused `LoggingNotificationService`; recorded `GAP-023` (repo-root-relative links inside `docs/` do not render on GitHub); extended `.gitignore` (test/coverage output, `.env.*`, `*.db-journal`, SPA cache) and stopped ignoring `docker-compose.yml` so deployment packaging (IMP-043) can be committed. Verified: `dotnet test PropertyManagement.sln` → **46 passed / 0 failed** (23 unit + 23 API integration), build 0 warnings / 0 errors. Dashboard: Completed 1→4, In Progress 13→12, Not Started 9→7; cross-cutting 12%→26%, **overall 63%→67%**, MVP 87% (unchanged). `GAP-010` RESOLVED. |
| 2026-09-11 | **`IMP-040` COMPLETE - TDD discipline established.** Six module test files added (`AuthTokenServiceTests`, `ResidentServiceTests`, `LeaseServiceTests`, `CommunicationServiceTests`, `BookingServiceTests`, `SecurityServiceTests`) plus a shared `RecordingCommunicationService` double and Booking/Security project references. Tests were written at the ADR-0005 `Result` seam against existing behaviour (characterization/regression coverage) so the red -> green loop applies to *new* behaviour going forward; the workflow is documented in [`docs/TESTING.md`](docs/TESTING.md) and the `.roo/skills/tdd` skill. **No production code was changed.** Suite 46 -> **101 passed / 0 failed** (14 test files), build 0 warnings / 0 errors. `IMP-004` history-retention evidence gap closed (85 -> 95). GAP-001 narrowed (background-job/attachment paths and frontend tests remain). Dashboard: Completed 4 -> 5, In Progress 12 -> 11; cross-cutting 26% -> 50%, MVP 87% -> 89%, **overall 67% -> 73%**. Next task IMP-041. No CI created (IMP-042 / GAP-009). Added GAP-024. |
| 2026-09-11 | **Confluence mirror synchronized - page v7.** The same page (`PMP Implementation Plan`, id `27262978`, space `PMP`) was updated to IMP-040 COMPLETE with the per-module service tests, the TDD strategy/workflow links, the refreshed dashboard (overall 73%, MVP 89%; Completed 5 / In Progress 11; gaps 24 incl. GAP-024), the GAP-001 narrowing, the module table (Resident 95%, Testing 70%, Lease/Communication/Booking/Security test blockers resolved), the next-task note (IMP-041) and a v7 change-log entry. No duplicate page was created. |
| 2026-09-14 | **`IMP-053` COMPLETE — payment invoices (receipts).** A successful payment now creates exactly one `PaymentInvoice` (unique `PaymentTransactionId` FK) with a deterministic `INV-YYYY-NNNNNN` number from a per-year sequence table; failed/declined payments create none and a paid request cannot be paid again. Added `GET /api/invoices/my`, `GET /api/invoices` (Financial policy + filters), `GET /api/invoices/{id}` (ownership/scope enforced server-side). Added the resident + accountant/manager/admin `InvoicesPage` and `/invoices` route. Tests: 22 payment unit + 2 SQLite-constraint + 17 payment API integration (suite **116 passed / 0 failed**); frontend build + oxlint green. Added `GAP-025` (no invoice PDF export). Dashboard: Completed 5→6; post-MVP 72%→75%; overall 73%→74%, MVP 89% unchanged. Confluence mirror synchronized (page v8). |
| 2026-09-15 | **`IMP-031` COMPLETE — immediate role revocation (GAP-003).** The JWT bearer handler now re-loads the caller's current roles (and active state) on every request and replaces stale role claims in the principal, so a removed/demoted role stops authorizing on the next request (≤1 request latency) and deactivated/deleted accounts are rejected immediately. Added [`RoleRevocationApiTests`](tests/PMP.Tests/Integration/RoleRevocationApiTests.cs:10) proving an issued technician token stops working after the Technician role is removed. Suite 116 → **117 passed / 0 failed**; build 0 warnings / 0 errors. Dashboard: Completed 6→7, Gaps 3→2; Auth 66%→78%, Security 0%→27%, Post-MVP 75%→80%, **overall 74%→76%**, MVP 89% unchanged. Next task IMP-030. Confluence mirror sync pending (page v9). |
| 2026-09-15 | **`IMP-030` COMPLETE — owner self-service profile update (GAP-004).** Added `PUT /api/auth/me` so an authenticated user can edit their own Identity name fields (first/last name + phone); the change is audited (`ProfileUpdated`) and the resident profile is kept in sync via `IResidentService.UpdateMyProfileAsync` (no-op for staff). The endpoint is caller-scoped by construction (no user id parameter). Added [`ProfileUpdateApiTests`](tests/PMP.Tests/Integration/ProfileUpdateApiTests.cs:11) (2 integration tests: 401 unauthenticated; self-edit reflected in `/api/auth/me` and `/api/residents/me` while another user's identity is untouched). Suite 117 → **119 passed / 0 failed**; build 0 warnings / 0 errors. Dashboard: Completed 7→8, Gaps 2→1; Auth 78%→85%, Post-MVP 80%→82%, **overall 76%→77%**, MVP 89% unchanged. Next task IMP-041. Confluence mirror sync pending (page v9). |
| 2026-09-15 | **`GAP-025` RESOLVED — invoice CSV export.** Added `GET /api/invoices/{id}/export` producing a portable CSV receipt (header + one row: number, payment date, resident, unit, property, purpose, amount, currency, method, status, transaction reference, confirmation number). Access reuses `GetPaymentInvoiceAsync`, so residents export only their own invoice and another resident receives 400. Format decision: CSV (no PDF-generation infrastructure in the project). Added 2 integration tests (own export succeeds; another resident is rejected). Suite 119 → **121 passed / 0 failed**; build 0 warnings / 0 errors. No task-weight change (IMP-053 already 100%). |
