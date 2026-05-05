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

Gate 2 includes empty server project shells only. Domain behavior, persistence,
and runtime composition come in later gates.
