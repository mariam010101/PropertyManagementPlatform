# PMP Glossary

The terms used across the Property Management Platform. Definitions are opinionated: where multiple words exist for the same concept, one is canonical and the others are listed under _Avoid_.

## Domain terms

**Property**:
A managed residential development (one or more buildings) with exactly one assigned manager.
_Avoid_: estate, complex, site

**Building**:
A structure within a property that contains residential units.
_Avoid_: block, tower (reserved as a possible subtype)

**Residential Unit**:
A rentable unit within a building; has an operational status and a derived occupancy.
_Avoid_: apartment, flat, flat unit

**Property Manager**:
A staff role that manages one or more properties (all data scoped to those properties).
_Avoid_: landlord, administrator

**Resident**:
A person who occupies a residential unit; represented by a user account and a resident profile.
_Avoid_: tenant, occupant, customer

**Technician**:
A staff role that handles assigned maintenance requests and updates their status.
_Avoid_: repairman, worker, handyman

**Administrator**:
A system role with full access that provisions staff accounts and manages roles.
_Avoid_: superuser, root

**Resident-unit association**:
An effective-dated link between a resident and a unit, with move-in/move-out dates; a unit may have several active residents.
_Avoid_: tenancy, lease (a lease is a separate, post-MVP concept)

**Operational status**:
The unit's own lifecycle state: Active / Inactive / Under Maintenance.
_Avoid_: unit status, occupancy

**Occupancy**:
The derived occupied/vacant state of a unit, computed from active resident-unit associations — never stored.
_Avoid_: availability, rented

**Maintenance request**:
A work item with a lifecycle (Submitted → Assigned → In Progress → Completed → Confirmed / Closed, plus Cancelled) and a priority (Low / Medium / High / Urgent).
_Avoid_: ticket, work order

## Platform and security terms

**Access token**:
A short-lived JWT that authorizes API calls.
_Avoid_: session token, API key

**Refresh token**:
A longer-lived, rotated token used to mint new access tokens.
_Avoid_: keep-alive

**Email verification**:
Confirmation that a resident owns the email address used at registration; required before sign-in.
_Avoid_: email confirmation link

**Role-based access control (RBAC)**:
The authorization model: users get roles, roles grant access via policies, and services additionally scope data by ownership.
_Avoid_: ACL, permissions matrix

**Least privilege**:
Principle that each user receives only the permissions needed for their responsibilities.

**Deactivation / soft delete**:
Marking a record inactive while preserving its history (e.g., deactivating a resident account).
_Avoid_: delete, purge

## Architecture terms

**Modular monolith**:
A single deployable application internally organized into modules, with boundaries enforced at the database level (one schema per module).
_Avoid_: microservice, big ball of mud

**Schema**:
The per-module database namespace: `auth`, `property`, `resident`, `maintenance`.

**Composition root**:
The API startup/DI point where cross-module wiring happens (e.g., registration calls Auth to create the user and Resident to create the profile).
_Avoid_: service locator

**Result pattern**:
Services signal expected outcomes via a `Result`/`Result<T>` value instead of throwing exceptions for ordinary failures.
