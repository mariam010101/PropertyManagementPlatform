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