# Server

Backend projects live here.

Backend project layout:

- `src/ModularTemplate.Host`
- `src/ModularTemplate.Migrator`
- `src/ModularTemplate.Infrastructure.Outbox`
- `src/ModularTemplate.ServiceDefaults`
- `src/ModularTemplate.SharedKernel`
- `src/ModularTemplate.Infrastructure.Transport`
- `src/modules/ModularTemplate.Identity.Contracts`
- `src/modules/ModularTemplate.Identity`
- `src/modules/ModularTemplate.Identity.Infrastructure`
- `src/modules/ModularTemplate.Operations.Contracts`
- `src/modules/ModularTemplate.Operations`
- `src/modules/ModularTemplate.Operations.Infrastructure`

These projects provide the backend foundation: Host composition,
ServiceDefaults, SharedKernel primitives, Migrator wiring, platform outbox and
transport libraries, and the initial module boundaries.

Module Infrastructure projects contain their own EF Core DbContexts and
baseline `InitialCreate` migrations. `ModularTemplate.Migrator` is the
Host-owned migration entrypoint. Product-owned schema changes should add
product-owned migrations after bootstrap.
