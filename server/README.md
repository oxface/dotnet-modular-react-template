# Server

Backend projects live here.

Current project shells:

- `src/ModularTemplate.Host`
- `src/ModularTemplate.Migrator`
- `src/ModularTemplate.ServiceDefaults`
- `src/ModularTemplate.SharedKernel`
- `src/modules/ModularTemplate.Identity.Contracts`
- `src/modules/ModularTemplate.Identity`
- `src/modules/ModularTemplate.Identity.Infrastructure`

These projects are intentionally empty or near-empty. The Host currently has
only baseline service defaults, problem-details/error handling, and health
endpoints. Domain behavior, persistence, auth/session plumbing, migrations, and
tests come in later gates.
