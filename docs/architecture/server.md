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
- Shared persistence lives in `ModularTemplate.Persistence`, a Host-owned
  composition project with the concrete EF Core `ModularTemplateDbContext`.
- `ModularTemplateDbContext` implements narrow module persistence interfaces,
  including the Identity persistence surface for local users and application
  access records.
- The Migrator references the persistence project and is the entrypoint for
  applying Host-owned migrations later.
- Durable intermodule messaging is deferred until a concrete workflow needs it.
- SharedKernel contains only domain primitives at this gate: entity,
  aggregate-root, value-object, domain-event, and domain-exception base types.
- ServiceDefaults provides OpenTelemetry, service discovery, default HTTP
  resilience, and development health endpoints.
- Host configures problem-details responses, baseline exception handling, and
  shared persistence registration.
- Host composes minimal API authentication, the `GET /api/me` current-user
  endpoint, and an application-access authorization policy while Identity owns
  local identity and access decisions behind contracts.
- Host registers ASP.NET Core OpenAPI metadata for template-owned `/api/`
  endpoints. Build-time OpenAPI generation writes the checked-in client source
  document used by the frontend API client package.

## Backend Feature Shape

Host API behavior should be organized as vertical feature slices under
`ModularTemplate.Host/Features/{FeatureName}`. Endpoint mapping, endpoint-local
response models, request/claim adapters, and feature-specific HTTP composition
should stay close together unless another feature needs the same abstraction.

Endpoints that are consumed by browser code should include stable names,
response metadata, and tags so generated clients have durable operation names
and schemas. Host-owned browser auth routes such as login, logout, callback,
and signed-out callback routes are platform routes and should stay out of the
generated API client surface.

Modules should keep application behavior behind contracts. Contracts may expose
provider-neutral DTOs and service/query abstractions, but they must not expose
EF entities, aggregate internals, `ClaimsPrincipal`, provider SDK types, or
Host-specific HTTP concepts.

## DDD And CQRS Direction

Domain objects with identity and behavior should use explicit terminology:
aggregate root, entity, value object, repository, query handler, or command
handler. If a type is intentionally only a persistence DTO or transitional
scaffold, document that in the feature artifacts and stable docs.

The template's durable direction is CQRS through narrow module contracts and
Mediator-backed command/query handlers where behavior grows beyond a very small
module service. Repository abstractions should represent module-owned domain
persistence and should sit inside the module boundary; infrastructure
implements those abstractions through narrow DbContext interfaces.

Direct module stores are acceptable only as temporary scaffolding when a feature
precedes the full repository/query-handler conventions. They should be replaced
or reshaped before the feature is accepted as durable template guidance.

## Testing Conventions

Tests should use substitutes for simple collaborator interactions. In-memory
fakes are reserved for stateful behavior that is central to the test and should
remain test-only. Integration tests that verify persistence behavior should use
real external IO through the documented Testcontainers pattern.
