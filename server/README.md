# Server

Backend projects live here.

Backend project layout:

- `src/ModularTemplate.Host`
- `src/ModularTemplate.Migrator`
- `src/ModularTemplate.Persistence`
- `src/ModularTemplate.ServiceDefaults`
- `src/ModularTemplate.SharedKernel`
- `src/modules/ModularTemplate.Identity.Contracts`
- `src/modules/ModularTemplate.Identity`
- `src/modules/ModularTemplate.Identity.Infrastructure`

These projects provide the backend foundation: Host composition,
ServiceDefaults, shared persistence, SharedKernel primitives, Migrator wiring,
and the initial Identity module boundary.

`ModularTemplate.Persistence` contains the concrete EF Core DbContext shell.
`ModularTemplate.Migrator` is wired to apply Host-owned migrations later, but
the template does not include generated migrations yet.
