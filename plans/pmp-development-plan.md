# Property Management Platform — Sharpened Development Plan (MVP)

> **Historical planning record.** This document captures the original MVP planning/grilling session and is kept
> for provenance. Several details were superseded as the project progressed:
> - the SPA lives at [`frontend/`](../frontend) (this record planned it at `src/frontend/`);
> - the database is a single SQLite file with table-per-module ownership
>   ([ADR-0011](../docs/adr/0011-sqlite-single-file-module-boundaries.md)), not PostgreSQL schema-per-module;
> - the delivered scope is nine modules ([ADR-0012](../docs/adr/0012-post-mvp-modules.md)), not the four-module MVP.
>
> For the current, verified status use [`docs/IMPLEMENTATION_PLAN.md`](../docs/IMPLEMENTATION_PLAN.md).

Source: Functional requirements pulled from Confluence (PMP space). Grilled via the
"grill-me" interview process to resolve ambiguities and lock buildable decisions.

## 1. Scope

**True MVP first: 4 modules**
1. Authentication & User Management (FR-AUTH-001..008)
2. Property Management (FR-PROP-001..007)
3. Resident Management (FR-RES-001..007)
4. Maintenance Management (FR-MNT-001..008)

Post-MVP modules (grill later, after MVP validation): Payment (FR-PAY), Lease &
Document (FR-LEASE), Communication & Notifications (FR-COM), Facility Booking
(FR-BOOK), Physical Security & Visitor (FR-SEC), Financial Reporting (FR-ACCT),
Mobile Platform Access (FR-MOBILE), and full RBAC administration (FR-RBAC).

## 2. Tech Stack (locked)

- **Backend:** ASP.NET Core (C#), EF Core ORM
- **Frontend:** React (web SPA)
- **Architecture:** Modular Monolith with module boundaries enforced at the database
  level (schema-per-module), single deployable
- **Database:** SQLite (single file `pmp.db`; the 4 module DbContexts own distinct
  tables — see ADR-0011; supersedes the PostgreSQL/schema-per-module choice in ADR-0002)
- **Auth:** ASP.NET Core Identity + JWT bearer tokens with refresh tokens
- **RBAC:** Identity roles + policy-based authorization
  - Roles: Administrator, Property Manager, Resident, Technician
  - (Accountant, Security Staff added in post-MVP modules)

## 3. Cross-Cutting Foundations (locked)

- **Account provisioning:** Residents self-register; staff (Property Manager,
  Technician, Administrator) are created/invited by an Administrator.
- **Resident↔unit linking:** Admin/property manager assigns the unit after
  registration and verifies identity. No self-claiming of units.
- **Security posture:** Email verification required at registration; strong password
  policy (8+ chars with complexity); account lockout after 5 failed attempts.
- **Data scoping:** One property = one assigned manager; a manager may manage
  multiple properties; all queries scoped to the manager's assigned properties
  (least privilege). Administrator sees all.
- **Soft deletes:** Deactivation never hard-deletes records (FR-RES-006).

## 4. Module Decisions (locked)

### 4.1 Authentication & User Management (FR-AUTH-001..008)
- Self-registration for Residents only; staff are admin-provisioned.
- Email verification on registration; strong password policy; lockout after 5
  failed attempts.
- Password reset via approved email recovery flow (FR-AUTH-003).
- Session/refresh-token lifecycle; logout terminates sessions (FR-AUTH-006).
- Authentication events recorded for audit (FR-AUTH-008).
- Unit association assigned later by admin (see 4.3).

### 4.2 Property Management (FR-PROP-001..007)
- Hierarchy: **Property → Building → Residential Unit** (each building belongs to
  one property; each unit belongs to one building).
- **Unit model — two separate concepts:**
  - Operational status: Active / Inactive / Under Maintenance (FR-PROP-006)
  - Derived occupancy: Occupied / Vacant, computed from active resident/lease
    associations (FR-PROP-004)
- Each property has exactly one assigned manager; managers can hold multiple
  properties (BRULE-PROP-003).
- Search/filter across properties, buildings, units (FR-PROP-007).

### 4.3 Resident Management (FR-RES-001..007)
- **One-to-one User ↔ Resident profile.** Resident self-registration auto-creates
  the Resident profile; admin later assigns the unit and fills details
  (FR-RES-001..003, FR-RES-004).
- **Effective-dated ResidentUnit association:** one row per resident-unit period
  with move-in/move-out dates. Multiple active residents per unit allowed
  (BRULE-RES-002). History preserved on move-out/deactivation (FR-RES-005,
  FR-RES-006).
- Deactivation = soft delete; occupancy history retained.
- Search/filter resident records (FR-RES-007).

### 4.4 Maintenance Management (FR-MNT-001..008)
- **State machine:** Submitted → Assigned → In Progress → Completed → Confirmed /
  Closed, plus Cancelled. Exactly one current status (BRULE-MNT-002); closed
  requests not modified (BRULE-MNT-004).
- **Priorities:** Low / Medium / High / Urgent (FR-MNT-007).
- **Assignment:** Individual technicians only for MVP — one Technician (staff
  user) per request (FR-MNT-003). Team concept deferred.
- **Resident confirmation gate (FR-MNT-008):** resident must confirm completion
  to close. If unconfirmed, **auto-close after 7 days** in Completed; resident can
  reopen within that window.
- Images can be attached to requests (FR-MNT-002).
- Full request history maintained (FR-MNT-006).
- Status-change notifications (FR-MNT-005): in-app + email, via a lightweight
  notification service in MVP (full Communication module is post-MVP).

## 5. Suggested Solution Structure (Modular Monolith)

```
PropertyManagement/            # historical MVP layout — see README for the current tree
├── src/
│   ├── PMP.Api/                 # Web API host, Program.cs, DI composition root
│   ├── PMP.Shared/              # Shared kernel: BaseEntity, result types, exceptions
│   ├── PMP.Modules.Auth/        # Auth & User Management
│   ├── PMP.Modules.Property/    # Property Management
│   ├── PMP.Modules.Resident/    # Resident Management
│   └── PMP.Modules.Maintenance/ # Maintenance Management
├── frontend/                    # React SPA (Vite) — planned here as src/frontend/
├── tests/                       # xUnit: unit + integration tests
└── docs/                        # ADRs, requirements, diagrams
```

- Each module: Controllers → Application Services → Repositories → EF DbContext
  owning its own tables (per Confluence architecture pages).
- Shared identity/auth lives in the Auth module; other modules reference user
  identity via claims + module-level FK-to-auth only through public contracts.
- EF Core migrations per module (table ownership, not schema).

## 6. Post-MVP Modules (to grill in a later session)

| Module | FR set | Notes |
|---|---|---|
| Payment Management | FR-PAY-001..007 | Rent payment, confirmations, reports, due-date alerts |
| Lease & Document Management | FR-LEASE-001..007 | Lease records, doc storage, version history, expiry alerts |
| Communication & Notifications | FR-COM-001..006 | Announcements, notification history, email + in-app |
| User Roles & Permissions (admin UI) | FR-RBAC-001..006 | Role/permission management UI, audit, immediate apply |
| Facility Booking Management | FR-BOOK-001..007 | Availability, conflict-free reservations, cancellation window |
| Physical Security & Visitor Mgmt | FR-SEC-001..007 | Visitor check-in/out, access grants, temp access w/ expiry, logs |
| Financial Reporting & Accountant Access | FR-ACCT-001..004 | Scoped financial views, report export, reconciliation |
| Mobile Platform Access | FR-MOBILE-001..005 | Same auth; mobile UI for maintenance, payment, announcements, bookings |

## 7. Open Questions for Next Grilling Session

- Payment provider and rent/fee configuration model (FR-PAY-001..007).
- Lease vs. ResidentUnit relationship when Lease module lands (versioning).
- Communication channels and announcement audience model (FR-COM-001..006).
- Facility definitions, time-slot granularity, cancellation window (FR-BOOK).
- Visitor access-control integration (hardware/API) vs. log-only MVP (FR-SEC).
- Mobile: responsive web vs. native app (FR-MOBILE).
