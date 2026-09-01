# Module boundaries and data ownership

**Status:** accepted

Each module owns its schema and its aggregates:

- **Auth** owns identity (users, roles, refresh tokens, auth audit events).
- **Property** owns properties/buildings/units; it depends on an `IUnitOccupancyProvider` interface for derived occupancy, implemented by the **Resident** module (occupancy is a resident fact, not a property fact).
- **Resident** owns resident profiles and effective-dated resident-unit associations.
- **Maintenance** owns requests, attachments, and history; it reads resident/property data to enforce scoping rules.

**Boundary rules**

- The **Auth** module must never reference Resident/Property/Maintenance. At registration the **API composition root** calls Auth to create the user, then Resident to create the profile.
- Modules reference other modules' data only through deliberate, documented calls — never by opening another module's schema ad hoc.

**Consequences**

- The dependency graph is acyclic and visible: Auth ← Resident ← Maintenance, and Property → (occupancy interface) ← Resident.
- Pragmatic MVP shortcut: modules may use another module's `DbContext`/service directly; the interface indirection is added where the ownership direction would otherwise be wrong (occupancy).
