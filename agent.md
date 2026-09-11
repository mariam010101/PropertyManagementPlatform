# PMP Implementation Agent

Operating instructions for the primary implementation agent on the Property Management Platform (PMP).
This file describes **how to work**. It is not the roadmap — the roadmap is the implementation plan.

---

## 1. Sources of truth (in order)

| Priority | Source | What it governs |
| --- | --- | --- |
| 1 | [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) | **Master implementation checklist.** What may be worked on, in what order, and its verified status. |
| 2 | [`docs/adr/`](docs/adr/) | Approved architecture decisions (module boundaries, identity, RBAC, service result pattern, derived occupancy, MVP scope, post-MVP modules). |
| 3 | [`docs/requirements-compliance.md`](docs/requirements-compliance.md) | Functional-requirement (FR-*) and business-rule (BRULE-*) coverage. |
| 4 | [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](docs/IMPLEMENTATION_GAP_ANALYSIS.md) | GAP-XXX records: known defects, missing behaviour, decision-required items. |
| 5 | [`plans/pmp-development-plan.md`](plans/pmp-development-plan.md), [`README.md`](README.md) | Background and narrative context (may contain drift — see GAP-010). |
| 6 | Repository code | The actual behaviour. When docs and code disagree, the disagreement is recorded, never silently resolved. |

**Never invent a separate roadmap.** Work is selected from `docs/IMPLEMENTATION_PLAN.md`.

---

## 2. Required skills

Use the existing skills. Do not create duplicate skills, methodologies or roadmaps.

| Work type | Required skills |
| --- | --- |
| Any implementation | **Implementation Skill** (`implement`) + **Grilling Skill** (`grilling`) |
| UI / UX, layout, components, interactions, responsiveness, visual consistency, UI review | Implementation + **Taste Skill** (`taste`) + Grilling |
| Refactoring / code-quality review | Implementation + Grilling |
| Test-first work at an agreed seam | `tdd` (alongside Implementation) |
| Glossary / `CONTEXT.md` / ADR authoring | `domain-modeling` |

Before implementation, check the available skills and load only those relevant to the current task.

---

## 3. Task execution loop

For every `IMP-XXX` (or `GAP-XXX`) item, follow this loop exactly.

### STEP 1 — SELECT
Read `docs/IMPLEMENTATION_PLAN.md` and select the highest-priority **eligible unchecked** task.
Check its status, dependencies, related requirements, ADRs, known gaps and affected modules.
Eligibility order: (1) blocking/foundation, (2) required MVP, (3) high-priority FRs, (4) required cross-module
integration, (5) required testing, (6) security/NFR, (7) DevOps/deployment, (8) technical debt, (9) post-MVP.
Dependencies must be satisfied first. Do not pick a task at random.

### STEP 2 — UNDERSTAND
Read only what the task needs: its plan section, its acceptance criteria, the relevant ADRs and requirement IDs,
the gap-analysis entries, the affected source files, API contracts and existing tests. Avoid repository-wide scans.

### STEP 3 — IMPLEMENT
Implement the selected task and only the supporting changes it genuinely requires.
Do not add unrelated features, endpoints, business rules or UI.

### STEP 4 — VERIFY
Verify, with evidence:
- every acceptance criterion of the task;
- actual behaviour (not the existence of a file, method, endpoint or component);
- relevant API behaviour and database behaviour (migrations, invariants, retention);
- authorization/RBAC and ownership scoping;
- cross-module integration (host wiring, adapters, notifications);
- relevant automated tests; the build; and the full suite when the change can affect it;
- ADR compliance and requirement compliance.

For UI work additionally verify: responsive behaviour, loading, empty, error and forbidden states, disabled/hover/focus
and validation states, consistency with the established design system, and the Taste pre-flight checklist.

### STEP 5 — CHECK THE WORK
Ask: **"Can I prove that every acceptance criterion for this task is satisfied?"**
- **YES** → mark `[x] COMPLETE`.
- **NO** → do not check it. Mark `IN_PROGRESS`, `BLOCKED` or `GAP` and document exactly what remains.

### STEP 6 — UPDATE THE PLAN
Immediately after verification, update `docs/IMPLEMENTATION_PLAN.md`:
task checkbox, status, completion %, evidence/verification notes, remaining work, related gaps, overall progress,
module/MVP progress where affected, and the next eligible task. Update
`docs/IMPLEMENTATION_GAP_ANALYSIS.md` when a gap is created, changed or resolved.

### STEP 7 — CONTINUE
Return to `docs/IMPLEMENTATION_PLAN.md` and repeat from STEP 1 with the next eligible unchecked item.

---

## 4. Checklist status vocabulary

| Status | Meaning |
| --- | --- |
| `[ ] NOT_STARTED` | No work begun. |
| `[ ] IN_PROGRESS` | Partially implemented; verification or remaining scope outstanding. |
| `[ ] BLOCKED` | Requires an external decision or a dependency that is not satisfied. |
| `[ ] NEEDS_REVIEW` | Implemented; verification/hardening remains. |
| `[ ] IMPLEMENTED` | Code exists; acceptance criteria not yet proven. |
| `[x] VERIFIED` | Implemented + tested with one clearly identified, separately tracked open item. |
| `[x] COMPLETE` | Implemented, acceptance criteria met, tests pass, integration complete, ADR/requirement compliant. |
| `[ ] GAP` | Missing or incorrect versus requirements. |
| `[ ] DEFERRED` | Explicitly out of current scope by an approved decision. |

Use `[x] COMPLETE` **only** after successful verification. Elsewhere, keep the box unchecked and state the reason.

---

## 5. Evidence-based completion

A task is complete only when all applicable conditions hold: implementation exists, acceptance criteria pass, required
functionality works, relevant tests pass, build/validation passes, integration works, requirements are satisfied, ADRs
are respected, and no blocking issue remains.

Not evidence on its own: a file exists; a method exists; an endpoint exists; a component renders; documentation claims
it works. Verify actual behaviour.

---

## 6. When verification fails

Do not check the task. Record what failed, why, what remains, whether another task is required and whether it is
blocked. Create or update a `GAP-XXX` entry in `docs/IMPLEMENTATION_GAP_ANALYSIS.md`. Never silently skip unfinished
work or make the plan look complete while work remains.

---

## 7. Progress reporting

Recompute progress from verified state after meaningful work — never from file, endpoint or document counts, and never
beyond what completed tasks support. Keep synchronized: overall PMP %, MVP %, module/layer %, completed, in-progress,
blocked and gap counts.

---

## 8. Scope boundaries

- **MVP (ADR-0007 + ADR-0012 clarification):** Auth & User Management, Property Management, Resident Management,
  Maintenance Management, RBAC (roles/policies + admin UI).
- **Post-MVP (ADR-0012):** Communication, Lease, Payment + Financial Reporting/Accountant, Booking, Security & Visitor.
- **Out of scope until instructed:** BR-012 mobile platform (IMP-027), real payment-provider integration, access-control
  hardware integration. Post-MVP work must not start ahead of incomplete MVP verification, and IMP-027 must not start
  before IMP-040 is green.

---

## 9. UI dependency order

UI redesign work follows this order; do not skip a dependency without recording the reason in the implementation plan:

1. `GAP-017` — Identity contract (self-identity so a reload keeps the session)
2. `GAP-018` — Shell/layout, role-aware navigation, forbidden state
3. `GAP-019` — Design tokens, primitives, icon family
4. `GAP-020` — Dashboard data
5. `GAP-022` — Authentication screens
6. `GAP-021` — Module-by-module client coverage and screens (Property → Resident → Maintenance → Lease → Booking)
7. Non-blocking work only after UI stabilization

---

## 10. Confluence synchronization

The Confluence page **`PMP Implementation Plan`** (page id `27262978`, space `PMP`) is the live, human-editable mirror of
`docs/IMPLEMENTATION_PLAN.md`.

- Always update **that same page**. Never create a second implementation-plan page.
- After meaningful completed work: update `docs/IMPLEMENTATION_PLAN.md` first, then the Confluence page, and keep
  checkboxes, statuses, counts and percentages identical.
- The page must clearly show what is complete, in progress, blocked, remaining and next.
- The separate page titled `Project Plan` (timeline/budget/risk/backlog) must not be overwritten by this dashboard.
- If no Confluence tool is available, record the pending sync explicitly in the plan's change log — never silently skip it.

---

## 11. Token efficiency

Paid API tokens are in use. Be extremely efficient:

- Use the implementation plan as the primary navigation mechanism.
- Do not read the whole repository, re-read unchanged documentation, or redo completed audits.
- Read only the files relevant to the current task; reuse previously established findings.
- Avoid redundant tool calls; prefer targeted searches over broad scans.
- Keep reports concise.

---

## 12. Protected project rules

Do not:
- invent requirements, APIs or business rules;
- introduce unrelated features or remove required functionality;
- create duplicate components unnecessarily;
- change architecture without justification;
- reintroduce Voting / Community Voting;
- hardcode user-specific payment amounts;
- silently ignore gaps or silently mark incomplete work complete.

Follow approved ADRs and requirements. If an ADR and the implementation plan conflict, identify the conflict and follow
the current approved ADR (newest approved ADR wins on module scope).

---

## 13. Response format after each task

```text
Task: IMP-XXX
Status: COMPLETE / IN_PROGRESS / BLOCKED / GAP

Implemented:
- ...

Verified:
- ...

Remaining:
- ...

Plan updated: YES / NO
Checklist: [x] / [ ]

Overall PMP: XX%
MVP: XX%

Next: IMP-XXX — Task name
```

If the task is incomplete, the checklist must remain unchecked. The implementation plan is the controlling checklist —
always return to it and continue with the next eligible unchecked item.
