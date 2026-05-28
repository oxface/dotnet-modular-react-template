# Server Architecture

The backend target is a .NET modular monolith using ASP.NET Core Minimal APIs,
EF Core with PostgreSQL, and explicit module boundaries.

Durable direction:

- The Host owns HTTP composition, platform authentication mechanics, transport
  composition, and module composition.
- Business modules live under `server/src/modules`.
- Modules with persistence or external adapters use separate Contracts, Module,
  and Infrastructure projects.
- Module stores and query adapters inside a module Infrastructure project may
  depend on that module's concrete DbContext. Other modules still interact
  through provider-neutral contracts, not EF sets or infrastructure types.
- Each module Infrastructure project owns its EF Core DbContext, schema, and
  baseline `InitialCreate` migration.
- The Migrator references module Infrastructure projects and migrates module
  contexts. Product-owned schema changes should add product-owned migrations
  after bootstrap.
- SharedKernel contains dependency-light domain primitives, validation
  contracts, normalization helpers, and messaging contracts shared across
  module and platform libraries.
- Domain events and outbox messages are persisted in each module schema by the
  shared Infrastructure library.
- ServiceDefaults provides OpenTelemetry, service discovery, default HTTP
  resilience, and development health endpoints.
- Host configures problem-details responses, baseline exception handling, and
  transport registration.
- Host composes minimal API authentication, browser auth endpoints,
  current-user HTTP endpoints, and application-access authorization policies
  while modules own local identity and access decisions behind contracts. The
  Host does not grant bootstrap authorization during web startup; initial
  setup belongs to Migrator or other explicit setup tooling.
- Host registers ASP.NET Core OpenAPI metadata for template-owned `/api/`
  endpoints. Build-time OpenAPI generation writes the checked-in client source
  document used by the frontend API client package.

## Backend Feature Shape

Host API behavior should be organized as vertical feature slices under
`ModularTemplate.Host/Features/{FeatureName}`. Endpoint mapping, endpoint-local
response models, request/claim adapters, and feature-specific HTTP composition
should stay close together unless another feature needs the same abstraction.

Tightly coupled request, command, result, validator, and handler types may live
in the same file when they change together and are not reused outside the
feature or use case. Split them only when the type becomes shared, materially
large, or owned by a different boundary.

Endpoints that are consumed by browser code should include stable names,
response metadata, and tags so generated clients have durable operation names
and schemas. Host-owned browser auth routes such as login, logout, callback,
and signed-out callback routes are platform routes and should stay out of the
generated API client surface.

Modules should keep application behavior behind contracts. Contracts may expose
provider-neutral DTOs and service/query abstractions, but they must not expose
EF entities, aggregate internals, `ClaimsPrincipal`, provider SDK types, or
Host-specific HTTP concepts.

Host `Program.cs` should stay a composition outline. Module registration should
be delegated to a configuration extension that wires module services,
infrastructure adapters, Mediator assemblies, module pipeline behavior, and
module endpoint mapping.

## DDD And CQRS Direction

Domain objects with identity and behavior should use explicit terminology:
aggregate root, child entity, value object, repository, query handler, command
handler, DTO, or artifact payload. Records are a C# representation choice, not
automatic domain value objects. A record is a value object only when the
feature docs and code model its invariants, equality, lifecycle, and ownership
as a domain concept.

The template's durable direction is CQRS through narrow module contracts and
Mediator-backed command/query handlers where behavior grows beyond a very small
module service. Repository abstractions represent command-side domain
persistence for aggregate loading and saving. They sit inside the module
boundary, and infrastructure implements them through module-owned DbContexts.
Query handlers and read models provide provider-neutral projections for callers
that need read-side data. Avoid `Reader` services unless a feature artifact
intentionally documents why that term is clearer than a query handler, read
model, or repository.

Modules should use the `Mediator` library's command/query abstractions instead
of template-owned dispatcher abstractions. Command handlers mutate aggregates
through module-owned repositories and rely on a Mediator pipeline behavior to
run the command inside the selected module unit of work and save changes once
after successful command handling. Module Infrastructure registers assemblies
that contain its persistent Mediator command handlers with
`AddModulePersistence<{Module}DbContext>()`; the registration scans those
handlers and the pipeline uses the discovered command types to select one
module DbContext for the command. Query handlers read provider-neutral state
and do not save changes.

Command handlers should get decision-making data through repositories or
command-side read ports, not by calling query handlers. Query handlers are
application read use cases for callers that need read-side data.

Cross-module synchronous reads should use in-process query contracts from the
target module's Contracts project. Cross-module asynchronous work should use
durable commands or integration events through the outbox/Rebus pipeline.
Durable commands are send-and-forget: callers may receive an acceptance record
and operation id, but handler results are observed later through query
contracts, operation status, read models, or follow-up integration events.
Module code should send durable commands through `IDurableCommandSender` from
inside the source module's command unit of work so the source module outbox row
is committed consistently with module state.
Receive-side Rebus handlers should be transport adapters that delegate state
changes to target-module Mediator commands; persistence remains in the Mediator
module unit-of-work pipeline.
See [Intermodule Communication](intermodule-communication.md) for the detailed
pattern guide, message lifecycle, and module scaffolding checklist.
Host-level orchestration is reserved for API/user workflows that do not belong
to one module; module-owned orchestration should stay inside the owning module.
Generated products should cover real communication workflows with product tests.
Template-framework communication examples are maintained in the factory root
test project, outside the generated-product payload.

Aggregates own domain transitions and raise domain events for relevant actions.
Child entities owned by an aggregate root should keep constructors, factories,
and mutators private or internal to the aggregate unless the model documents a
reason for independent lifecycle control. Domain event classes declare stable
aggregate type, event type, and version metadata with explicit attributes so
persisted event rows do not depend on CLR type names.

Surface validation may use pipeline behaviors and colocated validators for
request shape, authorization preconditions, and cross-field input rules. Domain
invariants remain inside aggregates, child entities, and value objects so they
hold regardless of which command, endpoint, test, or background process invokes
the model. Command validators implement the shared request-validator contract
and are executed by the request-validation pipeline before command transaction
handling opens a transaction.

Reusable normalization helpers should live in shared kernel, module shared
code, or a clearly named feature helper when multiple handlers/entities need
the same trimming, empty-to-null, filtering, deduplication, or ordering rule.
Avoid repeating ad hoc `Trim`, `Where`, and `Distinct` chains across command
handlers.

Direct module stores are acceptable only when the accepted feature scope keeps
the behavior small enough that a repository/query handler split would add more
ceremony than clarity.

## Testing Conventions

Tests should use substitutes for simple collaborator interactions. In-memory
fakes are reserved for stateful behavior that is central to the test and should
remain test-only. Application tests should cover host or application harness
behavior that cannot be usefully covered by pure unit tests and must stay free
of real external IO. Integration tests that verify persistence behavior should
use real external IO through the documented Testcontainers pattern.
