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

Send durable commands through `IDurableCommandSender` from inside the source
module's command unit of work. The sender writes an outbox row for the source
module and rejects sends when `SourceModule` does not match the active module
unit of work. The outbox worker later dispatches the command through Rebus to
the target module queue.

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
3. `OutboxDispatcher` takes a per-module PostgreSQL advisory lock and claims
   pending source-module outbox rows with `FOR UPDATE SKIP LOCKED`
4. `RebusOutboxTransport` deserializes the stable message identity and sends or
   publishes the message through Rebus
5. Rebus delivers the message to the target module queue or event subscribers
6. the module-scoped Rebus adapter opens the receiving module unit of work
7. the adapter records a target-module inbox row per message id and stable
   message identity, then delegates to the target handler
8. target handlers may call Mediator for behavior owned by the receiving module
   inside that receiving transaction
9. the adapter commits target state and the inbox row after successful message
   handling

Duplicate transport deliveries are expected. Handlers that perform
non-idempotent work should still use product-owned idempotency keys or state
checks for business-level safety. The template ships a minimal inbox table for
receive-side duplicate suppression; it is not a full audit log or diagnostics
store.

Outbox rows store dispatch state and a bounded error summary. Full exception
details belong in structured logs and traces so the outbox table stays an
operational queue, not a diagnostics archive.

The outbox dispatcher takes a per-module PostgreSQL advisory lock before
claiming rows. This keeps one Host instance at a time polling a module's outbox
and claims eligible rows oldest-first while still allowing different modules to
dispatch independently. It is not a strict FIFO guarantee: if an older message
fails and is scheduled for retry, later eligible messages from the same module
may still be dispatched. Products that need causal ordering should model that
explicitly with aggregate state, sequence numbers, or product-owned process
managers.

If the Host stops while a message is claimed as `Processing`, the row is not
deleted or considered delivered. It becomes eligible again after
`Messaging:LockTimeout` when another dispatcher treats the claim as stale.
Choose a timeout that is comfortably longer than normal dispatch latency but
short enough for the product's restart recovery expectations.

Inbox and processed/dead-lettered outbox rows are retained until product
operations define a cleanup policy. Do not delete inbox rows inside the maximum
transport redelivery or manual replay window. Dead-letter replay, archival, and
cleanup are product operational decisions because retention needs depend on the
deployment, incident response workflow, and compliance expectations. Products
that add those operations should keep them explicit: replay dead-lettered rows
through a product-owned maintenance command, archive processed outbox rows only
after observability and incident windows have elapsed, and clean inbox rows only
after the duplicate-delivery window is no longer needed.

## Rebus Boundary

Rebus owns transport, routing, pub/sub delivery, and receive-side handler
dispatch. Rebus handlers should stay thin and delegate meaningful state changes
to target-module application behavior. The module-scoped Rebus adapter owns the
receiving module transaction because transport delivery starts outside the
Mediator pipeline; receiving module Mediator calls participate inside that
transaction when they are used. Do not call another module's write command from
inside a receiving module transaction; use another durable command or
integration event for cross-module follow-up work. The template owns both the
source-side outbox and the
receive-side inbox so outgoing messages commit atomically with source module
state and duplicate deliveries are suppressed per receiving module and message
identity.

The generated transport is Rebus over PostgreSQL, which fits the local
modular-monolith topology and reuses the product database dependency. External
broker transports are product decisions and should bring their own deployment
resources and verification.

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
`AddModuleEventSubscriptions("module", typeof(SomeIntegrationEvent))` so Rebus
subscriptions are deterministic at startup.

Module infrastructure registrations should call
`AddModulePersistence<{Module}DbContext>("{module}", typeof(SomeCommandHandler))`
with marker types from assemblies that contain persistent Mediator command
handlers for that module. The registration scans `ICommandHandler` types and
maps their command message types to the module DbContext. It also registers the
module persistence resolver, module unit of work, DbContext adapter, and outbox
writer plumbing used by durable messaging. A module with no persistent Mediator
commands can call `AddModulePersistence` with no handler markers. A command type
that is not mapped to any module persistence registration runs without an
automatic module save; reserve that for platform commands or explicitly
non-persistent work.

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
   `ApplyOutboxConfiguration("{module}")` for domain events, inbox, and outbox
7. call `AddModulePersistence<{Module}DbContext>("{module}", ...)` from module
   infrastructure with marker types from assemblies that contain persistent
   Mediator command handlers for that module
8. call `AddModuleMessaging("{module}", ...)` from module infrastructure for
   the module, contracts, and infrastructure assemblies
9. add module Mediator handler assembly markers to the Host and Migrator
   compile-time Mediator configuration when the module has Mediator handlers
10. add the module name to `Messaging:Modules`
11. add the module context to the Migrator
12. add module docs and focused tests

## Adding A Durable Command

1. Define a command that implements `IDurableCommand`.
2. Put it in a contracts project unless it is truly platform-wide.
3. Add `MessageIdentityAttribute` and ensure its assembly is registered with
   `AddModuleMessaging("{module}", ...)`.
4. Send it with `IDurableCommandSender` from inside the source module's
   Mediator command handler, including source and target module names.
5. Implement an `IModuleMessageHandler<TCommand>` handler in the target module.
6. Keep the transport handler thin: validate transport assumptions and call a
   receiving-module Mediator command for state changes.
7. Ensure the receiving-module Mediator command handler's assembly is included
   in that module's `AddModulePersistence` call so the Mediator pipeline commits
   the receiving module DbContext after successful command handling.
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
6. Subscribe module queues with `AddModuleEventSubscriptions`.

Generated products should add tests for real communication workflows as those
workflows appear.
