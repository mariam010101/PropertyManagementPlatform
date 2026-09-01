# Maintenance request state machine and resident confirmation gate

**Status:** accepted

A maintenance request follows **Submitted → Assigned → In Progress → Completed → Confirmed / Closed** (plus **Cancelled**), with priorities **Low / Medium / High / Urgent**, and is assigned to **individual technicians** (team concept deferred). A request is closed by **resident confirmation**; if a resident never confirms, the request is **auto-closed after 7 days** in `Completed` (with reopen allowed within that window).

**Considered options**

- **No resident confirmation gate** (tech completion auto-closes) — rejected: resident confirmation drives satisfaction and closes the loop (FR-MNT-008).
- **Maintenance teams** — deferred: individual technicians satisfy the MVP.

**Consequences**

- Transitions are validated in the service (invalid transitions rejected, BRULE-MNT-002/004).
- An hourly `AutoCloseHostedService` closes stale `Completed` requests.
