# SQLite single-file with table-level module boundaries

**Status:** accepted

We migrated the database from PostgreSQL to **SQLite** (`Data Source=pmp.db;Foreign Keys=True`) using `Microsoft.EntityFrameworkCore.Sqlite` 9.0.0. SQLite has no PostgreSQL-style schemas, so the four module DbContexts (`auth`, `property`, `resident`, `maintenance`) share **one SQLite file** and own distinct table sets. Module boundaries are enforced at the application/service layer and by keeping the four separate DbContexts, preserving the modular-monolith structure (ADR-0001). The PostgreSQL `HasDefaultSchema` calls were removed and all four EF Core migration sets were regenerated for SQLite.

**Considered options**

- **Keep PostgreSQL schema-per-module** — rejected: the requirement is SQLite.
- **Four separate SQLite files (one per module)** — rejected: services read across modules (ResidentService uses Property unit data; MaintenanceService reads Resident + Property), which requires a single shared file; SQLite cannot supply multiple schemas/files for those cross-module reads.
- **Table prefixes per module** — considered, deferred: existing table names are already unique across modules, so explicit prefixes would add churn without immediate benefit; they can be layered later if ownership clarity requires it.

**Consequences**

- Single `pmp.db` file; foreign-key enforcement via `Foreign Keys=True` in the connection string (SQLite does not enforce FKs by default).
- `Guid` keys and `DateTimeOffset` values are stored as `TEXT`. SQLite cannot translate `DateTimeOffset` comparisons to SQL, so timestamp range filters run in memory (see `AutoCloseHostedService`); entities keep `DateTimeOffset`.
- No database server or Docker container is required to run the API.
- Supersedes ADR-0002 (PostgreSQL schema-per-module); the PostgreSQL version is preserved in Git commit `08aa11b`.
