# Property Management Platform (PMP) — MVP

A modular-monolith implementation of the Property Management Platform requirements
captured in the Confluence **PMP** space. This MVP covers four modules:

1. **Authentication & User Management** (FR-AUTH-001..008)
2. **Property Management** (FR-PROP-001..007) — Property → Building → Unit
3. **Resident Management** (FR-RES-001..007) — profiles + effective-dated unit associations
4. **Maintenance Management** (FR-MNT-001..008) — request state machine, priorities, assignment, auto-close

> Grilled decisions and the sharpened plan live in [`plans/pmp-development-plan.md`](plans/pmp-development-plan.md).

## Tech Stack

- **Backend:** ASP.NET Core 9 (C#), EF Core 9, SQLite
- **Frontend:** React 18 + TypeScript + Vite
- **Database:** SQLite — one shared file `pmp.db`; the four module DbContexts
  (`auth`, `property`, `resident`, `maintenance`) own distinct tables in it
  (modular-monolith boundary preserved at the app/service layer)
- **Auth:** ASP.NET Core Identity + JWT (access + refresh tokens), RBAC roles
  (Administrator, PropertyManager, Resident, Technician)

## Solution Structure

```
PropertyManagement.sln
├── src/
│   ├── PMP.Api/                 # Web API host (controllers, composition root, seeding)
│   ├── PMP.Shared/              # Shared kernel (base entities, Result, roles/policies)
│   ├── PMP.Modules.Auth/        # Identity, JWT, refresh tokens, audit events  (shared SQLite, auth tables)
│   ├── PMP.Modules.Property/    # Property/Building/Unit + scoped CRUD      (shared SQLite, property tables)
│   ├── PMP.Modules.Resident/    # Profiles + effective-dated unit links     (shared SQLite, resident tables)
│   └── PMP.Modules.Maintenance/ # Requests, state machine, auto-close job   (shared SQLite, maintenance tables)
├── frontend/                    # React + TS + Vite SPA
└── tests/PMP.Tests/             # xUnit tests
```

## Running Locally

### 1. Database

No database server is required. SQLite is file-based: on first run the API creates
`pmp.db` (next to `appsettings.json`) and applies the EF Core migrations, creating
all module tables in the single file.

### 2. Run the API

```bash
dotnet run --project src/PMP.Api
```

On startup the API applies the EF Core migrations (one per module DbContext,
all targeting the shared `pmp.db`) and seeds demo data (roles,
admin/manager/technician/resident users, a sample property with units, and a
resident assignment). Swagger is available at `/swagger`.

Seed logins:

| Role | Email | Password |
| --- | --- | --- |
| Administrator | admin@pmp.com | Admin123! |
| Property Manager | manager@pmp.com | Manager123! |
| Technician | tech@pmp.com | Tech123! |
| Resident | resident@pmp.com | Resident123! |

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server runs on http://localhost:5173 and proxies `/api` to the API.

## API Surface (MVP)

- `POST /api/auth/register` — resident self-registration (email verification token returned in dev)
- `POST /api/auth/confirm-email` — confirm email
- `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
- `POST /api/auth/change-password`, `forgot-password`, `reset-password`
- `POST /api/auth/staff` — admin provisions PropertyManager/Technician/Administrator
- `GET/POST/PUT /api/properties`, `/api/properties/{id}/buildings`, `/api/buildings/{id}/units`, `/api/units/{id}`
- `GET/PUT /api/residents`, `/api/residents/me`, assign-unit, move-out, deactivate
- `POST /api/maintenance` (resident), `GET /api/maintenance`, assign, status, priority, confirm, attachments

## Next Steps (Post-MVP Modules)

Payment, Lease & Document, Communication & Notifications, Facility Booking,
Physical Security & Visitor, Financial Reporting, Mobile Access, and RBAC admin UI —
see the plan's "Open Questions for Next Grilling Session".
