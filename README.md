# Property Management Platform (PMP)

A modular-monolith SaaS platform for managing residential properties — buildings, units, residents,
maintenance, leases, payments, communication, facility booking and physical security — built from a full
system-analysis lifecycle: business requirements and business rules, functional and non-functional
requirements, use cases and business processes, architecture decisions, a traceability chain from requirement
to code to test, and a documented, honest status against the plan.

The platform is implemented in **ASP.NET Core 9 + EF Core 9** (backend) and **React 19 + TypeScript + Vite**
(frontend), organised as **nine domain modules inside one deployable**, with a single SQLite database and
JWT-based authentication with role-based access control.

> **Repository role.** This GitHub repository is the **source code plus the selected engineering/analysis
> documentation**. Detailed requirements catalogues, business-rule pages and process modelling are authored in
> **Confluence**; delivery is tracked in **Jira**. This repository summarises and links to them rather than
> duplicating them, so the two cannot diverge. Start with [`docs/README.md`](docs/README.md).

---

## Repository at a glance

| | |
| --- | --- |
| **Domain** | Residential property management (PropTech / SaaS) |
| **Architecture** | Modular monolith — 9 modules, 1 deployable, 1 SQLite file ([ADR-0001](docs/adr/0001-modular-monolith.md), [ADR-0011](docs/adr/0011-sqlite-single-file-module-boundaries.md)) |
| **Business areas** | BR-001 … BR-012 (78 functional requirements) |
| **Backend** | ASP.NET Core 9, EF Core 9, ASP.NET Core Identity, JWT bearer |
| **Frontend** | React 19, TypeScript, Vite, React Router |
| **Authorization** | 5 roles + policy-based RBAC ([ADR-0004](docs/adr/0004-rbac-roles-and-policies.md)) |
| **Tests** | xUnit — unit + API integration tests (real Kestrel host, throwaway SQLite); test-first workflow in [`docs/TESTING.md`](docs/TESTING.md) |
| **Decisions** | 12 Architecture Decision Records ([`docs/adr/`](docs/adr/)) |
| **Traceability** | BR → FR → BRULE → ADR → IMP task → code/API → test ([`docs/traceability.md`](docs/traceability.md)) |
| **Status** | MVP complete; post-MVP modules implemented. Verified overall progress and remaining work are tracked in [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) |

---

## Project Overview

### The business problem

Managing a residential portfolio is fragmented across spreadsheets, chat threads and paper. Property managers
lose track of who occupies which unit, residents have no structured way to report maintenance issues, rent and
fees are reconciled manually, lease documents expire unnoticed, shared facilities are double-booked, and
building access is recorded inconsistently. The result is slow response, poor auditability and disputes.

### The proposed solution

The Property Management Platform centralises those operations in one role-aware system:

- a single register of **properties → buildings → residential units**, with operational status and *derived*
  occupancy;
- **residents** linked to units through effective-dated assignments that preserve full occupancy history;
- a **maintenance request lifecycle** with priorities, technician assignment, history and a resident
  confirmation gate;
- **leases** with versioned change history and documents that expire on a controlled schedule;
- **payments** with per-resident obligations, confirmations, financial reporting and invoice receipts;
- **communication** (notifications and announcements), **facility booking** and **visitor/access** management;
- everything governed by **role-based access control** with least privilege and an audit trail.

### Target users and business context

The platform is intended for property-management companies operating multiple residential developments. Each
property is owned operationally by one assigned manager; staff self-register through provisioning rather than
claiming data, and residents self-register and are then linked to a unit by staff.

### System purpose

To provide a single, auditable, role-appropriate source of truth for the day-to-day operation of managed
residential buildings — started deliberately as one coherent vertical slice and grown module by module.

---

## Business Objectives

| # | Objective |
| --- | --- |
| BO-1 | Replace fragmented manual records with one authoritative property/resident register. |
| BO-2 | Give residents a structured, trackable channel for maintenance issues and their resolution status. |
| BO-3 | Make rent/fee obligations and outstanding balances visible and reconcilable. |
| BO-4 | Keep lease terms and documents current, versioned and visible before they expire. |
| BO-5 | Give management clear operational and financial reporting scoped to their responsibilities only. |
| BO-6 | Record visitor and access activity for security and audit. |
| BO-7 | Enforce least-privilege access so each actor sees only what their role requires. |

These objectives are the anchor of the traceability chain: business objective → business requirement (BR) →
functional requirement (FR) → implementation task → code → test.

---

## Stakeholders

Only roles the platform actually models are listed.

| Stakeholder / Actor | Type | Interest in the system |
| --- | --- | --- |
| **Administrator** | Actor (system role) | Provisions staff, assigns roles, oversees all data; needs full visibility and control. |
| **Property Manager** | Actor (system role) | Runs day-to-day operations for assigned properties; needs scoped access to properties, residents, maintenance, leases, payments and facilities. |
| **Technician** | Actor (system role) | Handles assigned maintenance work; needs only their assignments and status updates. |
| **Resident** | Actor (system role) | Occupant; needs to submit requests, see balances, book facilities and receive announcements — for their own data only. |
| **Accountant** | Actor (system role) | Needs financial records, reporting and reconciliation, and must be kept out of operational data. |
| **Property owner / management company** | Stakeholder (indirect) | Consumes reporting and depends on data accuracy and auditability. |

> There is currently **no dedicated "Security Operator" role** — property managers and administrators act as
> security operators for visitor/access management. This is recorded, not hidden (see
> [`docs/requirements-compliance.md`](docs/requirements-compliance.md) §11).

---

## Key Business Capabilities

| # | Capability | Module | Scope |
| --- | --- | --- | --- |
| 1 | Authentication & User Management | `PMP.Modules.Auth` | Registration, login, refresh/logout, password reset/change, email confirmation, audit events, staff provisioning |
| 2 | Role-Based Access Control | `PMP.Modules.Auth` + `PMP.Shared` | 5 roles, policy-based authorization, admin user/role management, least privilege |
| 3 | Property Management | `PMP.Modules.Property` | Property → Building → Unit CRUD, operational status, derived occupancy, manager scoping |
| 4 | Resident Management | `PMP.Modules.Resident` | Profiles, effective-dated unit assignment, move-out/deactivation, occupancy history |
| 5 | Maintenance Management | `PMP.Modules.Maintenance` | Request lifecycle state machine, priorities, assignment, attachments, history, resident confirmation, auto-close |
| 6 | Payment Management | `PMP.Modules.Payment` | Invoices/requests, obligations, payments, confirmations, history, due-date alerts, invoice receipts |
| 7 | Financial Reporting & Accountant Access | `PMP.Modules.Payment` | Reports, reconciliation, outstanding-by-resident, scoped accountant access |
| 8 | Lease & Document Management | `PMP.Modules.Lease` | Lease records, version history, document storage, expiry/notice lifecycle |
| 9 | Communication & Notifications | `PMP.Modules.Communication` | Persisted notifications, announcements scoped by property, read state |
| 10 | Facility Booking | `PMP.Modules.Booking` | Facilities, availability, overlap-free reservations, cancellation windows |
| 11 | Physical Security & Visitor Management | `PMP.Modules.Security` | Visitor register, check-in/out, access grants with expiry, access log |

**MVP** (per [ADR-0007](docs/adr/0007-mvp-scope.md)) is capabilities **1–5** plus RBAC. Capabilities **6–11**
were approved and delivered as the post-MVP increment ([ADR-0012](docs/adr/0012-post-mvp-modules.md)).
**Mobile platform access (BR-012) is not implemented** — see [Project Status](#project-status).

---

## System Analyst Perspective

This project is a case study in *analysis leading implementation* — the repository contains the artefacts, not
just the code. The analysis work it demonstrates:

- **Requirements elicitation & analysis** — 12 business areas broken into 78 functional requirements
  (`FR-<AREA>-NNN`) and an explicit set of business rules (`BRULE-<AREA>-NNN`).
- **Business-rule modelling** — invariants such as unique payment transaction references, immutable closed
  maintenance requests, effective-dated occupancy, and one-manager-per-property scoping.
- **Functional & non-functional requirements** — functional coverage is audited per requirement; non-functional
  concerns (security, performance, availability, observability) are handled honestly, including where they are
  *not* yet defined.
- **User stories & acceptance criteria** — every implementation task (`IMP-XXX`) carries acceptance criteria
  that must be *provably* satisfied; a task stays unchecked until then.
- **Use cases & business processes** — actors, preconditions, main/alternative/exception flows and process
  modelling are authored in Confluence and summarised here.
- **Requirements traceability** — a maintained chain from business objective to requirement to decision to code
  to test, plus a first-class **gap register** for everything that breaks it ([`docs/traceability.md`](docs/traceability.md)).
- **System architecture & data modelling** — an approved architecture (modular monolith, module ownership) and
  a domain model (entities, effective-dated relationships, derived state) documented as ADRs.
- **RBAC analysis** — a role/permission model with deny-by-default policies and ownership scoping, tested with
  positive and negative authorization cases.
- **API analysis & documentation** — a REST surface analysed per module and documented (controller overview +
  Swagger in development).
- **SDLC, Agile & QA traceability** — iterative delivery, a living implementation plan with an auditable
  progress model, and a test strategy that distinguishes *documented* coverage from *verified* coverage.
- **Technical decision documentation** — 12 ADRs recording context, decision, alternatives and consequences,
  including a **superseded** decision preserved for history.

The intent is to demonstrate the analysis through the artefacts and their consistency with the code — not
through self-description.

---

## Requirements & Traceability

### The chain

```
Business Objective
   └── BR-0NN                 business requirement
         ├── FR-<AREA>-0NN    functional requirement
         └── BRULE-<AREA>-0NN business rule / invariant
               └── ADR-0NNN   the decision that shaped the solution
                     └── IMP-0NN    implementation task + acceptance criteria
                           └── code / API          (controller + service + entity + migration)
                                 └── automated test  (xUnit unit + API integration)
                                       └── verified status recorded in the implementation plan

        any break in the chain  ──▶  GAP-0NN recorded in the gap analysis
```

### Identifiers used in this repository

| Prefix | Meaning | Example |
| --- | --- | --- |
| `BR-0NN` | Business Requirement | `BR-004` — Maintenance Management |
| `FR-<AREA>-0NN` | Functional Requirement | `FR-MNT-005` — notify residents on status change |
| `BRULE-<AREA>-0NN` | Business Rule / invariant | `BRULE-MNT-004` — closed requests are not modified |
| `ADR-0NNN` | Architecture Decision Record | `ADR-0009` — maintenance state machine |
| `IMP-0NN` | Implementation task + acceptance criteria | `IMP-005` — Maintenance module |
| `GAP-0NN` | Known gap / defect / decision-required item | `GAP-003` — role-revocation latency |

**Non-functional requirements do not yet have their own IDs** — that is recorded as a traceability gap
(`GAP-013`) rather than invented. See [`docs/traceability.md`](docs/traceability.md) for the full scheme.

### Where to read it

- [`docs/traceability.md`](docs/traceability.md) — the scheme and the sources of truth.
- [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) §7 — the live matrix
  (requirement set → ADR → task → API → code → test).
- [`docs/requirements-compliance.md`](docs/requirements-compliance.md) — per-FR status with code evidence.
- [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) — the gap register.

---

## Functional Requirements

78 functional requirements across 12 business areas, each mapped to a module in code. The full per-requirement
audit (status + file/line evidence) is [`docs/requirements-compliance.md`](docs/requirements-compliance.md);
the high-level roll-up is:

| Business Requirement | Module | FRs |
| --- | --- | --- |
| BR-001 Authentication & User Management | Auth | 8 |
| BR-002 Resident Management | Resident | 7 |
| BR-003 Property Management | Property | 7 |
| BR-004 Maintenance Management | Maintenance | 8 |
| BR-005 Payment Management | Payment | 7 |
| BR-006 Lease & Document Management | Lease | 7 |
| BR-007 Communication & Notifications | Communication | 6 |
| BR-008 User Roles & Permissions | Auth (RBAC) | 6 |
| BR-009 Facility Booking Management | Booking | 7 |
| BR-010 Physical Security & Visitor Management | Security | 7 |
| BR-011 Financial Reporting & Accountant Access | Payment | 4 |
| BR-012 Mobile Platform Access | — | 5 *(not implemented)* |

> The detailed FR catalogue and business-rule pages are maintained in the Confluence `PMP` space; this
> repository keeps the compliance audit and the summary rather than duplicating them.

---

## Non-Functional Requirements

Only categories that are genuinely addressed (or genuinely recorded as missing) are listed — no invented
numeric targets.

| Category | Status | How it is addressed |
| --- | --- | --- |
| **Security** | Implemented (baseline) | Identity + JWT with rotating refresh tokens, email-confirmation gate, password complexity, account lockout, policy-based RBAC, HTTPS redirection, restricted CORS |
| **Auditability** | Implemented (partial) | Authentication and role-change events persisted (`AuthEvent`); maintenance/lease/security history tables; append-only payment transactions |
| **Maintainability** | Implemented | Module ownership + shared kernel, service `Result` pattern, one `DbContext` per module, ADRs, living plan |
| **Reliability** | Implemented (basic) | Idempotent startup migrations + seed; background sweeps for maintenance auto-close, lease expiry and payment due dates |
| **Usability** | Implemented | Role-aware navigation, explicit loading/empty/error/forbidden states, token-based design system |
| **Scalability** | Addressed by design | Modular boundaries allow a module to be extracted later; not load-tested |
| **Performance** | **Not defined** | No performance requirements or benchmarks exist — recorded as `GAP-013`, not claimed |
| **Availability / DR** | **Not defined** | No availability target, health endpoint or documented `pmp.db` backup/restore procedure |
| **Observability** | **Not defined** | No structured logging/metrics/tracing strategy yet |

Platform-wide NFRs are tracked as `IMP-044` (security & NFR review) with the traceability gap `GAP-013`.

---

## System Architecture

PMP is a **modular monolith**: a single ASP.NET Core deployable (`PMP.Api`) that hosts nine domain modules and
one shared kernel, plus a separate React single-page application. Modules own their data and communicate
through published contracts wired at the composition root, never by reaching into another module's internals.

```
                        ┌────────────────────────────┐
                        │  React 19 SPA (frontend/)  │
                        │  role-aware routes + shell  │
                        └──────────────┬─────────────┘
                                       │  HTTPS / JSON (JWT bearer)
                                       ▼
        ┌───────────────────────────────────────────────────────────┐
        │                     PMP.Api  (host)                       │
        │  Controllers ──▶ module services ──▶ module DbContexts     │
        │  AuthN/AuthZ · CORS · Swagger · seeding · hosted services  │
        └───────┬───────────────────────────────────────┬───────────┘
                │                                       │
   ┌────────────▼─────────────┐            ┌────────────▼───────────┐
   │  9 domain modules        │            │  PMP.Shared            │
   │  Auth, Property,         │            │  shared kernel:        │
   │  Resident, Maintenance,  │◀──contracts│  roles, policies,      │
   │  Communication, Lease,   │            │  Result<T>, BaseEntity │
   │  Payment, Booking,       │            └────────────────────────┘
   │  Security                │
   └────────────┬─────────────┘
                │  EF Core (one DbContext per module)
                ▼
        ┌──────────────────────────┐
        │   pmp.db  (SQLite file)  │
        │   table-per-module       │
        └──────────────────────────┘
```

- **Layered within each module:** controller → service (business rules, `Result`) → `DbContext` → entities.
- **Cross-module composition at the root** ([`src/PMP.Api/Program.cs`](src/PMP.Api/Program.cs)):
  - Property consumes unit occupancy from Resident through `IUnitOccupancyProvider` → `UnitOccupancyProvider`
    (occupancy is *derived*, never stored — [ADR-0010](docs/adr/0010-derived-occupancy.md));
  - Maintenance raises status-change notifications through `INotificationService` →
    `PersistedNotificationService` (owned by Communication), so notifications are real and retrievable.
- **Background hosted services:**
  - `AutoCloseHostedService` — auto-closes maintenance requests left in *Completed* for more than 7 days;
  - `LeaseLifecycleHostedService` — daily lease expiry and expiry-notice sweep;
  - `PaymentDueDateHostedService` — daily payment due-date reminders.

> **UML component diagram:** an editable diagrams.net file of the full architecture is at
> [`docs/diagrams/PropertyManagement_Component_Diagram.drawio`](docs/diagrams/PropertyManagement_Component_Diagram.drawio) —
> open it in draw.io / diagrams.net.

---

## Technology Stack

| Layer | Technology |
| --- | --- |
| Backend framework | ASP.NET Core 9 (C#) |
| ORM / data access | Entity Framework Core 9 |
| Database | SQLite (single file `pmp.db`, table-per-module ownership) |
| Authentication | ASP.NET Core Identity + JWT bearer (access + rotating refresh tokens) |
| Authorization | Policy-based RBAC ([`PMP.Shared/Common/AppPolicies.cs`](src/PMP.Shared/Common/AppPolicies.cs)) |
| API documentation | Swagger / OpenAPI (development environment) |
| Frontend | React 19, TypeScript, React Router, Vite |
| Frontend linting | Oxlint |
| Backend testing | xUnit — unit tests + API integration tests over a real host |
| Solution tooling | .NET SDK 9, npm |
| Containerisation / CI | **Not yet implemented** (`IMP-042`, `IMP-043`) |

No technologies are claimed that are not actually used — there is no Docker, no message broker, no cache and
no external payment/email provider in the current implementation.

---

## Project Structure

```
PropertyManagement/
├── src/
│   ├── PMP.Api/                      # Web API host: controllers, composition root, seeding, hosting
│   ├── PMP.Shared/                   # Shared kernel: AppRoles, AppPolicies, Result<T>, BaseEntity, DomainException
│   ├── PMP.Modules.Auth/             # Identity, JWT issuance/refresh, RBAC, audit events
│   ├── PMP.Modules.Property/         # Property / Building / Unit, operational status, derived occupancy
│   ├── PMP.Modules.Resident/         # Profiles, effective-dated unit links, occupancy provider
│   ├── PMP.Modules.Maintenance/      # Request state machine, priorities, assignment, attachments, auto-close
│   ├── PMP.Modules.Communication/    # Persisted notifications + announcements
│   ├── PMP.Modules.Lease/            # Lease agreements, version history, documents, expiry lifecycle
│   ├── PMP.Modules.Payment/          # Invoices, payment transactions, financial reports
│   ├── PMP.Modules.Booking/          # Facilities, availability, reservations
│   └── PMP.Modules.Security/         # Visitors, access grants, access log
├── frontend/                         # React 19 + TypeScript + Vite SPA
├── tests/PMP.Tests/                  # xUnit: unit + API integration tests
├── docs/                             # ADRs, requirements audit, traceability, glossary, plan, gap analysis, diagrams
├── plans/                            # Historical development plan (superseded in parts)
├── agent.md                          # How implementation work is selected, verified and recorded
├── CONTRIBUTING.md                   # Setup and contribution workflow
└── SECURITY.md                       # Security posture, secret handling, disclosure
```

---

## API

A single REST surface under `/api`, protected by JWT bearer tokens and role policies. Swagger UI is available
at `/swagger` when the API runs in the Development environment. Controllers, by module:

| Module | Endpoint area | Highlights |
| --- | --- | --- |
| Auth | `/api/auth` | register, confirm-email, login, refresh, logout, change/forgot/reset password, staff provisioning, admin user list/roles/deactivate/activate, `me` |
| Properties | `/api/properties` | properties, buildings and units CRUD; occupancy |
| Residents | `/api/residents` | list/search, detail, `me`, assign-unit, move-out, deactivate |
| Maintenance | `/api/maintenance` | submit, list/detail, assign, status, priority, confirm, attachments |
| Communication | `/api/communication` | notifications (list/read/unread-count/read-all), announcements |
| Leases | `/api/leases` | list/detail, create, update, terminate, document upload/list, lifecycle trigger |
| Payments | `/api/payments` | balance, invoices, pay, history, dashboard, outstanding, financial report, reconciliation |
| Payment invoices | `/api/invoices` | resident `my` list, financial listing with filters, single invoice (ownership/scope enforced) |
| Bookings | `/api/bookings` | facilities, availability, book, my/all bookings, cancel, configure facility |
| Security | `/api/security` | visitors (register/check-in/check-out/list), access grants (grant/revoke/log) |

Endpoint behaviour is verified in the API integration suite (`tests/PMP.Tests/Integration/`); the definitive
request/response contract is the OpenAPI document served by Swagger. There is no hand-maintained OpenAPI file
to drift out of date.

---

## Database

- **Engine:** SQLite, a single file `pmp.db` created on first run next to `appsettings.json`.
- **Module boundary:** each module has its own `DbContext` and owns a distinct set of tables in the shared file
  ([ADR-0011](docs/adr/0011-sqlite-single-file-module-boundaries.md)); schema-per-module was used historically
  and is preserved as a superseded decision ([ADR-0002](docs/adr/0002-postgresql-schema-per-module.md)).
- **Migrations:** EF Core migrations are applied idempotently at startup — one migration set per module
  (Auth, Property, Resident, Maintenance, Communication, Lease, Payment + `PaymentRequestsAndObligations`,
  Booking, Security).
- **Domain areas / principal entities:**
  - *Property*: `ManagedProperty` → `Building` → `ResidentialUnit` (operational status; occupancy derived).
  - *Resident*: `ResidentProfile`, `ResidentUnit` (effective-dated, history retained).
  - *Maintenance*: `MaintenanceRequest`, `MaintenanceHistoryEntry`, `MaintenanceAttachment`.
  - *Lease*: `LeaseAgreement`, `LeaseDocument`, `LeaseHistoryEntry`.
  - *Payment*: `Invoice` (request/obligation), `PaymentTransaction` (append-only), `PaymentInvoice` (receipt), `InvoiceNumberSequence`.
  - *Communication*: `Notification`, `Announcement`.
  - *Booking*: `Facility`, `FacilityBooking`.
  - *Security*: `VisitorRecord`, `AccessGrant`.
  - *Auth*: `ApplicationUser`, `RefreshToken`, `AuthEvent`.
- **Naming note:** the Property aggregate entity is `ManagedProperty`; older prose sometimes says `Property`.
  The code is authoritative ([`src/PMP.Modules.Property/Entities/ManagedProperty.cs`](src/PMP.Modules.Property/Entities/ManagedProperty.cs)).

---

## Security

Implemented security controls:

| Control | Detail |
| --- | --- |
| Authentication | ASP.NET Core Identity; JWT access tokens (15-minute lifetime) with rotating refresh tokens |
| Session termination | Logout revokes the refresh token family; deactivation revokes refresh tokens |
| Email verification | Required before sign-in (`RequireConfirmedEmail = true`) |
| Password policy | ≥8 characters, upper/lower/digit/non-alphanumeric, 4 unique characters |
| Brute-force protection | Account lockout after 5 failed attempts for 15 minutes |
| Authorization | Deny-by-default policies per role + service-level ownership scoping (management scoped to assigned properties) |
| Audit | Authentication and role-change events persisted; maintenance/lease/security history and access logs |
| Transport / CORS | HTTPS redirection; CORS restricted to the frontend development origins |
| Configuration | Secrets held in git-ignored `appsettings.json`; `Jwt:Key` must be ≥32 characters or startup fails |

**Known limitations (recorded, not hidden):** payments are simulated (no provider integration); the email
channel is record-only; no invoice PDF export (on-screen invoice details only); role revocation does not invalidate an in-flight access token before expiry; no
independent security review has been run yet. Full detail and IDs are in
[`SECURITY.md`](SECURITY.md) and [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md).

---

## Testing & Quality Assurance

Testing is deliberately **behavioural** and reported honestly as two different measures:

- **Documented coverage** — requirements with a matching implementation ([`docs/requirements-compliance.md`](docs/requirements-compliance.md)).
- **Verified coverage** — behaviour proven by a test or a recorded manual check ([`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)).

| Layer | Approach |
| --- | --- |
| Unit tests | Services tested at the `Result` seam (the agreed testing seam from [ADR-0005](docs/adr/0005-service-result-pattern.md)) — Auth token, Property, Resident, Maintenance, Payment, Lease, Communication, Booking and Security rules |
| API integration tests | The real API is booted (Kestrel + throwaway SQLite, configured only through environment variables — no test-only hooks in the API) and driven over HTTP. They assert the MVP journey end-to-end plus authentication, RBAC and ownership negatives |
| Background jobs / attachments | **Not yet covered** (`GAP-001`) |
| Frontend | **No test runner configured yet** (`IMP-041`) — the build and lint are the current gates |
| CI | **Not yet configured** (`IMP-042` / `GAP-009`) |

**Verified on 2026-09-11:** `dotnet test PropertyManagement.sln` → build clean and **101 tests passed / 0 failed**
(unit + API integration; all nine modules have service-level tests). Run it yourself with the command in
[How to Run](#how-to-run); the suite is re-runnable and self-contained.

New and changed business behaviour is implemented **test-first** (RED → GREEN → REFACTOR → VERIFY) through the
service seam, with the expected behaviour expressed as a failing test before the production code exists.
Behaviour added under `IMP-040` for modules that already existed is covered by characterization tests that pin
it down so it can be changed safely. The full strategy, the TDD workflow and the exact commands are in
[`docs/TESTING.md`](docs/TESTING.md).

Testing traceability follows *requirement → acceptance criteria → test*, with defects and missing coverage
recorded as `GAP-XXX`. The relationship *requirement → test scenario → test case → defect* is maintained in the
plan/gap register; dedicated manual test-case artefacts are not yet authored.

---

## Architecture Decision Records

Architecture decisions are recorded as ADRs in [`docs/adr/`](docs/adr/) — each with status, context, decision,
alternatives and consequences. Superseded decisions are preserved, not rewritten.

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-0001](docs/adr/0001-modular-monolith.md) | Modular monolith | accepted |
| [ADR-0002](docs/adr/0002-postgresql-schema-per-module.md) | PostgreSQL with schema-per-module | **superseded by ADR-0011** |
| [ADR-0003](docs/adr/0003-identity-jwt-auth.md) | ASP.NET Core Identity with JWT access + refresh tokens | accepted |
| [ADR-0004](docs/adr/0004-rbac-roles-and-policies.md) | RBAC with platform roles and policy-based authorization | accepted |
| [ADR-0005](docs/adr/0005-service-result-pattern.md) | Services return `Result` instead of throwing | accepted |
| [ADR-0006](docs/adr/0006-module-boundaries-and-ownership.md) | Module boundaries and data ownership | accepted |
| [ADR-0007](docs/adr/0007-mvp-scope.md) | MVP scope: four core modules first | accepted — **partially superseded by ADR-0012** |
| [ADR-0008](docs/adr/0008-resident-account-provisioning.md) | Resident self-registration; staff admin-provisioned | accepted |
| [ADR-0009](docs/adr/0009-maintenance-state-machine.md) | Maintenance request state machine and confirmation gate | accepted |
| [ADR-0010](docs/adr/0010-derived-occupancy.md) | Unit occupancy is derived, not stored | accepted |
| [ADR-0011](docs/adr/0011-sqlite-single-file-module-boundaries.md) | SQLite single-file with table-level module boundaries | accepted |
| [ADR-0012](docs/adr/0012-post-mvp-modules.md) | Post-MVP module increment (Payment, Lease, Communication, Booking, Security, Accounting) | accepted |

---

## Development Approach

- **Iterative / incremental SDLC** — a narrow end-to-end MVP first (Auth, Property, Resident, Maintenance,
  RBAC), validated through API integration tests, then a controlled post-MVP increment module by module.
- **Requirements baseline** — business requirements, FRs and business rules are authored and baselined in
  Confluence; ambiguities were resolved in **structured grilling sessions** before implementation
  (see the historical [`plans/pmp-development-plan.md`](plans/pmp-development-plan.md)).
- **Backlog & delivery tracking** — work is expressed as `IMP-XXX` tasks with dependencies, weights and
  acceptance criteria, selected by priority order (foundation → MVP → high-priority FRs → integration → testing
  → security → DevOps → debt → post-MVP). The live dashboard is mirrored to a Confluence page.
- **Definition of done** — implemented **and** acceptance criteria proven, tests passing, integration complete,
  ADRs/requirements respected; otherwise the task stays unchecked with the reason recorded.
- **Decision-first change control** — architectural change requires an ADR; documentation and code are kept in
  agreement, and conflicts are recorded rather than silently resolved.
- **Agile practices in use** — prioritised incremental delivery, continuous verification, living documentation
  and explicit technical-debt/gap tracking. Formal sprint ceremonies are not claimed.

---

## Project Status

Honest status, derived from verified state — not from file or endpoint counts.

### Implemented

- 9 modules + shared kernel wired into a single host; authentication, RBAC and the MVP slice verified
  end-to-end by API integration tests (`IMP-006`).
- Post-MVP modules implemented in code: Payment (+ Accountant reporting, verified), Lease, Communication,
  Booking, Security.
- Background lifecycle jobs (maintenance auto-close, lease expiry/notice, payment due dates).
- Frontend SPA with a role-aware shell, navigation manifest and route guards; design-system primitives.

### In Progress / Remaining

- **Remaining test expansion** — background-job and attachment coverage (`GAP-001`); per-module service tests
  delivered under `IMP-040`.
- **Frontend tests** (`IMP-041`) and **CI pipeline** (`IMP-042`).
- **Deployment packaging** (`IMP-043`) — no Docker/Compose or production configuration yet.
- **Security & NFR review/hardening** (`IMP-044`).
- Open requirement-level gaps: `FR-AUTH-005` (owner profile edit), `FR-RBAC-006` (role-revocation latency),
  `FR-COM-006` (email record-only), lease invariant, and two role-validation hardening items.

### Planned / Future

- **BR-012 Mobile platform access** — not implemented (no native app or PWA); its prerequisite (`IMP-040`) is
  now green, but it remains explicitly deferred.
- Real payment-provider integration, email transport, and access-control hardware integration are out of scope
  until instructed.

> **Verified progress:** overall **81%**, MVP **100%** (defined by the auditable weighting in
> [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)). The gap register holds **26** items
> (`GAP-001…GAP-026`).

---

## Roadmap

Ordered by the project's own selection policy; nothing here is claimed as delivered.

| Order | Item | Tracked as |
| --- | --- | --- |
| 1 | Background-job + attachment test coverage (per-module service tests delivered under `IMP-040`) | `GAP-001` |
| 2 | Frontend test infrastructure and core page/route tests | `IMP-041` |
| 3 | CI pipeline (build + `dotnet test` + frontend lint/build on every push) | `IMP-042` |
| 4 | Deployment packaging (container/compose, production config, secrets strategy) | `IMP-043` |
| 5 | Security & NFR review and hardening | `IMP-044` |
| 6 | Owner self-service profile edit endpoint | `IMP-030` (GAP-004) |
| 7 | Immediate role revocation | `IMP-031` (GAP-003) |
| 8 | Lease invariant, role-validation hardening, BRULE pages for BR-009/010 | `IMP-033`/`034`/`035` |
| 9 | Mobile platform access (BR-012) — only after testing is green | `IMP-027` |

---

## How to Run

### Prerequisites

| Requirement | Version |
| --- | --- |
| .NET SDK | 9.0 |
| Node.js | 20+ (with npm) |
| Database server | **None** — SQLite is file-based |

### 1. Clone and configure the backend

```bash
git clone <repository-url>
cd PropertyManagement
```

Create the backend configuration from the committed example and set a JWT secret of **at least 32 characters**:

```powershell
copy src\PMP.Api\appsettings.example.json src\PMP.Api\appsettings.json
```

```bash
# macOS / Linux
cp src/PMP.Api/appsettings.example.json src/PMP.Api/appsettings.json
```

`appsettings.json` is git-ignored on purpose — never commit real secrets. It holds
`ConnectionStrings:DefaultConnection` and the `Jwt` section (`Issuer`, `Audience`, `Key`,
`AccessTokenExpiryMinutes`, `RefreshTokenExpiryDays`).

### 2. Run the API

```bash
dotnet run --project src/PMP.Api --launch-profile http
```

On startup the API creates `pmp.db` if needed, applies every module's EF Core migrations and seeds demo data
(roles, users, a sample property/building/units with a resident assignment, plus a lease, an invoice, a
facility and a welcome notification). Swagger UI: http://localhost:5070/swagger.

### 3. Seed logins (development fixtures only)

| Role | Email | Password |
| --- | --- | --- |
| Administrator | `admin@pmp.com` | `Admin123!` |
| Property Manager | `manager@pmp.com` | `Manager123!` |
| Technician | `tech@pmp.com` | `Tech123!` |
| Resident | `resident@pmp.com` | `Resident123!` |
| Accountant | `accountant@pmp.com` | `Accountant123!` |

> These are demo accounts with well-known passwords and must not be used in a deployed environment.

### 4. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server runs at http://localhost:5173 and proxies `/api` to the API on port 5070.

### 5. Run the tests

```bash
# Backend — unit + API integration (boots a real host on a throwaway SQLite database)
dotnet test PropertyManagement.sln

# Frontend — build (type-check) and lint (no test runner yet)
cd frontend
npm run build
npm run lint
```

---

## Documentation

A full index is in [`docs/README.md`](docs/README.md). Quick links:

| Category | Where |
| --- | --- |
| Requirements traceability scheme | [`docs/traceability.md`](docs/traceability.md) |
| Functional-requirements audit (FR / BRULE) | [`docs/requirements-compliance.md`](docs/requirements-compliance.md) |
| Implementation plan (master checklist + matrix) | [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) |
| Implementation gap analysis (GAP-XXX) | [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) |
| Architecture Decision Records | [`docs/adr/`](docs/adr/) |
| UML component diagram (editable) | [`docs/diagrams/PropertyManagement_Component_Diagram.drawio`](docs/diagrams/PropertyManagement_Component_Diagram.drawio) |
| Domain & analysis glossary | [`docs/glossary.md`](docs/glossary.md) |
| Historical development plan | [`plans/pmp-development-plan.md`](plans/pmp-development-plan.md) |
| Contributing / workflow | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Security policy | [`SECURITY.md`](SECURITY.md) |
| Agent operating rules | [`agent.md`](agent.md) |

**Jira = work tracking · Confluence = detailed project/system documentation · GitHub = source code + selected
engineering documentation.** The implementation plan in this repository is mirrored 1:1 to the
**`PMP Implementation Plan`** page in the Confluence `PMP` space, so status has a single live dashboard.

---

## System Analyst Deliverables

Artefacts that actually exist in this repository (no placeholder claims):

| Deliverable | Where |
| --- | --- |
| Requirements catalogue (summary + audit) | [`docs/requirements-compliance.md`](docs/requirements-compliance.md) |
| Business rules catalogue (BRULE per module) | Within the compliance audit, per business area |
| Functional / non-functional requirement overview | This README; detailed FRs in Confluence |
| Acceptance criteria per task | [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) |
| Traceability matrix (REQ → ADR → task → code/API → test) | [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) §7 |
| Traceability scheme & identifier conventions | [`docs/traceability.md`](docs/traceability.md) |
| Gap register (defects / missing behaviour / decisions) | [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) |
| Architecture Decision Records | [`docs/adr/`](docs/adr/) |
| System / component architecture diagram (UML) | [`docs/diagrams/PropertyManagement_Component_Diagram.drawio`](docs/diagrams/PropertyManagement_Component_Diagram.drawio) |
| Data model (entities + migration history) | EF Core entities and migrations under [`src/`](src) |
| API surface documentation | This README §API and Swagger (`/swagger` in development) |
| Domain glossary | [`docs/glossary.md`](docs/glossary.md) |
| Test artefacts (unit + API integration) | [`tests/PMP.Tests/`](tests/PMP.Tests) |
| Project governance (plan, verification loop, status) | [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md), [`agent.md`](agent.md) |

**Not present yet (recommended improvements, not claimed):** BPMN process models, use-case diagrams and
sequence diagrams (authored in Confluence, not committed here); a numeric NFR catalogue with requirement IDs;
CSV/PDF financial-report export; open API specification committed to the repository.

---

## Contributing, Security & Licence

- Contributing, local setup and the definition of done: [`CONTRIBUTING.md`](CONTRIBUTING.md).
- Security posture, secret handling and responsible disclosure: [`SECURITY.md`](SECURITY.md).
- **Licence:** not specified. A `LICENSE` file has been intentionally **not** invented; the repository owner
  should choose one before public distribution.
