# PostgreSQL with schema-per-module

**Status:** superseded by [ADR-0011](0011-sqlite-single-file-module-boundaries.md)

> **2026-09-01:** The project migrated from PostgreSQL to SQLite. See
> [ADR-0011](0011-sqlite-single-file-module-boundaries.md). This record is kept
> for historical context (the PostgreSQL version is preserved in Git commit
> `08aa11b`).

We used **PostgreSQL via EF Core/Npgsql** as the database, with each module owning its own schema (`auth`, `property`, `resident`, `maintenance`) — each module had its own `DbContext` and its own migrations.

**Considered options**

- **SQL Server** — rejected: licensing and deployment overhead; PostgreSQL gives the same relational capabilities with simpler local/cloud provisioning.
- **Single shared schema** — rejected: it would erase the module boundary that ADR-0001 relies on; a module could reference any table.

**Consequences (at the time)**

- Npgsql provider; each module DbContext migrates independently.
- Cross-module data access happened only through public service contracts or explicit, deliberate joins at the composition root — never ad-hoc across schemas.
