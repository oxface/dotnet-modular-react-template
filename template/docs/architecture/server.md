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
- The Migrator references module Infrastructure projects, bootstraps transport
  infrastructure when requested, and migrates module DbContexts discovered from
  module persistence registrations. Product-owned schema changes should add
  product-owned migrations after bootstrap.
- SharedKernel contains dependency-light domain primitives, validation
  contracts, and normalization helpers shared across module libraries.
- Bondstone contains module-boundary abstractions, command handling, messaging
  contracts, module message registration, and EF Core persistence plumbing. Its
  PostgreSQL and Rebus integrations are adapters; module Infrastructure
  projects should register Bondstone concepts instead of taking a direct
  dependency on a transport adapter unless they own adapter-specific behavior.
- Domain events and outbox messages are persisted in each module schema by
  Bondstone.EntityFrameworkCore, with PostgreSQL-specific locking and duplicate
  detection supplied by Bondstone.EntityFrameworkCore.Postgres.
- Inbox message records are persisted in each module schema so receive-side
  handlers can skip duplicate deliveries by message id and stable message
  identity.
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
be delegated to module-owned configuration extensions, ideally one
`Add{Module}Module` service extension and one `Map{Module}Module` endpoint
extension where the module owns endpoints. Those extensions wire module
services, infrastructure adapters, module persistence, and Bondstone module
messaging. Host composition wires transport adapters, such as Rebus, around the
Bondstone registrations; modules should not register transport handlers
directly. The generated Host uses Rebus/PostgreSQL by default for local
simplicity, while the Rebus adapter also exposes Azure Service Bus for products
that want broker-backed transport without changing module contracts or
Bondstone message handlers.

## DDD And CQRS Direction

Domain objects with identity and behavior should use explicit terminology:
aggregate root, child entity, value object, repository, query handler, command
handler, DTO, or artifact payload. Records are a C# representation choice, not
automatic domain value objects. A record is a value object only when the
feature docs and code model its invariants, equality, lifecycle, and ownership
as a domain concept.

The template's durable direction is CQRS through narrow module contracts and
Bondstone command handlers where behavior grows beyond a very small module
service. Repository abstractions represent command-side domain
persistence for aggregate loading and saving. They sit inside the module
boundary, and infrastructure implements them through module-owned DbContexts.
Query contracts and read models provide provider-neutral projections for callers
that need immediate read-side data. They are cross-module application ports, not
a permission to share persistence details or perform cross-module writes. Avoid
`Reader` services unless a feature artifact intentionally documents why that
term is clearer than a query contract, read model, or repository.

Modules should use Bondstone command abstractions for write use cases that need
module unit-of-work behavior. Command handlers mutate aggregates through
module-owned repositories and rely on the Bondstone command pipeline to run the
command inside the selected module unit of work and save changes after
successful command handling. Normal callers should inject
`IModuleCommandExecutor<TCommand, TResult>` for known command types; the
runtime-typed `IModuleCommandBus` exists for dynamic edges and compatibility.
Each command type has exactly one registered command handler.
Command handlers should not call `DbContext.SaveChanges` directly. Handlers may
explicitly flush the active module unit of work when they need
database-generated values, constraint checks, stored-procedure inputs, or other
mid-transaction persistence effects. Each successful flush stores and clears
the aggregate domain events captured by that flush; if the enclosing
transaction later rolls back, the scoped DbContext and tracked aggregates must
not be reused for retry. A retry starts from a fresh request or
message-handling scope, and the module unit of work rejects further save or
transaction attempts after a failed save or transaction. The unit of work uses
the current .NET `Activity` for trace correlation when one exists; otherwise it
starts a Bondstone `Activity` for the module transaction.
Integration-event outbox rows persist the current W3C trace context in outbox
metadata so later dispatch and receive-side work continue the same distributed
trace. Module Infrastructure registers persistent command types with
`AddModulePersistence<{Module}DbContext>()`; modules can discover those command
types from handler assemblies with
`ModuleCommandTypes.FromHandlerAssemblyMarkers`. The pipeline uses the
discovered command types to select one module boundary for the command. Modules
that do not use Bondstone command handlers can also register persistence with
`AddModulePersistence` and run arbitrary module work through `IModuleBoundary`.
Both paths register the shared module DbContext/outbox plumbing used by durable
messaging. The Host starts durable outbox workers once with
`AddModuleOutboxDispatchers()` after composing modules; persistence
registration alone should not start background dispatch, so Migrator and other
tooling can reuse module persistence without draining queues. Commands that are not mapped to
module persistence fail unless they are explicitly marked with
`NonPersistentCommandAttribute`. Query contracts read provider-neutral state and
do not save changes.

Command handlers should get decision-making data through repositories or
command-side read ports, not by calling external query contracts. Query
contracts are application read use cases for callers that need read-side data.

Cross-module synchronous reads may use in-process query contracts from the
target module's Contracts project when the caller truly needs immediate state
and accepts in-process coupling to that target module. Cross-module writes,
eventual work, and reliability-boundary handoff should use durable commands or
integration events through the outbox/transport pipeline.
Durable commands are send-and-forget: callers may receive an acceptance record
and durable operation id, but handler results are observed later through query
contracts, operation status, read models, or follow-up integration events.
Module code should send durable commands through `IDurableCommandSender` from
inside the source module's command unit of work so the source module outbox row
is committed consistently with module state.
Receive-side transport handlers should be adapters that delegate state changes
to target-module application behavior. Because transport delivery starts outside
the Bondstone command pipeline, the module-scoped transport adapter owns the
receiving module transaction; receiving module command calls participate inside
that transaction when they are used. Cross-module write follow-up from a receive
handler must use another durable command or integration event, not a synchronous
command for another module.
See [Intermodule Communication](intermodule-communication.md) for the detailed
pattern guide, message lifecycle, ordering, retention, and module scaffolding
checklist.
Host-level orchestration is reserved for API/user workflows that compose module
contracts without owning durable state changes. Durable commands still need a
source module unit of work; Host workflows that need durable handoff should
delegate to a module-owned application command or an explicitly introduced
orchestration module with its own persistence. Module-owned orchestration should
stay inside the owning module.
Generated products should cover real communication workflows with product tests.
Template-framework infrastructure behavior is covered by the factory root test
project, outside the generated-product payload.

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
handling opens a transaction. Bondstone command diagnostics run as a pipeline
behavior so command timing and failures are captured consistently around
validation, transaction handling, and the command handler.

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
