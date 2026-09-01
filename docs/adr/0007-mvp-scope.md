# MVP scope: four core modules first

**Status:** accepted

The MVP ships **Authentication, Property, Resident, and Maintenance** first and defers Payment, Lease & Document, Communication, Facility Booking, Physical Security & Visitor, Financial Reporting, Mobile Access, and the RBAC admin UI until the MVP is validated.

**Considered options**

- **Build all 12 modules breadth-first** — rejected: maximizes risk and delays a working, testable slice.
- **MVP subset** — chosen: delivers a coherent vertical slice (resident registers → assigned a unit → submits a maintenance request → manager/technician resolve it) end to end.

**Consequences**

- Deferred modules' FRs remain tracked in Confluence (FR-PAY, FR-LEASE, FR-COM, FR-BOOK, FR-SEC, FR-ACCT, FR-MOBILE, FR-RBAC).
- The domain model (effective-dated associations, derived occupancy) was chosen so it does not need rework when Lease/Payment arrive.
