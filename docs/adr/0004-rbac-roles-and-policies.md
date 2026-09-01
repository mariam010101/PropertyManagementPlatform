# RBAC with platform roles and policy-based authorization

**Status:** accepted

Authorization is **role-based (RBAC)** with four platform roles — `Administrator`, `PropertyManager`, `Resident`, `Technician` — enforced via ASP.NET **policy-based authorization** (role requirements) at the endpoint layer, plus **data scoping in services** (a property manager only sees data for their assigned properties; least privilege).

**Considered options**

- **Attribute-based access control (ABAC) / per-resource permissions** — rejected: overkill for the MVP; roles map cleanly to the business personas.
- **Per-object ACLs** — rejected: adds significant complexity without a demonstrated need.

**Consequences**

- Adding a new role is a configuration change; new policies are declared centrally in the Auth module.
- Future modules (Accountant, Security Staff) add roles, not a new authorization model.
