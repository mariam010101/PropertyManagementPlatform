# PMP Documentation Index

Entry point for the repository's system-analysis and engineering documentation. Files are kept at their
existing paths so that links across the project remain stable; this page groups them by purpose.

---

## Requirements & traceability

| Artifact | File | Purpose |
| --- | --- | --- |
| Requirements traceability | [`traceability.md`](traceability.md) | The traceability scheme: identifiers, the BR → FR → ADR → IMP → code → test chain, sources of truth. |
| Functional-requirements compliance audit | [`requirements-compliance.md`](requirements-compliance.md) | Per-FR status (MET / PARTIAL / NOT MET) with code evidence, by business area (BR-001..BR-012). |
| Live traceability matrix | [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) §7 | Requirement set → ADR → task → API → code → test. |
| Domain glossary | [`glossary.md`](glossary.md) | Canonical domain, platform, architecture and requirements-analysis terminology. |

## Architecture & decisions

| Artifact | File | Purpose |
| --- | --- | --- |
| Architecture Decision Records | [`adr/`](adr/) | ADR-0001..ADR-0012: modular monolith, identity/JWT, RBAC, result pattern, module ownership, MVP scope, provisioning, maintenance state machine, derived occupancy, SQLite, post-MVP modules. |
| UML component diagram | [`diagrams/PropertyManagement_Component_Diagram.drawio`](diagrams/PropertyManagement_Component_Diagram.drawio) | Editable component/architecture diagram (open in draw.io / diagrams.net). |
| Architecture overview | [`../README.md`](../README.md) §System Architecture | Narrative of the modular monolith and cross-module wiring. |

## Delivery & project management

| Artifact | File | Purpose |
| --- | --- | --- |
| Implementation plan (master checklist) | [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) | Authoritative roadmap: task status, acceptance criteria, progress model, next eligible task. |
| Implementation gap analysis | [`IMPLEMENTATION_GAP_ANALYSIS.md`](IMPLEMENTATION_GAP_ANALYSIS.md) | GAP-001..GAP-022: defects, missing behaviour and decision-required items. |
| Development plan (historical) | [`../plans/pmp-development-plan.md`](../plans/pmp-development-plan.md) | Original MVP planning/grilling record; superseded in parts — see its status banner. |
| Agent operating rules | [`../agent.md`](../agent.md) | How the implementation agent selects, verifies and records work. |
| Contributing guide | [`../CONTRIBUTING.md`](../CONTRIBUTING.md) | Setup, workflow, coding and documentation conventions. |
| Security policy | [`../SECURITY.md`](../SECURITY.md) | Security posture, secret handling and vulnerability reporting. |

## External documentation (not duplicated here)

Detailed requirements catalogues, business-rule pages, use cases and process (BPMN) modelling live in the
project's **Confluence `PMP` space**; delivery tracking lives in **Jira**. This repository summarises and links
to them rather than copying them, so the two never diverge. See
[`../README.md`](../README.md) §Documentation for the relationship between Jira, Confluence and GitHub.
