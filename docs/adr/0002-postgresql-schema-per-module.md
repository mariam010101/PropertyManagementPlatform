# PostgreSQL with schema-per-module

**Status:** accepted

We use **PostgreSQL via EF Core/Npgsql** as the database, with each module owning its own schema (`auth`, `property`, `resident`, `maintenance`) — each module has its own `DbContext` and its own migrations.

**Considered options**

- **SQL Server** — rejected: licensing and deployment overhead; PostgreSQL gives the same relational capabilities with simpler local/cloud provisioning.
- **Single shared schema** — rejected: it would erase the module boundary that ADR-0001 relies on; a module could reference any table.

**Consequences**

- Npgsql provider; each module DbContext migrates independently.
- Cross-module data access happens only through public service contracts or explicit, deliberate joins at the composition root — never ad-hoc across schemas.
