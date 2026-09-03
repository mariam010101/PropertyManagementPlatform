# 0012 — Post-MVP module increment (Payment, Lease, Communication, Booking, Security, Accounting)

## Status
Accepted (2026-09-03)

## Context
The functional-requirements compliance audit ([`docs/requirements-compliance.md`](../requirements-compliance.md))
showed the MVP (Auth, Property, Resident, Maintenance + RBAC) satisfied 30/78 FRs fully; 43 FRs across seven
business areas were deferred under [`0007-mvp-scope.md`](0007-mvp-scope.md). The user directed that the full
post-MVP suite be built now: Payment (BR-005), Lease & Document (BR-006), Communication & Notifications
(BR-007), Facility Booking (BR-009), Physical Security & Visitor (BR-010), and Financial Reporting/Accountant
(BR-011). BR-012 (mobile) remains out of scope for this increment.

## Decision
Add five new module projects alongside the existing four, following the same modular-monolith conventions
(single shared SQLite `pmp.db`, table-per-module, separate DbContext + migrations, DI extension, controller):

- `PMP.Modules.Communication` (BR-007) — persisted notifications + announcements; implements the Maintenance
  `INotificationService` contract so `FR-MNT-005` status-change notifications are real and retrievable.
- `PMP.Modules.Lease` (BR-006) — lease agreements, version history, documents, expiry/archive lifecycle.
- `PMP.Modules.Payment` (BR-005 + BR-011) — invoices, unique payment transactions, confirmations, financial
  reports and reconciliation exposed to Accountant/Manager/Administrator.
- `PMP.Modules.Booking` (BR-009) — facilities, availability, overlap-preventing reservations, cancellation windows.
- `PMP.Modules.Security` (BR-010) — visitor registration/check-in/out, access grants/revocations with expiry.

Cross-module reads (e.g., Lease→Property/Resident, Payment→Lease, Booking→Property/Resident, Security→Auth/Property/Booking)
follow the established pattern of referencing the owning project's DbContext for read-only data; each module keeps
write ownership of its own tables. Notifications are delivered through `Communication` for a single channel.

## Consequences
- 72/78 FRs are now MET; 2 are PARTIAL (FR-AUTH-005 owner profile edit; FR-RBAC-006 JWT role-claim latency);
  5 (BR-012 mobile) are NOT MET.
- Business rules BRULE-PAY/LEASE/COM/RBAC now map to real implementations; Payment/Lease/Communication rule pages
  are honored (unique transaction ids, immutable processed records, no deletion, versioned lease changes, delivery
  status recorded).
- Added two hosted background services (lease expiry/notice sweep; payment due-date reminders) and a persisted
  notification adapter, plus new frontend pages/routes (Payments, Leases, Communication, Bookings, Security).
- SQLite `DateTimeOffset` range comparisons must be evaluated client-side; this was applied to booking overlap,
  availability, and payment due-date sweeps.
- New migrations: `InitialCommunication`, `InitialLease`, `InitialPayment`, `InitialBooking`, `InitialSecurity`.
- Trade-off: cross-module reads widen coupling slightly; kept bounded to read-only lookups and role-scoped services.
