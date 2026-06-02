# Intermodule Communication

This template keeps module boundaries explicit. A module should own its state,
its `DbContext`, its schema, and its application behavior. Other modules should
interact with it through contracts or durable messages, not through its domain
entities, infrastructure project, EF sets, or tables.

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
- Query contracts should not save changes.

Do not wrap normal same-module command calls in durable messaging just because
they are commands. Durable messaging is for asynchronous handoff across a
reliability boundary.

### Cross-Module Synchronous Reads

Use a query contract from the target module's `.Contracts` project when one
module needs immediate read-side data from another module. Contracts should
return DTOs or read models, not EF entities, aggregates, provider SDK types,
`ClaimsPrincipal`, or Host HTTP concepts.

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
    new RebuildOperationProjectionCommand(operationId),
    targetModule: "operations",
    operationId: operationId);
```

Use `OperationId` when a caller or user workflow needs to observe progress
after command acceptance.

### Cross-Module Facts

Use integration events when one module publishes a fact that zero or more
subscribers may react to independently.

Do not publish every domain event automatically. Keep domain events internal to
the source module, then add an integration event only when there is a real
consumer.

The source module maps selected domain events to integration events through an
`IIntegrationEventMapper<TDomainEvent>`. The module unit of work writes mapped
integration events to the same source module outbox as the aggregate changes.
The outbox worker publishes integration events through Rebus; Rebus
subscriptions decide which module queues receive them.

## Durable Message Lifecycle

Durable cross-module messages move through these stages:

1. application code changes one source module
2. the Bondstone command pipeline resolves the source module unit of work from
   module persistence registration and persists aggregate state, stored domain
   events, and outbox rows in one transaction
3. the source module's outbox worker takes its PostgreSQL advisory lock and
   claims pending rows with `FOR UPDATE SKIP LOCKED`
4. `RebusOutboxTransport` deserializes the stable message identity and sends or
   publishes the message through Rebus
5. Rebus delivers the message to the target module queue or event subscribers
6. the module-scoped Rebus adapter opens the receiving module unit of work
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
events, and handlers that need semantic deduplication should carry an
operation id, business idempotency key, or enough state for the receiver to
recognize already-applied work.

Outbox rows capture the current .NET `Activity` trace context in metadata.
Outbox dispatch writes standard W3C `traceparent`, `tracestate`, and `baggage`
headers to Rebus messages when trace metadata is available. Receive-side Rebus
handlers start a consumer `Activity` from those headers so follow-up durable
commands and integration events stay in the same distributed trace. The
incoming message id is added as causation baggage when it is a GUID, and the
incoming operation id is carried as operation baggage when present. New module
transactions that do not start from an incoming message start a Bondstone
`Activity`; OpenTelemetry exports that span when the Bondstone activity source
is registered.

Outbox rows store dispatch state and a bounded error summary. Full exception
details belong in structured logs and traces so the outbox table stays an
operational queue, not a diagnostics archive.

Each module persistence registration owns one outbox worker for that module's
schema. The worker takes a module-scoped PostgreSQL advisory lock before
claiming eligible rows oldest-first with `FOR UPDATE SKIP LOCKED`. Separate
module workers run independently, so slow or locked dispatch in one module does
not block another module's outbox. It is not a strict FIFO guarantee: if an
older message fails and is scheduled for retry, later eligible messages from
the same module may still be dispatched. Products that need causal ordering
should model that explicitly with aggregate state, sequence numbers, or
product-owned process managers.

If the Host stops while a message is claimed as `Processing`, the row is not
deleted or considered delivered. It becomes eligible again after
`Messaging:LockTimeout` when another dispatcher treats the claim as stale. A
stale claim counts as an abandoned dispatch attempt before the row is retried or
dead-lettered, so `Messaging:MaxAttempts` still bounds crash-looping dispatch
work. Choose a timeout that is comfortably longer than normal dispatch latency
but short enough for the product's restart recovery expectations.

Inbox and processed/dead-lettered outbox rows are retained until product
operations define a cleanup policy. Do not delete inbox rows inside the maximum
transport redelivery or manual replay window. Dead-letter replay, archival, and
cleanup are product operational decisions because retention needs depend on the
deployment, incident response workflow, and compliance expectations. Products
that add those operations should keep them explicit: replay dead-lettered rows
through a product-owned maintenance command, archive processed outbox rows only
after observability and incident windows have elapsed, and clean inbox rows only
after the duplicate-delivery window is no longer needed.
The template does not ship those replay, archival, or cleanup workflows by
default.

## Rebus Boundary

Rebus owns transport, routing, pub/sub delivery, and receive-side handler
dispatch. Transport registration creates one bus, queue, and subscription
startup service per configured module, and the module-scoped Rebus adapter owns
the receiving module transaction because transport delivery starts outside the
Bondstone command pipeline.
Rebus handlers should stay thin and delegate meaningful state changes to
target-module application behavior; receiving module command calls participate
inside that transaction when they are used. Do not call another module's write
command from inside a receiving module transaction; use another durable command
or integration event for cross-module follow-up work. The template owns both the
source-side outbox and the receive-side inbox so outgoing messages commit
atomically with source module state and duplicate deliveries are suppressed per
receiving module and message identity.

The generated transport is Rebus over PostgreSQL, which fits the local
modular-monolith topology and reuses the product database dependency. The
Migrator creates the configured transport schema; Host startup does not run
transport DDL. External broker transports are product decisions and should bring
their own deployment resources and verification.

Messages MUST use stable message identities, not CLR type names, for persisted
outbox rows and Rebus message type headers. Use `MessageIdentityAttribute` and
register message assemblies through `AddModuleMessaging("{module}", ...)`;
explicit `IMessageTypeRegistry` registration is still available for tests or
unusual composition.
Event identities should name the fact, not the CLR type. Prefer
`{aggregate}.{event}.{version}` for integration events derived from aggregate
facts, such as `local-user.created.v1`. Durable command identities should name
caller intent, such as `{target-module}.{command}.{version}`.

Module infrastructure registrations should call
`AddModuleMessaging("{module}", typeof(...))` for every assembly that may
contain durable messages or module message handlers: usually the module
assembly, its `.Contracts` assembly, and its `.Infrastructure` assembly. This
scans stable message identities and registers module-scoped
`IModuleMessageHandler<T>` handlers behind a Rebus adapter. Each receiving
module should register one module message handler per message identity. If a
module needs several internal reactions to the same event, keep the Rebus handler
as the single transport adapter and fan out to module-local services inside that
handler. This is an intentional template simplification: internal reactions
share the receiving module transaction, retry, and failure outcome. Products
that need independent retry loops or transactions for multiple reactions should
introduce separate product handlers or a richer message-runtime decision before
adding that behavior. The inbox key uses the stable transport message id plus
the stable message identity. Event subscribers should also call
`AddModuleMessaging("{module}", ...)`; registering an
`IModuleMessageHandler<TEvent>` for an integration event automatically registers
the module's Rebus subscription for that event. Publishing an event with no
subscribers is allowed.

Module infrastructure registrations should call
`AddModulePersistence<{Module}DbContext>("{module}", ModuleCommandTypes.FromHandlerAssemblyMarkers(...))`
with marker types from assemblies that contain persistent
`IModuleCommandHandler<TCommand, TResult>` handlers for that module. The
registration maps those command types to the module DbContext. Modules without
Bondstone command handlers can call `AddModulePersistence<{Module}DbContext>("{module}")`
and run custom handlers through `IModuleBoundary`. Both paths register the
module persistence resolver plus the module-owned unit of work, boundary
executor, inbox executor, outbox writer, and outbox worker used by durable
messaging. A command type that is not mapped to any module persistence
registration fails in the Bondstone command pipeline. Mark a command with
`NonPersistentCommandAttribute` only for platform or explicitly non-persistent
work.

## Naming The Application API

Use names that describe caller intent.

- Use `IModuleCommandBus` for in-process same-module commands.
- Use `IModuleBoundary` when custom handlers need the module
  transaction and outbox behavior.
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
5. create `{Module}DbContext` implementing `IModuleDbContext`
6. configure the module schema, migrations history table, and
   `ApplyOutboxConfiguration("{module}")` for domain events, inbox, and outbox
7. call `AddModulePersistence<{Module}DbContext>("{module}", ...)`
   from module infrastructure with marker types from assemblies that contain
   persistent Bondstone command handlers for that module, or omit command
   markers for custom handlers
8. call `AddModuleMessaging("{module}", ...)` from module infrastructure for
   the module, contracts, and infrastructure assemblies
9. add module command handler assembly markers to the Host and Migrator
   `AddModuleCommands` configuration when the module has command handlers
10. add the module name to `Messaging:Modules`
11. add the module context to the Migrator
12. add module docs and focused tests

## Adding A Durable Command

1. Define a command that implements `IDurableCommand`.
2. Put it in a contracts project unless it is truly platform-wide.
3. Add `MessageIdentityAttribute` and ensure its assembly is registered with
   `AddModuleMessaging("{module}", ...)`.
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
3. Add `MessageIdentityAttribute` and ensure its assembly is registered with
   `AddModuleMessaging("{module}", ...)`.
4. Add an `IIntegrationEventMapper<TDomainEvent>` in the source module.
   The mapper source module must match the changed module context; integration
   events are written to that source module's outbox.
5. Implement `IModuleMessageHandler<TEvent>` handlers in subscriber modules.
6. Register subscriber module handlers with `AddModuleMessaging`; integration
   event handlers automatically add the module's Rebus subscription.

Generated products should add tests for real communication workflows as those
workflows appear.
