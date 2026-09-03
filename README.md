# Property Management Platform (PMP)

A **modular-monolith** implementation of the Property Management Platform requirements captured in the Confluence **PMP** space. The platform is driven by the Confluence functional requirements (BR-001..BR-012) and business rules (BRULE-*); a compliance audit is maintained in [`docs/requirements-compliance.md`](docs/requirements-compliance.md).

Current coverage: **BR-001..BR-011 implemented end-to-end** (Auth, Resident, Property, Maintenance, Payment, Lease, Communication, Booking, Security, RBAC, Accountant reporting). BR-012 (mobile) is documented as remaining work.

## Architecture

The system is a modular monolith: one ASP.NET Core deployable (`PMP.Api`) hosting nine domain modules, a React SPA frontend, and a single SQLite file.

- **Frontend:** React 19 + TypeScript + Vite (dev :5173), proxies `/api` → :5070
- **Backend:** ASP.NET Core 9 (C#), EF Core 9
- **Database:** SQLite — one shared file `pmp.db`; each module has its own `DbContext` owning distinct tables in it (modular-monolith boundary preserved at the app/service layer)
- **Auth:** ASP.NET Core Identity + JWT (access + refresh tokens), RBAC roles (Administrator, PropertyManager, Resident, Technician, Accountant)

> 📐 **UML Component Diagram:** an editable diagram.net file of the full architecture lives at [`docs/diagrams/PropertyManagement_Component_Diagram.drawio`](docs/diagrams/PropertyManagement_Component_Diagram.drawio) — open it in draw.io / diagrams.net. A Confluence page describing the components and real module dependencies accompanies it ("System Architecture & Major Dependencies").

## Modules

| Module (project) | Responsibilities | DbContext (tables in `pmp.db`) |
| --- | --- | --- |
| PMP.Modules.Auth | Identity, JWT issuance/refresh, RBAC, audit events | AuthDbContext |
| PMP.Modules.Property | Property / Building / Unit CRUD, operational status, occupancy view | PropertyDbContext |
| PMP.Modules.Resident | Profiles, effective-dated unit assignment, occupancy provider | ResidentDbContext |
| PMP.Modules.Maintenance | Request state machine, priorities, assignment, attachments, auto-close | MaintenanceDbContext |
| PMP.Modules.Communication | Persisted notifications + announcements (in-app/email channels) | CommunicationDbContext |
| PMP.Modules.Lease | Lease agreements, version history, secure documents, expiry/notice sweep | LeaseDbContext |
| PMP.Modules.Payment | Invoices, unique payment transactions, confirmations, financial reports/reconciliation | PaymentDbContext |
| PMP.Modules.Booking | Facilities, availability, overlap-free reservations, cancellation windows | BookingDbContext |
| PMP.Modules.Security | Visitor register/check-in/out, building/unit/facility access grants (with expiry) | SecurityDbContext |
| PMP.Shared | Shared kernel — `AppRoles`, `AppPolicies`, `Result<T>`, `BaseEntity`, `DomainException` | — (no tables) |

Cross-module composition is wired in the host composition root ([`src/PMP.Api/Program.cs`](src/PMP.Api/Program.cs)):

- Property consumes unit occupancy from Resident via `IUnitOccupancyProvider` (implemented by `UnitOccupancyProvider`).
- Maintenance status-change notifications are persisted through Communication's `INotificationService` implementation (`PersistedNotificationService`).

Background services:

- `AutoCloseHostedService` — auto-closes maintenance requests left in Completed for > 7 days.
- `LeaseLifecycleHostedService` — daily lease expiry + expiry-notice sweep.
- `PaymentDueDateHostedService` — daily payment due-date reminders.

## Solution Structure

```
PropertyManagement.sln
├── src/
│   ├── PMP.Api/                  # Web API host (controllers, composition root, seeding)
│   ├── PMP.Shared/               # Shared kernel (base entities, Result, roles/policies)
│   ├── PMP.Modules.Auth/         # Identity, JWT, RBAC, audit events
│   ├── PMP.Modules.Property/     # Property/Building/Unit management
│   ├── PMP.Modules.Resident/     # Profiles + effective-dated unit links
│   ├── PMP.Modules.Maintenance/  # Requests, state machine, auto-close job
│   ├── PMP.Modules.Communication/# Notifications + announcements (persisted)
│   ├── PMP.Modules.Lease/        # Lease agreements, documents, expiry lifecycle
│   ├── PMP.Modules.Payment/      # Invoices, payments, financial reports
│   ├── PMP.Modules.Booking/      # Facility booking + availability
│   └── PMP.Modules.Security/     # Visitors + access control
├── frontend/                     # React 19 + TS + Vite SPA
├── tests/PMP.Tests/              # xUnit tests
├── docs/                         # ADRs, glossary, compliance audit, diagrams
└── plans/                        # Development plan / grilling notes
```

## Running Locally

### 1. Database

No database server is required. SQLite is file-based: on first run the API creates `pmp.db` (next to `appsettings.json`) and applies the EF Core migrations, creating all module tables in the single file.

### 2. Run the API

```bash
dotnet run --project src/PMP.Api --launch-profile http
```

On startup the API applies EF Core migrations (one per module DbContext, all targeting the shared `pmp.db`) and seeds demo data (roles, users, a sample property with units and a resident assignment, plus a lease, invoice, facility, and a welcome notification). Swagger is available at `/swagger`.

Seed logins:

| Role | Email | Password |
| --- | --- | --- |
| Administrator | admin@pmp.com | Admin123! |
| Property Manager | manager@pmp.com | Manager123! |
| Technician | tech@pmp.com | Tech123! |
| Resident | resident@pmp.com | Resident123! |
| Accountant | accountant@pmp.com | Accountant123! |

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server runs on http://localhost:5173 and proxies `/api` to the API.

## API Surface

Controllers (all under `/api`, JWT-protected with role policies):

- **Auth** — register, confirm-email, login, refresh, logout, change-password, forgot/reset-password, staff provisioning, admin user list/roles/deactivate/activate, `me`
- **Properties** — properties, buildings, units CRUD + occupancy
- **Residents** — list/search, details, `me`, assign-unit, move-out, deactivate
- **Maintenance** — submit (resident), list/detail, assign, status, priority, confirm, attachments
- **Communication** — notifications (list/read/unread-count/read-all), announcements (publish for managers, feed for residents)
- **Leases** — list/detail, create, update, terminate, document upload/list, lifecycle trigger
- **Payments** — balance (resident), invoices, pay, history, financial report, reconciliation (Accountant/Manager/Administrator)
- **Bookings** — facilities, availability, book, my/all bookings, cancel, configure facility
- **Security** — visitors (register/check-in/check-out/list), access grants (grant/revoke/log)

## Tests

```bash
dotnet test PropertyManagement.sln
```

## Documentation

- [`docs/adr/`](docs/adr/) — Architecture Decision Records (incl. SQLite single-file, modular monolith, JWT auth, RBAC, maintenance state machine, post-MVP modules)
- [`docs/glossary.md`](docs/glossary.md) — domain glossary
- [`docs/requirements-compliance.md`](docs/requirements-compliance.md) — FR × business-rule traceability/audit
- [`docs/diagrams/PropertyManagement_Component_Diagram.drawio`](docs/diagrams/PropertyManagement_Component_Diagram.drawio) — UML component diagram (editable)
- [`plans/pmp-development-plan.md`](plans/pmp-development-plan.md) — development plan / grilling notes

## Status / Roadmap

- ✅ MVP: Auth, Property, Resident, Maintenance + RBAC admin UI
- ✅ Post-MVP modules: Payment (+ Accountant reporting), Lease & Document, Communication & Notifications, Facility Booking, Security & Visitor
- 🔜 Remaining: Mobile platform access (BR-012); minor hardening (e.g., role-revocation latency, owner profile-edit endpoint) — see [`docs/requirements-compliance.md`](docs/requirements-compliance.md)
