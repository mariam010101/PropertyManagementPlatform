# PMP — Functional Requirements Compliance Audit

**Date:** 2026-09-03 — **corrected 2026-09-11** (FR-COM-006 downgraded; see §8)
**Source of truth:** Confluence space `PMP` — "Functional Requirements" (BR-001..BR-012) and "Business Rules" (BRULE-*) pages.
**Verdict scale:** 🟢 MET · 🟡 PARTIAL · 🔴 NOT MET (not implemented).

> **Headline result:** After the post-MVP increment all **12 business areas** have working modules. BR-001..BR-011 are implemented in code with **3 requirements marked partial** — FR-AUTH-005 (no dedicated owner profile-edit endpoint), FR-RBAC-006 (JWT role-claim latency) and FR-COM-006 (email is record-only) — plus a small set of business-rule nuances. BR-012 (mobile platform) is **NOT MET**: the React web app is responsive but is not delivered as a dedicated mobile platform. Full per-FR traceability and code evidence are below.

---

## 1. Roll-up by business area

| Business Requirement | Module in code | FR count | 🟢 Met | 🟡 Partial | 🔴 Not met |
| --- | --- | --- | --- | --- | --- |
| BR-001 Authentication & User Mgmt | Auth | 8 | 7 | 1 | 0 |
| BR-002 Resident Management | Resident | 7 | 7 | 0 | 0 |
| BR-003 Property Management | Property | 7 | 7 | 0 | 0 |
| BR-004 Maintenance Management | Maintenance | 8 | 8 | 0 | 0 |
| BR-005 Payment Management | Payment | 7 | 7 | 0 | 0 |
| BR-006 Lease & Document Mgmt | Lease | 7 | 7 | 0 | 0 |
| BR-007 Communication & Notifications | Communication | 6 | 5 | 1 | 0 |
| BR-008 User Roles & Permissions | Auth (RBAC) | 6 | 5 | 1 | 0 |
| BR-009 Facility Booking Mgmt | Booking | 7 | 7 | 0 | 0 |
| BR-010 Security & Visitor Mgmt | Security | 7 | 7 | 0 | 0 |
| BR-011 Financial Reporting & Accountant | Payment (Financial) | 4 | 4 | 0 | 0 |
| BR-012 Mobile Platform Access | — (responsive web only) | 5 | 0 | 0 | 5 |
| **Total** | | **78** | **71** | **3** | **5** |

> Count note: BR-001 lists 8 FRs on its Confluence page (FR-AUTH-001..008). The FRs that were "partial" before the increment are now fully met (self-service profile update via resident edit path, occupancy history retention, property filtering, real persisted notifications); the three remaining partials are **FR-AUTH-005 / BRULE-AUTH-006 (owner self-edit of identity fields not exposed as a dedicated endpoint)**, **FR-RBAC-006 (role revocation applies immediately to the DB but existing 15-minute JWTs keep old claims until expiry)** and **FR-COM-006 (email is recorded with a delivery status but no transport exists — see GAP-005)**.

---

## 2. BR-001 Authentication & User Management

Related rules: BRULE-AUTH-001..007.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-AUTH-001 | Register with valid email + password | 🟢 MET | [`AuthService.RegisterResidentAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:70); `POST /api/auth/register` |
| FR-AUTH-002 | Authenticate during login | 🟢 MET | [`AuthService.LoginAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:114) |
| FR-AUTH-003 | Securely reset forgotten password | 🟢 MET | [`ForgotPasswordAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:226) / [`ResetPasswordAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:239) |
| FR-AUTH-004 | Change own password | 🟢 MET | [`ChangePasswordAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:208) |
| FR-AUTH-005 | Update profile information | 🟡 PARTIAL | Residents update their resident profile via `PUT /api/residents/{id}` (manager/admin) and self via `GET /api/residents/me`; **no dedicated owner endpoint mutates the Identity user's name fields.** |
| FR-AUTH-006 | Terminate sessions on logout | 🟢 MET | [`LogoutAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:195) revokes refresh token |
| FR-AUTH-007 | Prevent unauthorized access | 🟢 MET | JWT bearer + `[Authorize]`/policies |
| FR-AUTH-008 | Record auth events for auditing | 🟢 MET | [`RecordEventAsync()`](src/PMP.Modules.Auth/Services/AuthService.cs:534) → `AuthEvent` |

**BRULE-AUTH:** 001 unique email 🟢 · 002 authenticate before protected features 🟢 · 003 password policy 🟢 · 004 approved recovery only 🟢 · 005 account state active/inactive (no distinct "suspended") 🟡 · 006 profile editable by owner/admin 🟡 (see FR-AUTH-005) · 007 auth recorded 🟢.

---

## 3. BR-002 Resident Management

Related rules: BRULE-RES-001..006.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-RES-001 | Create resident profiles | 🟢 MET | Auto-created on registration via [`CreateProfileForUserAsync()`](src/PMP.Modules.Resident/Services/ResidentService.cs:41) |
| FR-RES-002 | Edit resident information | 🟢 MET | [`UpdateResidentAsync()`](src/PMP.Modules.Resident/Services/ResidentService.cs:133) |
| FR-RES-003 | Display resident profile details | 🟢 MET | `GetResidentAsync` (:103), `GetMyProfile` (:118) |
| FR-RES-004 | Associate residents with units | 🟢 MET | [`AssignUnitAsync()`](src/PMP.Modules.Resident/Services/ResidentService.cs:152) — effective-dated |
| FR-RES-005 | Maintain occupancy history | 🟢 MET | `ResidentUnit` rows retained on move-out/deactivate (never deleted) |
| FR-RES-006 | Deactivate without deleting history | 🟢 MET | [`DeactivateResidentAsync()`](src/PMP.Modules.Resident/Services/ResidentService.cs:233) |
| FR-RES-007 | Search & filter residents | 🟢 MET | `GetResidentsAsync(search, activeOnly)` (:64) |

**BRULE-RES:** 001 resident↔unit 🟡 (enforced at assign-time only) · 002 one-or-more residents per unit 🟢 · 003 accurate/current info 🟢 · 004 authorized personnel only 🟢 · 005 former residents retained 🟢 · 006 contact uniqueness 🟢.

---

## 4. BR-003 Property Management

Related rules: BRULE-PROP-001..006.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-PROP-001 | Create property records | 🟢 MET | [`CreatePropertyAsync()`](src/PMP.Modules.Property/Services/PropertyService.cs:86) |
| FR-PROP-002 | Manage buildings | 🟢 MET | `CreateBuildingAsync` (:161), `UpdateBuildingAsync` (:189) |
| FR-PROP-003 | Manage residential units | 🟢 MET | `CreateUnitAsync` (:254), `UpdateUnitAsync` (:304) |
| FR-PROP-004 | Display property occupancy | 🟢 MET | Per-unit derived `IsOccupied` via `IUnitOccupancyProvider` in `ToDtosAsync` (:350) |
| FR-PROP-005 | Update property details | 🟢 MET | `UpdatePropertyAsync` (:111) |
| FR-PROP-006 | Maintain unit operational status | 🟢 MET | `OperationalStatus` enum |
| FR-PROP-007 | Search/filter properties/buildings/units | 🟢 MET | Lists are manager-scoped; filtering parity with residents available via query params |

**BRULE-PROP:** all 🟢 (001 building→property FK, 002 unit→building FK, 003 assigned manager, 004 lifecycle updates, 005 inactive units not assigned — enforced in [`AssignUnitAsync`](src/PMP.Modules.Resident/Services/ResidentService.cs:169), 006 unique ids). Hardening note: property create doesn't validate the manager user's role.

---

## 5. BR-004 Maintenance Management

Related rules: BRULE-MNT-001..006.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-MNT-001 | Residents submit requests | 🟢 MET | [`SubmitAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:63) |
| FR-MNT-002 | Attach images | 🟢 MET | `POST .../attachments` + [`AddAttachmentAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:316) |
| FR-MNT-003 | Assign to technicians/teams | 🟢 MET | [`AssignAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:172) |
| FR-MNT-004 | Technicians update status | 🟢 MET | [`UpdateStatusAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:212) + transition map |
| FR-MNT-005 | Notify residents on status change | 🟢 MET | Now **persisted** through [`PersistedNotificationService`](src/PMP.Modules.Communication/Services/PersistedNotificationService.cs:11) → Communication module (replaces the old logging stub) |
| FR-MNT-006 | Maintain complete request history | 🟢 MET | `MaintenanceHistoryEntry` on every transition |
| FR-MNT-007 | Managers prioritize | 🟢 MET | [`SetPriorityAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:253) |
| FR-MNT-008 | Residents confirm completion | 🟢 MET | [`ConfirmAsync()`](src/PMP.Modules.Maintenance/Services/MaintenanceService.cs:273) |

**BRULE-MNT:** all 🟢 (001 existing units, 002 one current status, 003 one responsible tech — hardening: no Technician-role validation on assignee, 004 closed not modified, 005 residents informed via persisted notification, 006 full history + 7-day auto-close [`AutoCloseHostedService`](src/PMP.Modules.Maintenance/Background/AutoCloseHostedService.cs:45)).

---

## 6. BR-005 Payment Management + BR-011 Accountant

Related rules: BRULE-PAY-001..006.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-PAY-001 | Display outstanding balances | 🟢 MET | [`PaymentService.GetBalanceAsync()`](src/PMP.Modules.Payment/Services/PaymentService.cs:186) — invoice `Amount - PaidAmount` |
| FR-PAY-002 | Pay rent electronically | 🟢 MET | [`PayInvoiceAsync()`](src/PMP.Modules.Payment/Services/PaymentService.cs:253) — simulated card/bank payment |
| FR-PAY-003 | Generate payment confirmations | 🟢 MET | Confirmation number returned on each completed payment (BRULE-PAY-005); a `PaymentInvoice` receipt (`INV-YYYY-NNNNNN`, 1:1 with the payment) is generated and linked to each successful payment |
| FR-PAY-004 | Maintain payment history | 🟢 MET | `payment_transactions` immutable rows; `GetPaymentHistoryAsync` |
| FR-PAY-005 | Managers monitor payment status | 🟢 MET | `GetInvoicesAsync` / `GetPaymentHistoryAsync` (manager/admin scope) |
| FR-PAY-006 | Generate financial reports | 🟢 MET | [`GetFinancialReportAsync()`](src/PMP.Modules.Payment/Services/PaymentService.cs:316) |
| FR-PAY-007 | Notify upcoming due dates | 🟢 MET | [`PaymentDueDateHostedService`](src/PMP.Modules.Payment/Background/PaymentDueDateHostedService.cs:11) daily sweep |

Accountant (BR-011): FR-ACCT-001 view payment/invoice records within scope 🟢 · FR-ACCT-002 generate/export financial reports 🟢 · FR-ACCT-003 reconciliation status 🟢 · FR-ACCT-004 restricted from non-financial data 🟢 (Accountant role is excluded from Property/Resident/Maintenance policies; only the `Financial`/`AccountantOrAdmin` payment endpoints accept it).

**BRULE-PAY:** 001 payments tied to valid lease 🟢 (invoice→lease validation) · 002 unique transaction id 🟢 (unique `TransactionReference`) · 003 never deleted after processing 🟢 (append-only) · 004 status reflects state 🟢 · 005 resident confirmation 🟢 · 006 outstanding balances visible 🟢.

---

## 7. BR-006 Lease & Document Management

Related rules: BRULE-LEASE-001..006.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-LEASE-001 | Create lease records | 🟢 MET | [`LeaseService.CreateLeaseAsync()`](src/PMP.Modules.Lease/Services/LeaseService.cs:88) |
| FR-LEASE-002 | Upload lease documents | 🟢 MET | `POST .../documents` + [`UploadDocumentAsync()`](src/PMP.Modules.Lease/Services/LeaseService.cs:289) |
| FR-LEASE-003 | Securely store lease documents | 🟢 MET | Files stored under `uploads/`, metadata + path in DB |
| FR-LEASE-004 | Maintain lease version history | 🟢 MET | `LeaseHistoryEntry` per modification (version bump) |
| FR-LEASE-005 | Display lease details | 🟢 MET | `GetLeaseAsync` returns documents + history |
| FR-LEASE-006 | Notify before lease expiration | 🟢 MET | [`LeaseLifecycleHostedService`](src/PMP.Modules.Lease/Background/LeaseLifecycleHostedService.cs:11) notifies within 30 days |
| FR-LEASE-007 | Archive expired leases | 🟢 MET | Same sweep marks past-end leases `Expired` (retained) |

**BRULE-LEASE:** 001 every active resident has a lease 🟡 (process-level; enforced at create/assign, not retroactively) · 002 one unit per lease 🟢 · 003 start/end dates 🟢 · 004 documents accessible to authorized 🟢 · 005 modifications versioned 🟢 · 006 expired retained 🟢.

---

## 8. BR-007 Communication & Notifications

Related rules: BRULE-COM-001..006.

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-COM-001 | System-generated notifications for business events | 🟢 MET | Maintenance/Payment/Lease/Booking events write persisted notifications via `ICommunicationService.SendUserNotificationAsync` |
| FR-COM-002 | Managers publish announcements | 🟢 MET | `POST /api/communication/announcements` (ManagerOrAdmin) |
| FR-COM-003 | Display announcements to relevant residents | 🟢 MET | [`GetResidentAnnouncementsAsync()`](src/PMP.Modules.Communication/Services/CommunicationService.cs:236) filters by the resident's occupied property |
| FR-COM-004 | Maintain notification history | 🟢 MET | `notifications` table retained |
| FR-COM-005 | Users view previously received notifications | 🟢 MET | `GET /api/communication/notifications` + mark-read |
| FR-COM-006 | Email and in-app notifications | 🟡 PARTIAL | `NotificationChannel` (`InApp`/`Email`); in-app is delivered and retrievable, but the email channel is **record-only** — no SMTP/MailKit/SendGrid transport exists, so no message is ever sent (GAP-005). Correction applied 2026-09-11; the 2026-09-03 revision marked this 🟢 MET. |

**BRULE-COM:** 001 intended recipients only 🟢 · 002 triggered by predefined business events 🟢 · 003 property-relevant announcements 🟢 · 004 history retained 🟢 · 005 only authorized publish 🟢 · 006 delivery status recorded 🟢.

---

## 9. BR-008 User Roles & Permissions

Related rules: BRULE-RBAC-001..006. (See also the RBAC module + [`AdminUsersPage.tsx`](frontend/src/pages/AdminUsersPage.tsx).)

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-RBAC-001 | Assign ≥1 role per user | 🟢 MET | Auto-role on register/create + multi-role assignment |
| FR-RBAC-002 | Restrict access by role | 🟢 MET | Policies in [`AppPolicies`](src/PMP.Shared/Common/AppPolicies.cs) |
| FR-RBAC-003 | Admins manage roles | 🟢 MET | `GET/PUT /api/auth/users...` + admin UI |
| FR-RBAC-004 | Validate permissions before access | 🟢 MET | Policy + service-level checks |
| FR-RBAC-005 | Record role/permission changes in audit log | 🟢 MET | `AuthEvent` ("RolesUpdated", "AccountDeactivated"…) |
| FR-RBAC-006 | Apply permission changes immediately | 🟡 PARTIAL | DB is immediate; **existing 15-minute JWTs keep old role claims until expiry** (mitigated by refresh-token revocation on deactivation). |

**BRULE-RBAC:** 001 ≥1 role 🟢 · 002 permissions by role 🟢 · 003 least-privilege scoping 🟢 · 004 admin-only administrative permissions 🟢 · 005 role changes audited 🟢 · 006 revoked permissions immediate 🟡 (JWT claim latency).

---

## 10. BR-009 Facility Booking Management

Rules: FR-BOOK-001..007 (no BRULE page published yet).

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-BOOK-001 | View facilities + availability | 🟢 MET | `GetFacilitiesAsync` / `GetAvailabilityAsync` in [`BookingService`](src/PMP.Modules.Booking/Services/BookingService.cs) |
| FR-BOOK-002 | Reserve a facility for a date/time slot | 🟢 MET | `BookAsync` |
| FR-BOOK-003 | Prevent overlapping bookings | 🟢 MET | Overlap guard on `BookAsync` (in-memory evaluation for SQLite) |
| FR-BOOK-004 | Cancel own booking within window | 🟢 MET | `CancelBookingAsync` enforces facility cancellation window |
| FR-BOOK-005 | Managers configure availability/rules | 🟢 MET | `CreateFacilityAsync`/`UpdateFacilityAsync` (hours, slot, cancellation window) |
| FR-BOOK-006 | Notify booking confirmations/cancellations | 🟢 MET | In-app notifications via Communication |
| FR-BOOK-007 | Maintain booking history | 🟢 MET | Booking rows retained (Reserved/Cancelled/Completed) |

---

## 11. BR-010 Physical Security & Visitor Management

Rules: FR-SEC-001..007 (no BRULE page published yet).

| FR | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| FR-SEC-001 | Register visitor info before/at arrival | 🟢 MET | [`RegisterVisitorAsync()`](src/PMP.Modules.Security/Services/SecurityService.cs:71) |
| FR-SEC-002 | Verify & check in registered visitors | 🟢 MET | `CheckInVisitorAsync` |
| FR-SEC-003 | Record check-in/out times | 🟢 MET | `CheckInAt`/`CheckOutAt` on visitor |
| FR-SEC-004 | Grant/revoke building/unit/facility access | 🟢 MET | `GrantAccessAsync` / `RevokeAccessAsync` (user or visitor subject) |
| FR-SEC-005 | Log all grants/revocations & entries/exits | 🟢 MET | `access_grants` + `visitors` audit log |
| FR-SEC-006 | View visitor and access logs | 🟢 MET | `GetVisitorsAsync` / `GetAccessLogAsync` |
| FR-SEC-007 | Temporary access with expiration | 🟢 MET | `ExpiresAt` on access grants |

*Note:* Property Manager/Administrator act as the authorized security operator role (no separate "Security" role exists yet).

---

## 12. BR-012 Mobile Platform Access — NOT MET

| FR | Requirement | Status | Note |
| --- | --- | --- | --- |
| FR-MOBILE-001..005 | Mobile access to maintenance/payments/announcements/booking + same auth | 🔴 NOT MET | The React web app is responsive, but there is **no native app or PWA** and no dedicated mobile build. Auth is the same JWT mechanism, so the API is mobile-ready; a mobile client is the remaining work. |

---

## 13. Summary of remaining gaps to reach 100%

1. **FR-MOBILE-001..005** — build a mobile client (native or PWA) on the existing API.
2. **FR-RBAC-006 / BRULE-RBAC-006** — reduce role-revocation latency (short access-token TTL + per-request active-role recheck or security stamp).
3. **FR-AUTH-005 / BRULE-AUTH-006** — expose a dedicated "edit my profile" endpoint that also updates Identity name fields.
4. **BRULE-AUTH-005** — add a distinct "suspended" account state if required.
5. **BRULE-LEASE-001** — enforce "active resident must have a lease" as a hard invariant.
6. **FR-COM-006** — implement a provider-backed email sender (config-driven, disabled by default) and record real delivery outcomes, or explicitly descope email delivery with an ADR and keep this audit at PARTIAL.
7. **Hardening (non-FR-blocking):** validate Technician role on maintenance assignment; validate manager role on property create; publish BRULE pages for BR-009/010 if desired.
