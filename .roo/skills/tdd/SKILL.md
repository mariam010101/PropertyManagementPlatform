---
name: tdd
description: Use test-driven development to implement behavior through public interfaces using a red → green loop, agreed testing seams, and minimal vertical slices.
---

# Test-Driven Development

TDD is the red → green loop. Apply these rules during every implementation cycle.

## Before Implementation

1. Inspect the existing codebase.
2. Read `CONTEXT.md` if it exists.
3. Read relevant ADRs.
4. Identify the requirement or acceptance criterion.
5. Identify the public interface (seam) where the behavior should be tested.
6. If the behavior or seam is unclear, clarify it before writing the test.

## The Red → Green Loop

Follow this cycle:

1. Identify ONE behavior.
2. Write ONE failing test.
3. Run the test and confirm it fails for the expected reason.
4. Implement only enough code to make that test pass.
5. Run the test again.
6. Move to the next behavior.
7. Repeat.

Never implement the whole feature first and add tests afterward.

### Rules

- Red before green.
- One behavior at a time.
- One seam → one test → minimal implementation.
- Do not write speculative code.
- Do not anticipate future tests.
- Work in vertical slices.
- Each test should describe observable behavior.
- Refactoring belongs to code review, not the red → green loop.

## What a Good Test Is

Tests verify behavior through public interfaces, not implementation details.

Good tests:

- Describe WHAT the system does, not HOW it does it.
- Test behavior users or callers care about.
- Use public APIs/interfaces.
- Survive internal refactoring.
- Use an independent expected result.
- Test one logical behavior.

Example:

```typescript
test("resident can submit a maintenance request", async () => {
  const result = await submitMaintenanceRequest({
    title: "Broken heater",
    description: "The heater is not working"
  });

  expect(result.status).toBe("Submitted");
});
```

## PMP Workflow — before implementing a feature

PMP is a System-Analyst project: the requirement comes first and the test makes it testable. Before writing
production code:

1. Identify the relevant **requirement** (`FR-<AREA>-NNN` / `BRULE-<AREA>-NNN`) and the `IMP-XXX` task in
   [`docs/IMPLEMENTATION_PLAN.md`](../../../docs/IMPLEMENTATION_PLAN.md).
2. Identify the **acceptance criteria** that must be provable.
3. Determine the **expected behaviour** — including negative and boundary cases the rules imply.
4. Choose the **test level**: service/unit for isolated rules, API integration for routing/auth/persistence
   behaviour (see [`docs/TESTING.md`](../../../docs/TESTING.md)).
5. Write the **test first**, through the agreed seam (the service `Result` boundary — ADR-0005).
6. Confirm the test **fails for the expected reason** (RED).
7. Implement the **minimum** production code (GREEN).
8. Run the test again.
9. **Refactor** with all tests green.
10. Run the **relevant regression suite** (and the full suite when the change can affect it).
11. Verify no existing functionality was broken.
12. **Report the test results** and update the implementation plan with evidence.

## Bug fixes

```
Bug → Reproduction test (fails) → Fix → Test passes → Regression verification
```

Reproduce the defect with a failing automated test whenever practical, then fix the production code and keep
the test as a permanent guard. If a reproduction test is not practical, record why in the plan/gap register.

## Traceability

Do not duplicate requirement text into tests. Reference the requirement, business rule or `IMP-XXX` task in a
short doc-comment or encode it in a behaviour-oriented test name so the chain
*requirement → acceptance criteria → test* stays auditable.

## Prohibited

- Do **not** implement functionality first and then create superficial tests afterwards merely to increase
  coverage.
- Do **not** write tests that always pass, assert nothing meaningful, depend on arbitrary delays, or lock
  down implementation details.
- Do **not** invent requirements, business rules or behaviour to justify a test. If the requirement is not
  detailed enough to test, record the gap.
- Do **not** mark an `IMP-XXX` task complete while an acceptance criterion is unproven.<｜end▁of▁thinking｜>