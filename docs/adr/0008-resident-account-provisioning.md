# Resident self-registration; staff admin-provisioned; unit assigned by staff

**Status:** accepted

Residents **self-register** (email-verified, strong password, lockout after 5 attempts). **Staff** (Property Manager, Technician, Administrator) are created/invited by an Administrator. After registration, the resident's **unit is assigned by an admin/manager who verifies identity** — there is no self-claiming of units.

**Considered options**

- **Self-registration for all roles** — rejected: insecure; staff provisioning must be controlled.
- **Unit-specific invite codes at registration** — rejected for MVP: added UX complexity; admin assignment is simplest and most secure.

**Consequences**

- A resident's profile is auto-created at registration by the API composition root (Auth creates user → Resident creates profile).
- Until a unit is assigned, the resident cannot submit maintenance requests (submission requires a current unit association).
