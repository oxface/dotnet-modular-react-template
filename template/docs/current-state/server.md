# Server Current State

The generated template ships a .NET modular-monolith backend using ASP.NET Core
Minimal APIs, EF Core with PostgreSQL, Bondstone command handling, and explicit
module boundaries.

The shipped backend includes:

- Host-owned HTTP composition, OpenAPI metadata for `/api/` endpoints,
  problem-details responses, baseline exception handling, authentication
  middleware, and application-access authorization policy wiring.
- Module Infrastructure EF Core DbContexts with baseline `InitialCreate`
  migrations so generated products can create the first local schemas.
- Bondstone platform libraries for module boundaries, messaging contracts,
  module message registration, PostgreSQL persistence/outbox/inbox storage, and
  Rebus transport dispatch through PostgreSQL or Azure Service Bus adapters.
- The Host starts module outbox dispatcher workers once after composing modules;
  Bondstone module persistence by itself only registers the module DbContext,
  unit of work, inbox, and outbox plumbing.
- Bondstone exposes module outbox maintenance hooks for product-owned
  dead-letter replay and processed-row cleanup workflows.
- `ModularTemplate.ServiceDefaults` for OpenTelemetry, service discovery,
  default HTTP resilience, and development health endpoints.
- SharedKernel primitives for entity, aggregate root, value object,
  single-value object, domain event, domain exception, and shared
  normalization helpers.
- Host feature slices for browser auth endpoints and `GET /api/me`.
- A Products module slice with provider-neutral read contracts, module-owned
  persistence, and `GET /api/products/{productId}`.
- Host module registration delegated through a configuration extension rather
  than direct module wiring in `Program.cs`.
- Command pipeline behaviors for diagnostics and request validation before
  module unit-of-work handling.

Business modules are expected under `server/src/modules` when products add
more behavior. Modules with persistence or external adapters should keep
Contracts, Module, and Infrastructure projects separate.
