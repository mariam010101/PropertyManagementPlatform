# Services return Result instead of throwing exceptions

**Status:** accepted

Application services return a small **`Result` / `Result<T>`** type (`Succeeded`, `Error`) rather than throwing exceptions for expected failures (validation, authorization, "not found"). Controllers map a failed result to `400 Bad Request { error }`.

**Considered options**

- **Exceptions for flow control** — rejected: expected business outcomes are not exceptional; exceptions make error handling implicit and are slower.
- **Throwing `DomainException` for invariants only** — retained: genuine programming errors still throw.

**Consequences**

- Consistent API error shape across all endpoints.
- Services are easier to unit test (assert on `Result.Succeeded`/`Error`).
