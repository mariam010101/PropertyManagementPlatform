# PMP Implementation Progress

Overall: 61%
MVP: 87%

Completed: 1
In Progress: 15
Not Started: 9
Blocked: 1
Gaps: 3

Last Updated: 2026-09-10

Next Recommended Task: IMP-040

---

## 0. How to read this plan

This file is the authoritative living implementation roadmap for PMP. It is derived from the
approved ADRs ([`docs/adr/`](docs/adr/)), the functional requirements audit
([`docs/requirements-compliance.md`](docs/requirements-compliance.md)), the sharpened development plan
([`plans/pmp-development-plan.md`](plans/pmp-development-plan.md)), and direct verification of the repository.

Status vocabulary (§7 of the operating rules): `NOT_STARTED`, `IN_PROGRESS`, `BLOCKED`, `IMPLEMENTED`,
`NEEDS_REVIEW`, `VERIFIED`, `COMPLETE`, `GAP`, `DEFERRED`.

Dashboard counts: `Completed`/`In Progress`/`Not Started`/`Blocked`/`Gaps` count **tasks** by status
(Gap status = `IMP-030`, `IMP-031`, `IMP-033`). Separately,
[`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) documents **14** gaps
(GAP-001..GAP-014) — some gaps span multiple tasks.

> Correction (2026-09-10): the first published revision reported `Gaps: 4`, which double-counted one task.
> The verified count is **3** tasks with status `GAP` (1 + 15 + 9 + 1 + 3 = 29 tasks).

Completion % is **not** derived from file/endpoint/document counts. It is derived from verification state:
`100%` = implemented + acceptance criteria met + tests pass + integration complete + ADR/REQ compliant.
The only task so far that reaches that bar is `IMP-006`; every other task still carries at least one open
acceptance criterion and is therefore reported below 100%.

## 1. Verified baseline (evidence captured 2026-09-10)

| Check | Result | Evidence |
| --- | --- | --- |
| Solution builds | ✅ | `dotnet test PropertyManagement.sln` exit code 0 |
| Test suite | ✅ 21 passed / 0 failed (9 unit + 12 API integration), 7 test files | [`tests/PMP.Tests`](tests/PMP.Tests) |
| MVP API integration harness | ✅ real Kestrel host + throwaway SQLite, driven over HTTP (no test-only hooks in the API) | [`PmpApiFixture.cs`](tests/PMP.Tests/Integration/PmpApiFixture.cs:1), [`MvpEndToEndApiTests.cs`](tests/PMP.Tests/Integration/MvpEndToEndApiTests.cs:1) |
| Modules wired in host | ✅ 9 modules + shared kernel | [`Program.cs`](src/PMP.Api/Program.cs:35) |
| Cross-module adapters | ✅ `IUnitOccupancyProvider`→`UnitOccupancyProvider`, `INotificationService`→`PersistedNotificationService` | [`Program.cs`](src/PMP.Api/Program.cs:49) |
| API surface | ✅ Controllers for Auth, Properties, Residents, Maintenance, Communication, Leases, Payments, Bookings, Security | [`Controllers/`](src/PMP.Api/Controllers) |
| EF Core migrations | ✅ 10 initial migrations (Auth, Property, Resident, Maintenance, Communication, Lease, Payment, Booking, Security) | [`src/`](src) |
| Background services | ✅ 3 hosted services (maintenance auto-close, lease lifecycle, payment due dates) | [`AutoCloseHostedService.cs`](src/PMP.Modules.Maintenance/Background/AutoCloseHostedService.cs:15), [`LeaseLifecycleHostedService.cs`](src/PMP.Modules.Lease/Background/LeaseLifecycleHostedService.cs:12), [`PaymentDueDateHostedService.cs`](src/PMP.Modules.Payment/Background/PaymentDueDateHostedService.cs:12) |
| Email confirmation enforced | ✅ `RequireConfirmedEmail = true`, checked at login | [`ServiceCollectionExtensions.cs`](src/PMP.Modules.Auth/Extensions/ServiceCollectionExtensions.cs:49), [`AuthService.cs`](src/PMP.Modules.Auth/Services/AuthService.cs:144) |
| Email **transport** (SMTP/provider) | ❌ none (notifications record channel only) | no `Smtp`/`MailKit`/`SendGrid`/`IEmailSender` anywhere in [`src/`](src) |
| Roles | 5: Administrator, PropertyManager, Resident, Technician, Accountant (no Security role) | [`AppRoles.cs`](src/PMP.Shared/Common/AppRoles.cs:8) |
| Frontend | ✅ React 19 + TS + Vite, 12 pages, role-guarded routes | [`App.tsx`](frontend/src/App.tsx:25) |
| Frontend tests | ❌ no test runner/script/deps | [`package.json`](frontend/package.json:6) |
| CI/CD, Docker, deployment assets | ❌ none in repository root | repository listing |
| Dead template scaffolding / placeholders | ⚠️ `Class1.cs` in 5 projects, `UnitTest1.cs`, [`body.json`](body.json) | [`src/`](src) |
| `TODO`/`FIXME`/`NotImplementedException` in `src/` | ✅ 0 matches | regex scan of [`src/`](src) |

**Documented state** (README, compliance audit, ADR-0012): 72/78 FRs MET, 2 PARTIAL, 5 NOT MET (BR-012);
BR-001..BR-011 "implemented end-to-end".
**Actual state**: the same code surface exists and compiles, but it is **not verified** by automated tests
outside Maintenance/Property, has no integration/API test coverage, no CI, no frontend tests, and the
BR-012 mobile platform does not exist. Progress therefore reflects verified implementation, not breadth.

### Documented-state vs actual-code conflicts (recorded, not silently resolved)

| # | Conflict | Resolution taken |
| --- | --- | --- |
| C-1 | ADR-0007 (MVP scope) is still `accepted` but ADR-0012 added the deferred modules and re-scoped MVP; the MVP definition also differs between ADR-0007 (no RBAC admin UI) and [`README.md`](README.md:130) (RBAC admin UI counted as MVP). | Newest approved ADR wins: ADR-0012 is authoritative for module scope; ADR-0007 remains authoritative for the MVP slice. Conflict recorded as GAP-010 (ADR-0007 not marked as superseded/incremented). |
| C-2 | `plans/pmp-development-plan.md` locates the SPA at `src/frontend/`; the SPA actually lives at [`frontend/`](frontend). | Code is truth; doc drift recorded as GAP-010. |
| C-3 | Property aggregate entity is [`ManagedProperty`](src/PMP.Modules.Property/Entities/ManagedProperty.cs:9) (`ManagedProperty` FK naming inside the Property module); older references (editor state, prose) use `Property`. | Code is truth; naming drift recorded as GAP-010. |
| C-4 | Compliance audit marks FR-COM-006 (email + in-app notifications) 🟢 MET, but email is only *recorded* with a delivery status — no transport exists. | Recorded as GAP-005; FR-COM-006 downgraded to partial in this plan (IMP-023). |

## 2. Progress model (weights are auditable)

Each task carries an effort weight. Completion = Σ(weight × verified %) across all tasks.

| Group | Weight | Earned | % |
| --- | --- | --- | --- |
| MVP (IMP-001..007) | 60 | 52.3 | **87%** |
| Post-MVP + remaining FRs (IMP-020..035) | 55 | 37.1 | 67% |
| Cross-cutting: tests, CI, DevOps, security, docs (IMP-040..052) | 38 | 3.3 | 9% |
| **Total** | **153** | **92.7** | **61%** |

### Module / layer progress

| Area | Weight basis | % | Primary blocker to reaching 100% |
| --- | --- | --- | --- |
| Auth (BR-001, BR-008) | IMP-001, IMP-002, IMP-030, IMP-031, IMP-032 | 66% | unit-level lockout tests, owner profile endpoint, JWT revocation latency, blocked suspension state |
| Property (BR-003) | IMP-003 | 90% | no manager-role validation on create; duplicate-unit rule covered by unit test only |
| Resident (BR-002) | IMP-004 | 85% | occupancy-history retention not covered by a test |
| Maintenance (BR-004) | IMP-005 | 95% | 7-day auto-close job and attachment path untested |
| Payment + Accounting (BR-005, BR-011) | IMP-020, IMP-021 | 71% | simulated payment provider, no tests |
| Lease (BR-006) | IMP-022 | 70% | BRULE-LEASE-001 invariant, document storage hardening, no tests |
| Communication (BR-007) | IMP-023 | 70% | no email transport, no tests |
| Booking (BR-009) | IMP-024 | 70% | no tests, no BRULE page published |
| Security/Visitors (BR-010) | IMP-025 | 70% | no dedicated Security role, no access-control hardware integration, no tests |
| Mobile (BR-012) | IMP-027 | 0% | not started (post-MVP) |
| Backend (MVP backend tasks IMP-001..006) | IMP-001..006 | 90% | unit-level coverage for lockout/auto-close; hardening |
| Frontend (web) | IMP-007, IMP-026, IMP-041 | 75% | no frontend test tooling |
| Database/API | migrations + endpoint surface | 80% | MVP surface verified end-to-end; post-MVP endpoints unverified |
| Testing | IMP-006, IMP-040, IMP-041 | 35% | MVP API layer covered; no post-MVP module tests, no frontend tests |
| Security (NFR/hardening) | IMP-031, IMP-034, IMP-044 | 0% | no review performed, no hardening tasks started |
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
| IMP-004 | Resident: profile auto-provisioning, edit, effective-dated unit assignment, move-out, deactivate, occupancy history, search | Resident | P1 | IMP-001, IMP-003 | FR-RES-001..007 | 0008, 0010 | NEEDS_REVIEW | 85 | 8 |
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
| IMP-020 | Payment: invoices, balances, pay, confirmations, history, due-date reminders | Payment | P2 | IMP-004, IMP-022 | FR-PAY-001..007 | 0012 | IN_PROGRESS | 70 | 10 |
| IMP-021 | Financial reporting, reconciliation, Accountant scoped access | Payment | P2 | IMP-020 | FR-ACCT-001..004 | 0004, 0012 | NEEDS_REVIEW | 75 | 6 |
| IMP-022 | Lease: agreements, version history, secure documents, expiry/archive sweep | Lease | P2 | IMP-003, IMP-004 | FR-LEASE-001..007 | 0012 | IN_PROGRESS | 70 | 8 |
| IMP-023 | Communication: persisted notifications (InApp/Email channels), announcements, read state | Communication | P2 | IMP-001 | FR-COM-001..006 | 0012 | IN_PROGRESS | 70 | 8 |
| IMP-024 | Booking: facilities, availability, overlap-free reservations, cancellation window | Booking | P2 | IMP-003, IMP-004 | FR-BOOK-001..007 | 0012 | IN_PROGRESS | 70 | 6 |
| IMP-025 | Security & visitors: visitor register/check-in/out, access grants with expiry, logs | Security | P2 | IMP-003, IMP-024 | FR-SEC-001..007 | 0012 | IN_PROGRESS | 70 | 6 |
| IMP-026 | Frontend post-MVP pages/routes (payments, leases, communication, bookings, security) | Frontend | P2 | IMP-020..025 | FR-PAY/LEASE/COM/BOOK/SEC sets | — | NEEDS_REVIEW | 75 | 8 |
| IMP-027 | Mobile platform access (native app or PWA) on the existing API | Mobile | P2 | IMP-007, IMP-026 | FR-MOBILE-001..005 | 0007, 0012 | NOT_STARTED (deferred) | 0 | 12 |
| IMP-030 | Dedicated owner "edit my profile" endpoint that also updates Identity name fields | Auth | P1 | IMP-001 | FR-AUTH-005, BRULE-AUTH-006 | 0003 | GAP | 25 | 2 |
| IMP-031 | Immediate role-revocation: short access-token TTL + per-request active-role recheck or security stamp | Auth | P1 | IMP-002 | FR-RBAC-006, BRULE-RBAC-006 | 0003, 0004 | GAP | 0 | 3 |
| IMP-032 | Distinct "suspended" account state (if the requirement is confirmed) | Auth | P3 | — | BRULE-AUTH-005 | — | BLOCKED | 0 | 1 |
| IMP-033 | Enforce "active resident must have an active lease" as a hard invariant | Lease/Resident | P2 | IMP-022 | BRULE-LEASE-001 | 0012 | GAP | 0 | 2 |
| IMP-034 | Hardening: validate Technician role on maintenance assignment; validate manager role on property create | Maintenance/Property | P3 | IMP-003, IMP-005 | BRULE-MNT-003, BRULE-PROP-003 | 0006 | NOT_STARTED | 0 | 2 |
| IMP-035 | Publish BRULE pages for BR-009/BR-010 in Confluence (or record the decision that they are not required) | Docs | P3 | — | FR-BOOK/FR-SEC | — | NOT_STARTED | 0 | 1 |

**Acceptance criteria (abbreviated for tasks above 0%)**

- **IMP-020:** payment rows append-only with unique transaction reference; confirmations returned; balances = invoice amount − paid; due-date sweep tested.
- **IMP-021:** report/reconciliation endpoints restricted to `Financial` policy; Accountant denied on non-financial policies; export format defined and tested.
- **IMP-022:** each lease change creates a version row; documents access-controlled; expiry sweep marks past-end leases and notices within 30 days; test coverage added.
- **IMP-023:** notification history queryable with delivery status; announcements filtered to the resident's property; email transport decision recorded (implement or explicitly descope FR-COM-006 email delivery).
- **IMP-024:** overlap rejection and cancellation-window rules unit-tested (SQLite client-side evaluation documented in ADR-0012).
- **IMP-025:** visitor check-in/out times recorded; expired grants treated as expired; access log immutable; role/ownership decision recorded (currently Property Manager/Admin act as security operator).
- **IMP-026:** each post-MVP page enforces the same server-side role guard as its endpoints.
- **IMP-027:** not to be started until IMP-040 is green (IMP-006 is complete).
- **IMP-030:** owner can update own name/profile; Identity `FirstName`/`LastName` (or equivalent) updated; audit event recorded; test covers self vs other-user access.
- **IMP-031:** revoking a role takes effect on the next request (≤1 request latency), with a test proving an old token cannot use the removed role.
- **IMP-032:** requires a product decision on BRULE-AUTH-005 (whether "suspended" is a distinct state or satisfied by active/inactive). Do not implement until answered.
- **IMP-033:** assignment/creation paths reject state where an active resident has no lease; decision recorded on retroactive data handling.
- **IMP-034:** assigning a non-Technician user is rejected; creating a property for a non-manager user is rejected; tests added.

## 6. Cross-cutting, testing, DevOps and tech-debt tasks

| ID | Task | Area | Pri | Deps | REQ/ADR | Status | % | W |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| IMP-040 | Automated test expansion: per-module service tests for Resident, Auth, Payment, Lease, Communication, Booking, Security; shared test host/fixtures | Testing | P1 | IMP-001..005 | ADR-0005 | IN_PROGRESS | 15 | 12 |
| IMP-041 | Frontend test infrastructure (Vitest/RTL) + core page/route tests | Testing | P2 | IMP-007 | — | NOT_STARTED | 0 | 4 |
| IMP-042 | CI pipeline: build + `dotnet test` + frontend lint/build on every push | DevOps | P2 | IMP-006, IMP-040 | — | NOT_STARTED | 0 | 4 |
| IMP-043 | Deployment packaging: Dockerfile/compose or equivalent, production SQLite path, secrets/JWT config, environment matrix | DevOps | P2 | IMP-042 | ADR-0011 | NOT_STARTED | 0 | 5 |
| IMP-044 | Security & NFR review and hardening: secrets handling, upload path safety, rate limiting/lockout tuning, HTTPS/HSTS, structured logging/observability | Security | P1 | IMP-001..005 | 0003, 0004 | NOT_STARTED | 0 | 6 |
| IMP-050 | Remove template scaffolding and stray artifacts (`Class1.cs` ×5, `UnitTest1.cs`, [`body.json`](body.json)) | Tech debt | P3 | — | — | NOT_STARTED | 0 | 2 |
| IMP-051 | Documentation sync: README status/paths, MVP definition (C-1), compliance-audit refresh, ADR-0007 superseded marker, `ManagedProperty` naming, BRULE-* coverage notes | Tech debt | P3 | — | ADR-0012 | NOT_STARTED | 0 | 2 |
| IMP-052 | Traceability registry: REQ → ADR → IMP → code → API → test, kept current with this plan | Tech debt | P2 | — | — | IN_PROGRESS | 50 | 3 |

**Acceptance criteria (abbreviated for tasks above 0%)**

- **IMP-040:** every module has at least service-level tests for its core rules; a shared SQLite/InMemory test host exists; suite runnable via `dotnet test`.
- **IMP-041:** `npm test` exists and covers login/route-guard and one data page; failures break the build.
- **IMP-042:** pipeline runs on push/PR and fails on build/test/lint errors.
- **IMP-043:** app runs from a clean container/packaged deployment with migrations + seed and no dev-only config.
- **IMP-044:** findings documented; each accepted finding becomes a task or an explicit accepted risk.
- **IMP-050/051:** repo contains no leftover scaffolding; docs match code with no open conflicts from §1.
- **IMP-052:** every REQ/ADR in scope maps to at least one IMP task and evidence, or is recorded as a traceability gap.

## 7. Traceability matrix (requirement → ADR → task → code/API → test)

`API` = controller surface, `Code` = service/entity, `Test` = existing automated coverage.

| Requirement set | ADR | Task(s) | API | Code | Test |
| --- | --- | --- | --- | --- | --- |
| FR-AUTH-001..008 | 0003, 0008 | IMP-001, IMP-030 | [`AuthController.cs`](src/PMP.Api/Controllers/AuthController.cs:28) | [`AuthService.cs`](src/PMP.Modules.Auth/Services/AuthService.cs), [`TokenService.cs`](src/PMP.Modules.Auth/Services/TokenService.cs) | ✅ API integration (IMP-006): login/confirm gate/refresh/logout; ⚠️ lockout untested |
| FR-RBAC-001..006 | 0004 | IMP-002, IMP-031 | [`AuthController.cs`](src/PMP.Api/Controllers/AuthController.cs:127) | [`AppPolicies.cs`](src/PMP.Shared/Common/AppPolicies.cs), [`AppRoles.cs`](src/PMP.Shared/Common/AppRoles.cs:8) | ✅ API integration (IMP-006): anonymous 401 + per-role 403; ⚠️ full policy matrix untested |
| FR-PROP-001..007 | 0006, 0010, 0011 | IMP-003, IMP-034 | [`PropertiesController.cs`](src/PMP.Api/Controllers/PropertiesController.cs:25) | [`PropertyService.cs`](src/PMP.Modules.Property/Services/PropertyService.cs) | ✅ API integration (IMP-006) + 2 unit tests ([`PropertyServiceTests.cs`](tests/PMP.Tests/PropertyServiceTests.cs:10)) |
| FR-RES-001..007 | 0008, 0010 | IMP-004 | [`ResidentsController.cs`](src/PMP.Api/Controllers/ResidentsController.cs:24) | [`ResidentService.cs`](src/PMP.Modules.Resident/Services/ResidentService.cs), [`UnitOccupancyProvider.cs`](src/PMP.Modules.Resident/Abstractions/UnitOccupancyProvider.cs) | ✅ API integration (IMP-006): auto-provisioning + own profile; ⚠️ history retention untested |
| FR-MNT-001..008 | 0009 | IMP-005, IMP-034 | [`MaintenanceController.cs`](src/PMP.Api/Controllers/MaintenanceController.cs:27) | [`MaintenanceService.cs`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs) | ✅ API integration (IMP-006) + 3 unit tests ([`MaintenanceServiceTests.cs`](tests/PMP.Tests/MaintenanceServiceTests.cs:15)); ⚠️ auto-close job untested |
| FR-PAY-001..007 | 0012 | IMP-020 | [`PaymentsController.cs`](src/PMP.Api/Controllers/PaymentsController.cs:32) | [`PaymentService.cs`](src/PMP.Modules.Payment/Services/PaymentService.cs) | ❌ none |
| FR-ACCT-001..004 | 0004, 0012 | IMP-021 | [`PaymentsController.cs`](src/PMP.Api/Controllers/PaymentsController.cs:74) | [`PaymentService.cs`](src/PMP.Modules.Payment/Services/PaymentService.cs) | ❌ none |
| FR-LEASE-001..007 | 0012 | IMP-022, IMP-033 | [`LeasesController.cs`](src/PMP.Api/Controllers/LeasesController.cs:32) | [`LeaseService.cs`](src/PMP.Modules.Lease/Services/LeaseService.cs) | ❌ none |
| FR-COM-001..006 | 0012 | IMP-023 | [`CommunicationController.cs`](src/PMP.Api/Controllers/CommunicationController.cs:30) | [`CommunicationService.cs`](src/PMP.Modules.Communication/Services/CommunicationService.cs), [`PersistedNotificationService.cs`](src/PMP.Modules.Communication/Services/PersistedNotificationService.cs) | ❌ none |
| FR-BOOK-001..007 | 0012 | IMP-024 | [`BookingsController.cs`](src/PMP.Api/Controllers/BookingsController.cs:30) | [`BookingService.cs`](src/PMP.Modules.Booking/Services/BookingService.cs) | ❌ none |
| FR-SEC-001..007 | 0012 | IMP-025 | [`SecurityController.cs`](src/PMP.Api/Controllers/SecurityController.cs:31) | [`SecurityService.cs`](src/PMP.Modules.Security/Services/SecurityService.cs) | ❌ none |
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

## 9. Next task selection policy

Order: (1) blocking/foundation, (2) required MVP, (3) high-priority functional requirements,
(4) required cross-module integration, (5) required testing, (6) security/NFR, (7) DevOps/deployment,
(8) technical debt, (9) post-MVP. Dependencies must be satisfied first.

**Next recommended task: `IMP-040 — Automated test expansion (per-module service tests + shared test host)`.**
Rationale: IMP-006 verified the MVP slice through the API, so the MVP is no longer unverified; the remaining
testing gaps are per-module service coverage for the post-MVP modules, the untested job/attachment paths and
the frontend. IMP-040 is also a precondition for IMP-042 (CI). IMP-027 (mobile) must still wait until
IMP-040 is green. Per the selection order, security/NFR (IMP-044) follows testing.

## 10. Confluence live dashboard (do not duplicate)

This plan is mirrored 1:1 to a single, continuously updated Confluence page:

| Property | Value |
| --- | --- |
| Title | `PMP Implementation Plan` |
| Page ID | `27262978` |
| Space | `PMP` (space id `688146`) |
| URL | https://mariamghevondyan05-1785014440825.atlassian.net/wiki/spaces/PMP/pages/27262978/PMP+Implementation+Plan |
| Created | 2026-09-10 (version 1) |
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
