# PMP — Requirements Traceability

This document is the repository-level entry point for **requirements traceability**: how a business need in the
Property Management Platform is followed through analysis, design, implementation and testing.

It is deliberately a **map, not a duplicate**. The authoritative per-task matrix lives in
[`docs/IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) §7; this page explains the scheme and the sources so a
reviewer can follow the chain without guessing.

---

## 1. Why traceability

Traceability lets a reviewer answer three questions with evidence:

1. **Why does this code exist?** — the requirement and the decision that authorised it.
2. **Is it done?** — the acceptance criteria and the automated test that proves the behaviour.
3. **What is missing?** — the gap register, recorded rather than silently dropped.

## 2. Identifier scheme

Identifiers are stable and are never renumbered. Pre-existing IDs are preserved.

| Prefix | Meaning | Example | Where it is defined |
| --- | --- | --- | --- |
| `BR-0NN` | Business Requirement (business area) | `BR-004` Maintenance Management | Confluence `PMP` space; rolled up in [`requirements-compliance.md`](requirements-compliance.md) §1 |
| `FR-<AREA>-0NN` | Functional Requirement | `FR-MNT-005` | Confluence `PMP` space; per-FR audit in [`requirements-compliance.md`](requirements-compliance.md) |
| `BRULE-<AREA>-0NN` | Business Rule / invariant | `BRULE-MNT-004` | Confluence `PMP` space; noted per module in [`requirements-compliance.md`](requirements-compliance.md) |
| `NFR` | Non-Functional Requirement | *(no IDs assigned yet)* | **Traceability gap — see [`GAP-013`](IMPLEMENTATION_GAP_ANALYSIS.md)**. Not invented. |
| `ADR-0NNN` | Architecture Decision Record | `ADR-0009` Maintenance state machine | [`docs/adr/`](adr/) |
| `IMP-0NN` | Implementation task | `IMP-005` Maintenance module | [`docs/IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) |
| `GAP-0NN` | Known gap / defect / decision-required item | `GAP-005` record-only email | [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](IMPLEMENTATION_GAP_ANALYSIS.md) |

> **Note on BPMN / UML.** The repository contains one editable **UML component diagram**
> ([`PropertyManagement_Component_Diagram.drawio`](diagrams/PropertyManagement_Component_Diagram.drawio)).
> No BPMN process models or use-case diagrams are committed. They are not claimed here; publishing them is
> listed as a recommended improvement in the README.

## 3. The chain

```
Business Goal
   └── BR-0NN            business requirement (Confluence)
         └── FR-<AREA>-0NN    functional requirement  ──┐
         └── BRULE-<AREA>-0NN business rule / invariant ─┤
                                                          ▼
                                           ADR-0NNN  architecture decision
                                                          │
                                                          ▼
                                           IMP-0NN   implementation task
                                           (acceptance criteria in the plan)
                                                          │
                                                          ▼
                              code / API  ────────────────┘
                              (Controller + Service + Entity + EF migration)
                                                          │
                                                          ▼
                              automated test  (xUnit unit + API integration)
                                                          │
                                                          ▼
                              verified status recorded in the implementation plan

              any break in the chain ──▶ GAP-0NN recorded in the gap analysis
```

## 4. Sources of truth (in priority order)

| # | Source | Governs |
| --- | --- | --- |
| 1 | [`docs/IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) | What may be worked on, in what order, and its verified status. |
| 2 | [`docs/adr/`](adr/) | Approved architecture decisions. |
| 3 | [`docs/requirements-compliance.md`](requirements-compliance.md) | FR / BRULE coverage. |
| 4 | [`docs/IMPLEMENTATION_GAP_ANALYSIS.md`](IMPLEMENTATION_GAP_ANALYSIS.md) | GAP records. |
| 5 | [`plans/pmp-development-plan.md`](../plans/pmp-development-plan.md) | Historical planning context (superseded in parts). |
| 6 | Repository code | Actual behaviour. Documentation conflicts are recorded, never silently resolved. |

## 5. Reading the matrix

The live matrix — **requirement set → ADR → task → code/API → test** — is
[`docs/IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) §7. It is intentionally maintained in one place so it
cannot drift into two copies.

A per-FR view (status 🟢 MET / 🟡 PARTIAL / 🔴 NOT MET with file-and-line evidence) is in
[`docs/requirements-compliance.md`](requirements-compliance.md).

## 6. Documented coverage vs verified coverage

The audit and the implementation plan deliberately distinguish two different things:

- **Documented coverage** — requirements that have a matching implementation in code
  (`requirements-compliance.md`).
- **Verified coverage** — requirements whose behaviour is proven by an automated test or a recorded manual check
  (`IMPLEMENTATION_PLAN.md`).

The difference is the substance of [`GAP-001`](IMPLEMENTATION_GAP_ANALYSIS.md) (test coverage) and of tasks
`IMP-040` (per-module tests) and `IMP-041` (frontend tests). This project does not report them as the same number.

## 7. Governance

Traceability is kept current as part of the task loop: when a task completes, its plan row, its acceptance
criteria, the gap register and — where relevant — the compliance audit are updated together. A change that
alters a requirement is not made silently in code.
