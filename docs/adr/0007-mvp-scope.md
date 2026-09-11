# MVP scope: four core modules first

**Status:** accepted — **partially superseded by [ADR-0012](0012-post-mvp-modules.md)**

> **Status note (2026-09-11).** ADR-0007 remains authoritative for the **MVP slice**: the first deliverable is
> Authentication, Property, Resident and Maintenance. Its *roadmap consequence* — that everything else is
> deferred — was superseded by **ADR-0012**, which approved and delivered the post-MVP modules (Payment,
> Lease, Communication, Booking, Security, Financial Reporting). The RBAC admin UI ships with the MVP-adjacent
> RBAC work tracked under FR-RBAC. This record is preserved for architectural history; the current
> module-scope authority is ADR-0012.

The MVP ships **Authentication, Property, Resident, and Maintenance** first and defers Payment, Lease & Document, Communication, Facility Booking, Physical Security & Visitor, Financial Reporting, Mobile Access, and the RBAC admin UI until the MVP is validated.

**Considered options**

- **Build all 12 modules breadth-first** — rejected: maximizes risk and delays a working, testable slice.
- **MVP subset** — chosen: delivers a coherent vertical slice (resident registers → assigned a unit → submits a maintenance request → manager/technician resolve it) end to end.

**Consequences**

- Deferred modules' FRs remain tracked in Confluence (FR-PAY, FR-LEASE, FR-COM, FR-BOOK, FR-SEC, FR-ACCT, FR-MOBILE, FR-RBAC).
- The domain model (effective-dated associations, derived occupancy) was chosen so it does not need rework when Lease/Payment arrive.
