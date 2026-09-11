# Contributing to PMP

This repository is governed by [`agent.md`](agent.md) and the living
[`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md). Read both before changing behaviour.

---

## 1. Before you start

- **Work is selected from the implementation plan**, not invented. Pick the highest-priority eligible unchecked
  task; respect its dependencies.
- **Read the decisions.** Architecture changes must follow or extend the ADRs in [`docs/adr/`](docs/adr/). A new
  architectural decision is captured as a new ADR, not edited into an old one.
- **Understand before editing.** Read the task's acceptance criteria, the affected module and its existing tests.
- **Do not invent** requirements, endpoints, business rules, tests or metrics.

## 2. Prerequisites

| Tool | Version |
| --- | --- |
| .NET SDK | 9.0 |
| Node.js | 20+ (with npm) |

## 3. Local setup

```bash
# Backend configuration (git-ignored; never commit real secrets)
copy src\PMP.Api\appsettings.example.json src\PMP.Api\appsettings.json   # Windows
# cp src/PMP.Api/appsettings.example.json src/PMP.Api/appsettings.json    # macOS / Linux
# then set Jwt:Key to a random string of at least 32 characters

# Backend (creates pmp.db and seeds demo data on first run)
dotnet run --project src/PMP.Api --launch-profile http

# Frontend
cd frontend
npm install
npm run dev
```

Demo seed accounts are documented in the root [`README.md`](README.md). They are **development fixtures only**.

## 4. Making a change

1. Create a branch describing the work (e.g. `imp-040-resident-service-tests`).
2. Implement **only** what the task requires, plus the changes it genuinely needs.
3. Keep the module boundary: a module owns its own tables and `DbContext`; cross-module access goes through a
   published contract wired at the composition root ([`src/PMP.Api/Program.cs`](src/PMP.Api/Program.cs)).
4. Services signal expected outcomes with the `Result` pattern rather than throwing for ordinary failures
   ([ADR-0005](docs/adr/0005-service-result-pattern.md)).
5. Do not commit secrets, local databases or build output — see [`SECURITY.md`](SECURITY.md) and
   [`.gitignore`](.gitignore).

## 5. Verification (required)

Run and record evidence — a claim without evidence is not completion:

```bash
dotnet build PropertyManagement.sln
dotnet test  PropertyManagement.sln

cd frontend
npm run lint
npm run build
```

Verify **actual behaviour** (request/response, database effect, authorization result), not just that a file,
method or component exists. For UI work, additionally check loading, empty, error and forbidden states and
responsiveness.

## 6. Definition of done

A task is done only when every acceptance criterion is provably satisfied, the build and relevant tests pass,
integration works, and the ADRs and requirements are respected.

When done — or when you cannot finish — update, in the same change:

- [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) — checkbox, status, evidence, remaining work;
- [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) — any gap created, changed or resolved;
- [`docs/requirements-compliance.md`](docs/requirements-compliance.md) — if a requirement's status changed.

If a task is incomplete, its checkbox **stays unchecked** and the reason is stated.

## 7. Commit messages

Use a concise, scoped format, e.g.:

```
fix(maintenance): include request title in status notifications
test(resident): cover occupancy-history retention on move-out
docs(readme): rewrite as system-analyst case study
```

## 8. Conventions

- Follow the naming and terminology in [`docs/glossary.md`](docs/glossary.md).
- Prefer the service layer (`Result`) as the testing seam; keep controllers thin.
- Keep documentation links valid; do not duplicate Confluence/Jira content in the repository.
