---
name: prototype
description: Build throwaway prototypes to answer concrete questions about business logic, state models, data shape, or UI design.
---

# Prototype

A prototype is throwaway code that answers a question. The question decides the shape.

## Pick a branch

Identify which question is being answered:

- "Does this logic / state model feel right?"
  → Use the Logic Prototype process below.

- "What should this look like?"
  → Use the UI Prototype process below.

If genuinely ambiguous and the user isn't reachable, choose the branch that best matches the surrounding code and state the assumption.

## Rules for all prototypes

1. Throwaway from day one and clearly marked as such.
2. Make it trivial to run.
3. No persistence by default.
4. Skip production-level polish, tests, and abstractions.
5. Surface the relevant state after interactions.
6. When validated, fold the decision into the real implementation and keep the prototype on a throwaway branch.

# Logic Prototype

Use this when the question concerns:

- business logic
- state transitions
- state machines
- data shape
- API behavior

## Process

### 1. State the question

Before writing code, explicitly state the question and state model being tested.

### 2. Isolate the logic

Keep the actual logic in a pure, portable module.

Choose the appropriate form:

- reducer
- state machine
- pure functions
- small stateful module

Keep DOM and UI code separate from the logic.

### 3. Build a shareable HTML prototype

Use one self-contained HTML file with inline HTML/CSS/JavaScript.

It should contain:

1. Title and explanation
2. Current state
3. Free-play action buttons
4. Guided walkthrough scenarios

Use domain language rather than technical language.

Test:

- happy path
- awkward edge cases
- illegal actions
- important state transitions

Keep the UI clean and restrained.

### 4. Capture the result

Once the question is answered:

- record the decision
- move validated logic into the real implementation
- preserve the prototype on a throwaway branch
- record the relationship between the prototype and implementation decision

## Logic Prototype anti-patterns

Do not:

- add tests
- connect to the real database
- generalize for hypothetical future requirements
- mix business logic with DOM code
- introduce frameworks/bundlers unnecessarily
- ship the prototype UI to production

# UI Prototype

Use this when the question is:

- "What should this page look like?"
- "Which dashboard layout should we use?"
- "What should this settings page look like?"

## Process

### 1. State the question

Default to 3 radically different variants.

State the plan clearly.

### 2. Generate radically different variants

Variants must differ structurally, not merely by:

- colors
- text
- minor spacing
- cosmetic changes

Consider differences in:

- layout
- information hierarchy
- primary actions
- navigation
- density
- component arrangement

Use the project's existing component/styling system.

### 3. Wire variants together

Use a `?variant=` URL parameter where appropriate.

Default:

- Variant A
- Variant B
- Variant C

The variants should be switchable without duplicating the application's existing data fetching or authentication unnecessarily.

### 4. Floating switcher

Provide:

- previous button
- current variant label
- next button

Support keyboard arrow navigation.

Keep the switcher visually separate from the design being evaluated.

Ensure it cannot appear in production.

### 5. Capture the answer

Record:

- winning variant
- why it won
- useful elements borrowed from other variants

Then:

- promote the winner into the real implementation
- remove losing variants from main
- preserve the prototype on a throwaway branch

## UI Prototype anti-patterns

Do not:

- create variants that only change colors/copy
- share so much layout code that variants aren't genuinely different
- connect prototypes to real mutations
- directly promote prototype code into production