# Modular Monolith

**Status:** accepted

The platform spans 12 business modules, but there is no team or traffic scale to justify distributed services, so we build a **modular monolith**: a single deployable application internally organized into independent modules whose boundaries are enforced at the database level (one schema per module).

**Considered options**

- **Microservices** — rejected: operational complexity (deployment, networking, observability) with no compensating need; the modules share one database anyway.
- **Plain monolith without boundaries** — rejected: module boundaries would decay into spaghetti and cross-module coupling would become unmanageable.

**Consequences**

- Single deployment and single set of servers to run and monitor.
- Module boundaries are physical (DB schemas) rather than just organizational, so coupling is visible and constrained.
- A later extraction to services is possible per module, but is not planned.
