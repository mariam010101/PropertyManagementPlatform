---
name: domain-modeling
description: Build and sharpen a project's domain model. Use when discussing codebase terminology, writing or editing a CONTEXT.md, or recording or editing an ADR.
---

# Domain Modeling

Actively build and sharpen the project's domain model as you design. This is the active discipline: challenging terms, inventing edge-case scenarios, and writing the glossary and decisions down the moment they crystallise.

## File structure

Most repos have a single context:

/
├── CONTEXT.md
├── docs/
│   └── adr/
└── src/

If a CONTEXT-MAP.md exists at the root, the repo has multiple contexts. The map points to where each one lives.

Create files lazily: only when you have something to write. If no CONTEXT.md exists, create one when the first term is resolved. If no docs/adr/ exists, create it when the first ADR is needed.

## During the session

### Challenge against the glossary

When the user uses a term that conflicts with the existing language in CONTEXT.md, call it out immediately.

### Sharpen fuzzy language

When the user uses vague or overloaded terms, propose a precise canonical term.

### Discuss concrete scenarios

When domain relationships are being discussed, stress-test them with specific scenarios. Invent scenarios that probe edge cases and force the user to be precise about the boundaries between concepts.

### Cross-reference with code

When the user states how something works, check whether the code agrees. If you find a contradiction, surface it.

### Update CONTEXT.md inline

When a term is resolved, update CONTEXT.md right there. Don't batch these up.

CONTEXT.md should be totally devoid of implementation details. It is a glossary and nothing else.

### Offer ADRs sparingly

Only offer to create an ADR when all three are true:

1. Hard to reverse: the cost of changing your mind later is meaningful.
2. Surprising without context: a future reader will wonder why it was done this way.
3. The result of a real trade-off: there were genuine alternatives and one was selected for specific reasons.

If any of the three is missing, skip the ADR.

# ADR Format

ADRs live in docs/adr/ and use sequential numbering:

0001-slug.md
0002-slug.md
0003-slug.md

Create docs/adr/ lazily: only when the first ADR is needed.

## ADR Template

# {Short title of the decision}

{1-3 sentences: what's the context, what did we decide, and why.}

That's it. An ADR can be a single paragraph. The value is recording that a decision was made and why, not filling out unnecessary sections.

## Optional ADR Sections

Only include these when they add genuine value.

- Status: proposed | accepted | deprecated | superseded by ADR-NNNN
- Considered Options: only when rejected alternatives are worth remembering
- Consequences: only when non-obvious downstream effects need to be called out

## ADR Numbering

Scan docs/adr/ for the highest existing number and increment by one.

## What qualifies as an ADR

Use ADRs for:

- Architectural shape
- Integration patterns between contexts
- Technology choices carrying meaningful lock-in
- Boundary and scope decisions
- Deliberate deviations from the obvious path
- Constraints not visible in the code
- Rejected alternatives when the reason for rejection is non-obvious

# CONTEXT.md Format

## Structure

# {Context Name}

{One or two sentence description of what this context is and why it exists.}

## Language

**{Term}**:

{One or two sentence definition of what the term IS.}

_Avoid_: {alternative terms}

## Rules

- Be opinionated. When multiple words exist for the same concept, pick the best one and list the others under _Avoid_.
- Keep definitions tight. One or two sentences maximum.
- Define what something IS, not what it does.
- Only include terms specific to this project's context.
- General programming concepts do not belong in CONTEXT.md.
- Group terms under subheadings when natural clusters emerge.

## Single vs Multi-Context Repositories

If CONTEXT-MAP.md exists, read it to find the contexts.

If only a root CONTEXT.md exists, treat the repository as a single context.

If neither exists, create a root CONTEXT.md lazily when the first term is resolved.

When multiple contexts exist, infer which context the current topic relates to. If unclear, ask the user.