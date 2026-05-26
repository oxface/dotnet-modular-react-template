# Server Current State

The generated template ships a .NET modular-monolith backend using ASP.NET Core
Minimal APIs, EF Core with PostgreSQL, Mediator, and explicit module
boundaries.

The shipped backend includes:

- Host-owned HTTP composition, OpenAPI metadata for `/api/` endpoints,
  problem-details responses, baseline exception handling, authentication
  middleware, and application-access authorization policy wiring.
- Module Infrastructure EF Core DbContexts with baseline `InitialCreate`
  migrations so generated products can create the first local schemas.
- `ModularTemplate.Outbox` and `ModularTemplate.Transport` platform libraries
  for durable outbox/inbox persistence, polling, and Rebus transport bridging.
- `ModularTemplate.ServiceDefaults` for OpenTelemetry, service discovery,
  default HTTP resilience, and development health endpoints.
- SharedKernel primitives for entity, aggregate root, value object,
  single-value object, domain event, domain exception, and shared
  normalization helpers.
- Host feature slices for browser auth endpoints and `GET /api/me`.
- Host module registration delegated through a configuration extension rather
  than direct module wiring in `Program.cs`.
- A command request-validation pipeline that executes registered validators
  before command transaction handling.

Business modules are expected under `server/src/modules` when products add
more behavior. Modules with persistence or external adapters should keep
Contracts, Module, and Infrastructure projects separate.
