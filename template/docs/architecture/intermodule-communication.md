# Intermodule Communication

This template keeps module boundaries explicit. A module should own its state,
its `DbContext`, its schema, and its application behavior. Other modules should
interact with it through contracts or durable messages, not through its domain
entities, infrastructure project, EF sets, or tables.

## Choosing A Pattern

Use the smallest communication pattern that matches the consistency need.

### Same-Module Commands And Queries

Use Mediator command/query handlers for behavior inside one module.

- Commands mutate one module through module-owned repositories or stores.
- Queries return provider-neutral read models.
- Command handlers rely on the Mediator module unit-of-work pipeline to save
  one changed module context.
- Query handlers should not save changes.

Do not wrap normal same-module Mediator calls in durable messaging just because
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

Send durable commands through `IDurableCommandSender`. The sender
writes an outbox row for the source module. The outbox worker later dispatches
the command through Rebus to the target module queue.

```csharp
CommandSubmission submission = durableCommandSender.Send(
    new RebuildOperationProjectionCommand(operationId),
    new DurableCommandSubmissionOptions(
        SourceModule: "identity",
        TargetModule: "operations",
        OperationId: operationId));
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
2. the Mediator command pipeline resolves the source module unit of work from
   module persistence registration and persists aggregate state, stored domain
   events, and outbox rows in one transaction
3. `OutboxDispatcher` claims pending source-module outbox rows with
   `FOR UPDATE SKIP LOCKED`
4. `RebusOutboxTransport` deserializes the stable message identity and sends or
   publishes the message through Rebus
5. Rebus delivers the message to the target module queue or event subscribers
6. Rebus handlers adapt the transport message to target-module Mediator
   commands
7. the Mediator command pipeline resolves the target module unit of work and
   commits target state after successful command handling

Duplicate transport deliveries are expected. Handlers that perform
non-idempotent work should use product-owned idempotency keys or state checks.
The template no longer ships a custom inbox table; add one only when a product
needs durable receive-side audit or deduplication beyond Rebus delivery and
handler-level idempotency.

Outbox rows store dispatch state and a bounded error summary. Full exception
details belong in structured logs and traces so the outbox table stays an
operational queue, not a diagnostics archive.

## Rebus Boundary

Rebus owns transport, routing, pub/sub delivery, and receive-side handler
dispatch. Rebus handlers should stay thin and delegate meaningful state changes
to target-module Mediator commands. Persistence is committed by the module
command pipeline, not by a Rebus pipeline step. The template owns only the
source-side outbox so outgoing messages commit atomically with module state.

The default generated transport is Rebus over PostgreSQL, which fits the local
modular-monolith topology and reuses the product database dependency. In-memory
transport is reserved for tests that intentionally avoid PostgreSQL-backed
transport dependencies. External broker transports are product decisions and
should bring their own deployment resources and verification.

Messages MUST use stable message identities, not CLR type names, for persisted
outbox rows. Use `MessageIdentityAttribute` and register message assemblies
through `AddMessagingAssembly<TMarker>()`; explicit `IMessageTypeRegistry`
registration is still available for tests or unusual composition.

Module infrastructure registrations should call `AddMessagingAssembly<TMarker>()`
for every assembly that may contain durable messages or Rebus handlers: usually
the module assembly, its `.Contracts` assembly, and its `.Infrastructure`
assembly. This scans stable message identities and registers Rebus
`IHandleMessages<T>` handlers. Event subscribers should also call
`AddModuleEventSubscriptions("module", typeof(SomeIntegrationEvent))` so Rebus
subscriptions are deterministic at startup.

Module infrastructure registrations should call
`AddModulePersistence<{Module}DbContext>("{module}", typeof(CommandMarker))`
with marker types from assemblies that contain commands mutating that module.
The Mediator unit-of-work pipeline uses those registrations to select exactly
one module DbContext for each command. A command type that is not mapped to any
module persistence registration runs without an automatic module save; reserve
that for platform commands or explicitly non-persistent work.

## Naming The Application API

Use names that describe caller intent.

- Use Mediator directly for in-process same-module commands and queries.
- Use `IDurableCommandSender` when the caller explicitly wants durable
  asynchronous delivery.
- If a product wants one abstraction that can choose immediate or durable
  execution, introduce that as a product application port, such as
  `ICommandSender`, and keep the immediate/durable choice explicit in its
  implementation and registration.

Do not hide the consistency model from the caller. A command sent through
Mediator can complete in the current request; a durable command is accepted for
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
   `ApplyOutboxConfiguration("{module}")`
7. register the context as `IModuleDbContext`
8. register `IOutboxWriter` as `OutboxWriter<{Module}DbContext>`
9. call `AddModulePersistence<{Module}DbContext>("{module}", ...)` from module
   infrastructure for assemblies that contain commands mutating that module
10. call `AddMessagingAssembly<TMarker>()` from module infrastructure for the
    module, contracts, and infrastructure assemblies
11. add the module name to `Messaging:Modules`
12. add the module context to the Migrator
13. add module docs and focused tests

## Adding A Durable Command

1. Define a command that implements `IDurableCommand`.
2. Put it in a contracts project unless it is truly platform-wide.
3. Add `MessageIdentityAttribute` and ensure its assembly is registered with
   `AddMessagingAssembly<TMarker>()`.
4. Send it with `IDurableCommandSender`, including source and target
   module names.
5. Implement a Rebus `IHandleMessages<TCommand>` handler in the target module.
6. Keep the Rebus handler thin: validate transport assumptions and call a
   target-module Mediator command for state changes.
7. Register that target command assembly with the target module's
   `AddModulePersistence` call so the Mediator pipeline commits the target
   module DbContext after successful command handling.
8. If callers need progress or results, model that as operation/read-model state
   and expose it through a query contract or endpoint.

## Adding An Integration Event

1. Keep the source domain event internal to the source module.
2. Add an `IIntegrationEvent` contract only when there is a real consumer.
3. Add `MessageIdentityAttribute` and ensure its assembly is registered with
   `AddMessagingAssembly<TMarker>()`.
4. Add an `IIntegrationEventMapper<TDomainEvent>` in the source module.
   The mapper source module must match the changed module context; integration
   events are written to that source module's outbox.
5. Implement Rebus `IHandleMessages<TEvent>` handlers in subscriber modules.
6. Subscribe module endpoints with `AddModuleEventSubscriptions`.

Generated products should add tests for real communication workflows as those
workflows appear.
