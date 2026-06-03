# Intermodule Communication

This template keeps module boundaries explicit. A module should own its state,
its `DbContext`, its schema, and its application behavior. Other modules should
interact with it through contracts or durable messages, not through its domain
entities, infrastructure project, EF sets, or tables.

The durable messaging topology is module-bounded at rest and connected only at
the transport/routing edge:

- each module owns its persistence context, schema, outbox rows, inbox rows,
  and dead-lettered outbox rows;
- Bondstone owns module message identities, handler registrations, command
  boundaries, source outbox writes, and receive-side inbox execution;
- transport adapters route source outbox messages to target module inboxes or
  event subscribers;
- integration-event fan-out is a routing concern, while each subscriber still
  handles the message inside its own module transaction.

## Choosing A Pattern

Use the smallest communication pattern that matches the consistency need.

### Same-Module Commands And Queries

Use Bondstone command handlers or module-local services for behavior inside one
module.

- Commands mutate one module through module-owned repositories or stores.
- Queries return provider-neutral read models.
- Bondstone command handlers rely on the module unit-of-work pipeline to run in
  one changed module context and save after successful command handling.
- Command handlers may explicitly flush the active module unit of work when
  they need database-generated values, constraint checks, or other
  mid-transaction persistence effects. A successful flush stores and clears the
  aggregate domain events captured by that flush. If the enclosing transaction
  later rolls back, retry must start from a fresh request or message-handling
  scope.
- A persistent command that is not mapped to a module persistence registration
  fails at runtime unless it is explicitly marked with
  `NonPersistentCommandAttribute`.
- Each command type has exactly one Bondstone command handler. If a command
  needs several internal reactions, keep one command handler as the application
  use-case owner and fan out to module-local services inside that handler.
- Query contracts should not save changes.

Do not wrap normal same-module command calls in durable messaging just because
they are commands. Durable messaging is for asynchronous handoff across a
reliability boundary.

### Cross-Module Synchronous Reads

Use a query contract from the target module's `.Contracts` project when one
module needs immediate read-side data from another module. This is an
intentional coupling to the target module's application API, so prefer it for
small read dependencies and avoid using contracts as a backdoor around durable
handoff. Contracts should return DTOs or read models, not EF entities,
aggregates, provider SDK types, `ClaimsPrincipal`, or Host HTTP concepts.

### Cross-Module Asynchronous Commands

Use a durable command when one module asks a specific target module to do work
later. Durable commands are acceptance-only:

- the caller receives a `CommandSubmission`
- the target handler does not return a direct response payload
- results are observed later through operation status, a read model, a query
  contract, or a follow-up integration event

Send durable commands through `IDurableCommandSender` from inside the source
module's command unit of work. The sender writes an outbox row for the source
module inferred from the active module boundary and rejects sends when no
module unit of work is active. The outbox worker later dispatches the command
through Rebus to the target module queue.

```csharp
CommandSubmission submission = durableCommandSender.Send(
    new RebuildProductProjectionCommand(productId),
    targetModule: "products",
    durableOperationId: durableOperationId);
```

Use a durable operation id when a caller or user workflow needs to observe
progress after command acceptance. This is Bondstone runtime workflow/result
identity, not a product aggregate id. Polling endpoints, websocket
notifications, or server-side waiters should observe operation/read-model state
above the durable command sender. Do not turn durable commands into synchronous
RPC by waiting for the transport handler to return a payload.

When a caller wants to wait briefly for a durable command result, use
`IDurableRequestSender` and register request/result polling with
`AddDurableRequestPolling()`. The sender sends the command with a durable
operation id and polls an `IDurableOperationReader` until the durable operation
completes, fails, is cancelled, or the caller's wait timeout expires. Timeout
only stops waiting; it does not cancel already accepted durable work. Bondstone
exposes the reader contract, but generated sample modules do not provide
durable operation storage by default. Product modules that need polling should
implement a module-owned reader or status endpoint for their durable
operation/read-model state. HTTP clients should normally receive a durable
operation id and poll an explicit status endpoint instead of depending on
transport-level request/reply.

### Cross-Module Facts

Use integration events when one module publishes a fact that zero or more
subscribers may react to independently.

Do not publish every domain event automatically. Keep domain events internal to
the source module, then add an integration event only when there is a real
consumer.

The source module maps selected domain events to integration events through an
`IIntegrationEventMapper<TDomainEvent>`. The module unit of work writes mapped
integration events to the same source module outbox as the aggregate changes.
The outbox worker publishes integration events through the configured transport
adapter; transport subscriptions decide which module queues receive them.

## Durable Message Lifecycle

Durable cross-module messages move through these stages:

1. application code changes one source module
2. the Bondstone command pipeline resolves the source module unit of work from
   module persistence registration and persists aggregate state, stored domain
   events, and outbox rows in one transaction
3. the source module's outbox worker takes its PostgreSQL advisory lock and
   claims pending rows with `FOR UPDATE SKIP LOCKED`
4. the transport adapter deserializes the stable message identity and sends or
   publishes the message
5. transport routing delivers the message to the target module queue or event
   subscribers
6. the module-scoped transport adapter opens the receiving module unit of work
7. the adapter records a target-module inbox row per message id and stable
   message identity, then delegates to the target handler
8. target handlers may call module application behavior owned by the receiving module
   inside that receiving transaction
9. the adapter commits target state and the inbox row after successful message
   handling

Duplicate transport deliveries are expected. Handlers that perform
non-idempotent work should still use product-owned idempotency keys or state
checks for business-level safety. The template ships a minimal inbox table for
receive-side duplicate suppression; it is not a full audit log or diagnostics
store.

Bondstone's durable messaging contract is at-least-once delivery. Source
module state, stored domain events, and source outbox rows commit atomically;
outbox dispatch to Rebus happens later and may be retried. The receive-side
inbox suppresses duplicate deliveries for the same stable transport message id,
message identity, and receiving module while the inbox row is retained. It does
not guarantee exactly-once business effects forever. Commands, integration
events, and handlers that need semantic deduplication should carry a durable
operation id, business idempotency key, or enough state for the receiver to
recognize already-applied work.

Outbox rows capture the current .NET `Activity` trace context in metadata.
Outbox dispatch writes standard W3C `traceparent`, `tracestate`, and `baggage`
headers to transport messages when trace metadata is available. Receive-side
transport handlers start a consumer `Activity` from those headers so follow-up
durable commands and integration events stay in the same distributed trace. The
incoming message id is added as causation baggage when it is a GUID, and the
incoming durable operation id is carried as durable operation baggage when
present. New module transactions that do not start from an incoming message
start a Bondstone `Activity`; OpenTelemetry exports that span when the
Bondstone activity source is registered.

Outbox rows store dispatch state and a bounded error summary. Full exception
details belong in structured logs and traces so the outbox table stays an
operational queue, not a diagnostics archive.

Each module persistence registration owns one outbox worker for that module's
schema. The worker takes a module-scoped PostgreSQL advisory lock before
claiming eligible rows oldest-first with `FOR UPDATE SKIP LOCKED`. Separate
module workers run independently, so slow or locked dispatch in one module does
not block another module's outbox.

Bondstone's default outbox is not a strict FIFO or per-aggregate ordered event
log. If an older message fails and is scheduled for retry, later eligible
messages from the same module may still be dispatched. Products that need
causal ordering must model it explicitly with aggregate state, process-manager
state, sequence numbers, or receiver-side version checks. A receiver that needs
ordered facts should reject, defer, or no-op out-of-order messages based on the
product-owned sequence/state rule instead of relying on table scan order.

If the Host stops while a message is claimed as `Processing`, the row is not
deleted or considered delivered. It becomes eligible again after
`Messaging:LockTimeout` when another dispatcher treats the claim as stale. A
stale claim counts as an abandoned dispatch attempt before the row is retried or
dead-lettered, so `Messaging:MaxAttempts` still bounds crash-looping dispatch
work. Choose a timeout that is comfortably longer than normal dispatch latency
but short enough for the product's restart recovery expectations.

Inbox and processed/dead-lettered outbox rows are retained until product
operations define a cleanup policy. This is intentionally deferred template
scope, not an accidental omission. Do not delete inbox rows inside the maximum
transport redelivery or manual replay window. Dead-letter replay, archival, and
cleanup are product operational decisions because retention needs depend on the
deployment, incident response workflow, and compliance expectations. Products
that add those operations should keep them explicit: replay dead-lettered rows
through a product-owned maintenance command, archive processed outbox rows only
after observability and incident windows have elapsed, and clean inbox rows only
after the duplicate-delivery window is no longer needed.

The template does not ship replay, archival, cleanup, or operational dashboard
workflows by default. Add them only with a product or framework decision that
defines retention windows, replay authorization, poison-message handling,
observability expectations, and whether replay reuses the existing message id
or creates a new causally linked message.

## Transport Boundary

The transport adapter owns routing, pub/sub delivery, and receive-side handler
dispatch. The generated template uses Rebus with PostgreSQL as the default
local transport and exposes Azure Service Bus as a broker-backed alternative.
Transport registration creates one bus, queue, and subscription startup service
per configured module, and the module-scoped transport adapter owns the
receiving module transaction because transport delivery starts outside the
Bondstone command pipeline. Transport handlers should stay thin and delegate
meaningful state changes to target-module application behavior; receiving
module command calls participate inside that transaction when they are used. Do
not call another module's write command from inside a receiving module
transaction; use another durable command or integration event for cross-module
follow-up work. The template owns both the source-side outbox and the
receive-side inbox so outgoing messages commit atomically with source module
state and duplicate deliveries are suppressed per receiving module and message
identity.

Rebus over PostgreSQL fits the local modular-monolith topology and reuses the
product database dependency, but it is a local/default transport rather than the
recommended production broker for every product. The Rebus adapter also exposes
Azure Service Bus through `UseAzureServiceBusInternalTransport`, which gives
production products a broker-backed transport path while keeping Bondstone
message contracts, outbox, inbox, route resolution, and module handler
registration unchanged. Azure Service Bus requires the Standard tier because
the Rebus transport uses topics.

When the PostgreSQL transport is used, the Migrator creates the configured
transport schema and centralized subscription table when the transport
migration scope is run. Rebus PostgreSQL transport table creation remains
adapter-owned until a product or framework decision replaces it with explicit
Migrator-owned transport DDL. Production products should run transport DDL
through Migrator or deployment migrations and make Host startup validate
transport readiness rather than creating infrastructure implicitly. External
broker transports are product decisions and should bring their own deployment
resources and verification.

Messages MUST use stable message identities, not CLR type names, for persisted
outbox rows and transport message type headers. Use
`DurableCommandIdentityAttribute` for durable commands,
`IntegrationEventIdentityAttribute` for integration events, and register
message assemblies through
`AddModuleMessaging("{module}", ...)`; explicit `IMessageTypeRegistry`
registration is still available for tests or unusual composition.
Event identities should name the fact, not the CLR type. Prefer
`{aggregate}.{event}.{version}` for integration events derived from aggregate
facts, such as `local-user.created.v1`. Durable command identities should name
caller intent, such as `{target-module}.{command}.{version}`.

Message payloads are part of the durable contract once they are written to an
outbox row. The current template uses the platform serializer directly and does
not ship message upcasting, payload migration, schema-registry integration, or
per-message serializer policies. Products should treat message contracts as
append-compatible by default: add optional fields, keep old identities readable
until retained rows and in-flight transport messages have drained, and introduce
a new message identity when a breaking payload change is required. A richer
versioning/upcasting layer is a future framework decision, not implicit
behavior.

Module infrastructure registrations should call
`AddModuleMessaging("{module}", typeof(...))` for every assembly that may
contain durable messages or module message handlers: usually the module
assembly, its `.Contracts` assembly, and its `.Infrastructure` assembly. This
scans stable message identities and registers module-scoped
`IModuleMessageHandler<T>` handlers as Bondstone module messaging metadata. The
configured transport adapter consumes that metadata to expose transport
handlers and subscriptions. Durable commands must have exactly one target-module
handler. Integration events may have multiple handlers in the same receiving
module only when each handler declares `IntegrationEventHandlerIdentityAttribute`;
Bondstone then records one inbox row per handler identity so successful handlers
are not rerun when another handler fails. If a module needs ordered internal
reactions, prefer one module message handler as the module-local event owner and
fan out to module-local services inside that handler. The inbox key uses the
stable transport message id plus the stable handler identity. Event subscribers
should also call
`AddModuleMessaging("{module}", ...)`; registering an
`IModuleMessageHandler<TEvent>` for an integration event automatically registers
the module's transport subscription for that event. Publishing an event with no
subscribers is allowed.

Module infrastructure registrations should call
`AddModulePersistence<{Module}DbContext>("{module}", ModuleCommandTypes.FromHandlerAssemblyMarkers(...))`
with marker types from assemblies that contain persistent
`IModuleCommandHandler<TCommand, TResult>` handlers for that module. The
registration maps those command types to the module DbContext. Each command
type must have exactly one registered `IModuleCommandHandler<TCommand,
TResult>`. Modules without Bondstone command handlers can call
`AddModulePersistence<{Module}DbContext>("{module}")` and run custom handlers
through `IModuleBoundary`. Both paths register the module persistence resolver
plus the module-owned unit of work, boundary executor, inbox executor, outbox
writer, and outbox worker used by durable messaging. Known command callers
should inject
`IModuleCommandExecutor<TCommand, TResult>` rather than the runtime-typed
command bus. A command type that is not mapped to any module persistence
registration fails in the Bondstone command pipeline. Mark a command with
`NonPersistentCommandAttribute` only for platform or explicitly non-persistent
work.

## Naming The Application API

Use names that describe caller intent.

- Use `IModuleCommandBus` for in-process same-module commands.
- Use `IModuleBoundary` when custom handlers need the module
  transaction and outbox behavior.
- Use `IModuleCommandExecutor<TCommand, TResult>` for known in-process module
  commands.
- Use `IModuleCommandBus` only for dynamic edges and compatibility where the
  command type is not known at compile time.
- Use `IDurableCommandSender` when the caller explicitly wants durable
  asynchronous delivery.
- If a product wants one abstraction that can choose immediate or durable
  execution, introduce that as a product application port, such as
  `ICommandSender`, and keep the immediate/durable choice explicit in its
  implementation and registration.

Do not hide the consistency model from the caller. A command sent through the
module command bus can complete in the current request; a durable command is accepted for
later handling and must be observed through state.

## Adding A Module

When adding a module:

1. add `{Module}`, `{Module}.Contracts`, and `{Module}.Infrastructure`
   projects
2. put domain and application behavior in `{Module}`
3. put provider-neutral contracts and DTOs in `{Module}.Contracts`
4. put EF Core, repositories, query implementations, and adapters in
   `{Module}.Infrastructure`
5. create `{Module}DbContext` inheriting `ModuleDbContext<{Module}DbContext>`
   or implementing `IModuleDbContext` directly
6. configure the module schema, migrations history table, and module messaging
   persistence hook for domain events, inbox, and outbox
7. call `AddModulePersistence<{Module}DbContext>("{module}", ...)`
   from module infrastructure with marker types from assemblies that contain
   persistent Bondstone command handlers for that module, or omit command
   markers for custom handlers
8. call `AddModuleMessaging("{module}", ...)` from module infrastructure for
   the module, contracts, and infrastructure assemblies
9. add module command handler assembly markers to the Host and Migrator
   `AddModuleCommands` configuration when the module has command handlers
10. add the module name to `Messaging:Modules`
11. ensure the module Infrastructure project is composed by the Migrator; module
    DbContext migrations are discovered through `AddModulePersistence`
12. add module docs and focused tests

## Adding A Durable Command

1. Define a command that implements `IDurableCommand`.
2. Put it in a contracts project unless it is truly platform-wide.
3. Add `DurableCommandIdentityAttribute` and ensure its assembly is registered
   with `AddModuleMessaging("{module}", ...)`.
4. Send it with `IDurableCommandSender` from inside the source module's command
   handler, including the target module name.
5. Implement an `IModuleMessageHandler<TCommand>` handler in the target module.
6. Keep the transport handler thin: validate transport assumptions and call a
   receiving-module command or service for state changes.
7. Ensure the receiving-module command handler's assembly is included in that
   module's persistence registration so the module pipeline commits the
   receiving module DbContext after successful command handling, or execute
   custom handlers through `IModuleBoundary`.
8. If callers need progress or results, model that as operation/read-model state
   and expose it through a query contract or endpoint.

## Adding An Integration Event

1. Keep the source domain event internal to the source module.
2. Add an `IIntegrationEvent` contract only when there is a real consumer.
3. Add `IntegrationEventIdentityAttribute` and ensure its assembly is
   registered with `AddModuleMessaging("{module}", ...)`.
4. Add an `IIntegrationEventMapper<TDomainEvent>` in the source module.
   The mapper source module must match the changed module context; integration
   events are written to that source module's outbox.
5. Implement `IModuleMessageHandler<TEvent>` handlers in subscriber modules.
6. Register subscriber module handlers with `AddModuleMessaging`; integration
   event handlers automatically add the module's transport subscription.
7. If a subscriber module has multiple handlers for the same integration event,
   add `IntegrationEventHandlerIdentityAttribute` to each handler.

Generated products should add tests for real communication workflows as those
workflows appear.
