# PMP Testing Strategy & TDD Workflow

This document defines how PMP is tested and how test-driven development (TDD) is applied to new and
changed behaviour. It is the companion to [`docs/IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) (what may
be worked on and its verified status) and [`docs/traceability.md`](traceability.md) (the requirement chain).

The goal is not "as many tests as possible". The goal is a disciplined practice in which a requirement is
made testable, the expected behaviour is written down as a failing test, the minimum production code makes it
pass, and the plan records what has actually been verified.

---

## 1. Principles

1. **Behaviour over structure.** Tests verify what the system does through its public seams, not how it is
   implemented, so they survive refactoring.
2. **Requirements first.** Tests derive from the requirements catalogue (`FR-<AREA>-NNN`), business rules
   (`BRULE-<AREA>-NNN`) and the acceptance criteria of the `IMP-XXX` task. Requirements are never invented to
   justify a test (see [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](IMPLEMENTATION_GAP_ANALYSIS.md)).
3. **The plan is the contract.** A task is only `COMPLETE` when its acceptance criteria are proven with
   evidence. A passing test run is evidence; the existence of a file, method or endpoint is not.
4. **Honesty.** Coverage is reported as *verified* (proven by a test) or *documented* (claimed by a document),
   never conflated. No coverage percentage is asserted unless it is actually generated and read.
5. **Small vertical slices.** One behaviour, one test, the minimum implementation. No speculative code.

---

## 2. Test levels (the pyramid)

```
        API / integration tests        (few, slower, real host over HTTP)
        ---------------------------
        Service / unit tests           (many, fast, no I/O)
```

PMP deliberately keeps the base of the pyramid large:

| Level | Location | What it covers | Isolation |
| --- | --- | --- | --- |
| **Unit / service** | `tests/PMP.Tests/*.cs` | Domain rules, service logic, validation, state transitions, authorization decisions, calculations, mappings | EF Core **InMemory** database per test; collaborators replaced with test doubles |
| **API / integration** | `tests/PMP.Tests/Integration/` | Multi-component behaviour: routing, model binding, JWT authentication, RBAC policies, ownership scoping, persistence, migrations, seeding | The **real API** is booted (Kestrel + throwaway SQLite under the temp directory) and driven over HTTP |
| **End-to-end (browser)** | — | Not introduced. The repository does not currently need a browser driver; the API harness already exercises the full server slice. | — |

The agreed unit-testing seam is the service `Result` boundary defined by
[ADR-0005](../docs/adr/0005-service-result-pattern.md): services return `Result`/`Result<T>`, so behaviour can
be asserted without HTTP.

---

## 3. The TDD loop

For a new behaviour:

```
Requirement → Acceptance criteria → Test → Implementation → Refactor → Verification
```

**RED** — write one test describing the expected observable behaviour; run it and confirm it fails *for the
expected reason* (not a compile error or a broken arrangement).

**GREEN** — implement the minimum production code that makes that test pass; run it again.

**REFACTOR** — improve the implementation with all tests still green.

**VERIFY** — run the relevant suite (and the full suite when the change can affect it), then update the
implementation plan with evidence.

Repeat per behaviour. The detailed, agent-facing checklist lives in the project skill
[`.roo/skills/tdd/SKILL.md`](../.roo/skills/tdd/SKILL.md).

> **This is a discipline, not a retrofit.** The tests added under `IMP-040` for modules that already existed
> are **characterization/regression tests at the agreed seams** — they pin down current, implemented behaviour
> so it can be changed safely. They were not written red-first, and the plan says so. TDD applies to *new and
> changed* behaviour from now on. Do not implement a feature first and then add superficial tests afterwards
> merely to raise a number.

---

## 4. Requirement → test traceability

```
Business Requirement (BR-NNN)
  → Functional Requirement (FR-AREA-NNN) / Business Rule (BRULE-AREA-NNN)
    → User Story / Use Case
      → Acceptance Criteria (IMP-XXX)
        → Test Scenario
          → Automated Test          ← file + test method name
            → Verified status in docs/IMPLEMENTATION_PLAN.md
```

Tests carry a short doc-comment naming the requirement/ADR they cover (e.g. `IMP-040 — lease lifecycle rules
(BRULE-LEASE-002…)`) or a `Method_WhenCondition_ShouldExpectedResult` name that states the rule. Large
requirement text is never duplicated into the test file — traceability is the goal, not duplication.

---

## 5. Test naming

Behaviour-oriented names in the project's existing style:

```
Method_WhenCondition_IsRejected | IsAccepted | ReturnsExpectedResult
```

Examples:

- `BookAsync_WhenSlotOverlapsAReservedBooking_IsRejected`
- `GrantAccessAsync_WhenExpiryIsInThePast_IsRejected`
- `CreateLeaseAsync_WhenUnitAlreadyHasAnOverlappingActiveLease_IsRejected`
- `GetMyNotificationsAsync_ReturnsOnlyTheCallersNotifications`

A name must state **what** is tested, **under what condition**, and **what is expected**.

---

## 6. Test data & isolation

The database strategy follows the production choice ([ADR-0011](../docs/adr/0011-sqlite-single-file-module-boundaries.md))
— **SQLite, not PostgreSQL**:

- **Unit/service tests** use `Microsoft.EntityFrameworkCore.InMemory` with a
  **unique database name per test** (`$"pmp-{area}-{Guid.NewGuid():N}"`) so tests never share state, never
  race, and never touch a developer's file. Each service receives its own `DbContext` instances.
- **API integration tests** boot the real API against a **throwaway SQLite file inside the temp directory**,
  injected purely through environment variables (`ConnectionStrings__DefaultConnection`, `Jwt__*`,
  `ASPNETCORE_URLS`). The working directory is the temp folder and the database is deleted afterwards, so
  **no test ever modifies the local `pmp.db`** and no test-only hooks exist in `PMP.Api`.
- No test depends on developer-specific configuration, ports, network access or the clock wall (time windows
  are chosen far from the boundary, or arranged relative to `DateTimeOffset.UtcNow`).

Test doubles are used only where a collaborator crosses a module boundary — see
[`TestDoubles.cs`](../tests/PMP.Tests/TestDoubles.cs) (`StubOccupancyProvider`, `StubNotificationService`,
`RecordingCommunicationService`). Services are not mocked wholesale.

---

## 7. Authentication & RBAC testing

Authentication and authorization are security behaviour, so both are tested:

| Concern | Where |
| --- | --- |
| Token contents the policies depend on (subject, email, roles), signature, expiry | `AuthTokenServiceTests` (unit) |
| Register / confirm-email gate / login / refresh rotation / logout revocation | `Integration/MvpEndToEndApiTests` (HTTP) |
| Anonymous access to protected endpoints → 401 | `Integration/MvpEndToEndApiTests`, `Integration/PaymentApiTests` |
| Per-role permissions and forbidden operations → 403 | `Integration/MvpEndToEndApiTests`, `Integration/PaymentApiTests` |
| Resource **ownership** (own vs. other user's data) | `CommunicationServiceTests`, `BookingServiceTests`, `PaymentServiceTests`, `SecurityServiceTests` |
| Manager/Administrator **property scoping** | `ResidentServiceTests`, `LeaseServiceTests`, `BookingServiceTests`, `SecurityServiceTests`, `CommunicationServiceTests` |

Framework internals (Identity stores, JWT middleware) are not re-tested; PMP's own security decisions are.

---

## 8. Bug-fix workflow

```
Bug → Reproduction test (fails) → Fix → Test passes → Regression run
```

Whenever practical, reproduce the defect with a failing automated test *first*, so the fix is proven and the
regression is permanently guarded. If a reproduction test is not practical (e.g. an environment-only issue),
say so in the plan/gap register instead of quietly skipping it.

---

## 9. Running the tests

All commands below were run from the repository root (`c:/Projects/PropertyManagement`).

```bash
# Build the whole solution (no warnings expected)
dotnet build PropertyManagement.sln

# Run the entire backend suite (unit + API integration)
dotnet test PropertyManagement.sln

# Run a single test class
dotnet test tests/PMP.Tests/PMP.Tests.csproj --filter FullyQualifiedName~LeaseServiceTests
```

> The API integration tests boot the real host, so the solution must be built before the suite runs (the
> fixture fails fast with a clear message if `src/PMP.Api/bin/.../PMP.Api.dll` is missing). `dotnet test`
> builds first, so a plain `dotnet test PropertyManagement.sln` is sufficient.

Frontend (no test runner is configured yet — see §12):

```bash
cd frontend
npm ci
npm run build   # tsc -b && vite build
npm run lint    # oxlint
```

---

## 10. Test organisation

```
tests/PMP.Tests/
├── TestDoubles.cs                    # shared stubs/recording fakes for module boundaries
├── ResultTests.cs                    # shared-kernel Result contract
├── AuthTokenServiceTests.cs          # Auth: token contents/signature/expiry      (IMP-040)
├── PropertyServiceTests.cs           # Property: CRUD + manager scoping
├── ResidentServiceTests.cs           # Resident: provisioning, assignment, transfer, move-out, deactivation (IMP-040)
├── MaintenanceServiceTests.cs        # Maintenance: state machine, invalid transitions, ownership
├── PaymentServiceTests.cs            # Payment: obligations, lifecycle, overdue, cancellation
├── LeaseServiceTests.cs              # Lease: overlap, versioning, termination, expiry (IMP-040)
├── CommunicationServiceTests.cs      # Communication: recipient scoping, delivery status, announcements (IMP-040)
├── BookingServiceTests.cs            # Booking: overlap, operating hours, cancellation window (IMP-040)
├── SecurityServiceTests.cs           # Security: visitor state machine, access grants (IMP-040)
└── Integration/
    ├── PmpApiFixture.cs              # real Kestrel host + throwaway SQLite + HTTP helpers
    ├── MvpEndToEndApiTests.cs        # MVP journey + auth/RBAC negatives
    └── PaymentApiTests.cs            # payment endpoints + Accountant scoping
```

Each `*ServiceTests.cs` file maps to one module under `src/PMP.Modules.*` and asserts its core rules; the
integration folder maps to the composed API surface.

---

## 11. Coverage

Coverage tooling is already referenced (`coverlet.collector`) but **no coverage target or percentage is
claimed**. Quality of tests outweighs the number they produce.

To generate a report when needed:

```bash
dotnet test PropertyManagement.sln --collect:"XPlat Code Coverage" --results-directory TestResults
```

This writes a Cobertura XML per test project under `TestResults/` (git-ignored). Read it to find *untested
behaviour*, then add meaningful tests at the right level — do not chase the percentage.

---

## 12. CI/CD and what remains

There is currently **no CI pipeline** (recorded as `GAP-009`, planned as `IMP-042`). No elaborate pipeline was
created silently. The recommended minimal workflow, once `IMP-042` is picked up, is:

1. `dotnet restore`
2. `dotnet build PropertyManagement.sln`
3. `dotnet test PropertyManagement.sln`
4. `cd frontend && npm ci && npm run lint && npm run build`

Not yet covered (tracked, not hidden):

| Area | Status |
| --- | --- |
| Frontend tests (`npm test`) | **Not implemented** — `IMP-041`; build + lint are the current gates |
| Background hosted jobs (maintenance auto-close, lease expiry, payment due dates) | **Not implemented** — remain in `GAP-001` |
| Attachment upload paths | **Not implemented** — remain in `GAP-001` |
| CI pipeline | **Not implemented** — `IMP-042` / `GAP-009` |
| Email transport | **Not implemented** — `GAP-005` (recorded, decision required) |

---

## 13. Change log

| Date | Change |
| --- | --- |
| 2026-09-11 | Document created alongside `IMP-040`. Backend suite expanded 46 → **101 passed / 0 failed**; module-level service tests added for Auth (token), Resident, Lease, Communication, Booking and Security; frontend test tooling and CI remain outstanding. |
