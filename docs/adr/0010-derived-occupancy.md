# Unit occupancy is derived, not stored

**Status:** accepted

A residential unit's occupancy (occupied/vacant) is **derived at query time from active resident-unit associations**, not stored as a column. Stored state is limited to the unit's **operational status** (Active / Inactive / Under Maintenance).

**Considered options**

- **Stored occupancy flag updated on move-in/out** — rejected: the flag drifts out of sync with associations and duplicates the source of truth.

**Consequences**

- Occupancy is always consistent with the resident data.
- The Property module exposes an `IUnitOccupancyProvider` interface; the Resident module implements it (occupancy is a resident fact), keeping the dependency direction correct.
