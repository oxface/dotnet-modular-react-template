# Server Architecture

The backend target is a .NET modular monolith using ASP.NET Core Minimal APIs,
EF Core with PostgreSQL, and explicit module boundaries.

Current direction:

- The Host owns HTTP composition, platform authentication mechanics, and shared
  persistence composition.
- Business modules live under `server/src/modules` when server projects are
  introduced.
- Modules with persistence or external adapters use separate Contracts, Module,
  and Infrastructure projects.
- Module stores depend on narrow module DbContext interfaces, not the concrete
  Host DbContext.
- Durable intermodule messaging is deferred until a concrete workflow needs it.
- SharedKernel contains only domain primitives at this gate: entity,
  aggregate-root, value-object, domain-event, and domain-exception base types.
- ServiceDefaults provides OpenTelemetry, service discovery, default HTTP
  resilience, and development health endpoints.
- Host configures problem-details responses and baseline exception handling.

Gate 5 includes Host foundation only. Domain behavior, persistence, auth, and
feature runtime composition come in later gates.
