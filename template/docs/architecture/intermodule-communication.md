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
- Command handlers rely on the module unit-of-work pipeline to save one changed
  module context.
- Query handlers should not save changes.

Do not wrap normal same-module Mediator calls in durable messaging just because
they are commands. Durable messaging is for asynchronous handoff across a
reliability boundary.

### Cross-Module Synchronous Reads

Use a query contract from the target module's `.Contracts` project when one
module needs immediate read-side data from another module.

The contract should return DTOs or read models, not EF entities, aggregates,
provider SDK types, `ClaimsPrincipal`, or Host HTTP concepts.

Example shape:

```csharp
public interface IOperationsQueries
{
    Task<OperationDetails?> GetOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken);
}
```

The calling module depends on the target module contract, and the Host wires the
implementation.

### Cross-Module Asynchronous Commands

Use a durable command when one module asks a specific target module to do work
later.

Durable commands are acceptance-only:

- the caller receives a `CommandSubmission`
- the target handler returns `Task`, not `Task<T>`
- results are observed later through operation status, a read model, a query
  contract, or a follow-up integration event

Submit durable commands through `IDurableCommandSubmitter`. The submitter writes
an outbox row for the source module. Delivery and handling happen later through
the outbox, Rebus transport, and inbox processor.

```csharp
CommandSubmission submission = durableCommandSubmitter.Submit(
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

Subscribers register through `ILocalSubscriptionRegistry` and implement
`IIntegrationEventHandler<TEvent>`.

## Durable Message Lifecycle

Durable cross-module messages move through these stages:

1. application code changes one source module
2. the source module unit of work persists aggregate state, stored domain
   events, and outbox rows in one transaction
3. `OutboxDispatcher` claims pending source-module outbox rows with
   `FOR UPDATE SKIP LOCKED`
4. `RebusOutboxTransport` sends a `DurableTransportEnvelope`
5. `RebusDurableTransportHandler` writes one target-module inbox row
6. `InboxProcessor` claims target-module inbox rows with
   `FOR UPDATE SKIP LOCKED`
7. the typed durable command or integration event handler runs inside the
   target module scope
8. handler changes and `Processed` inbox status commit together

If a handler fails after mutating target state, the message transaction rolls
back those handler changes. The inbox row is then reloaded and marked failed or
dead-lettered separately.

Duplicate transport deliveries are expected. The inbox table has a uniqueness
constraint on message id and target module, and duplicate Rebus envelopes are
treated as already received.

## Rebus Boundary

Rebus is used as the transport bridge between outbox dispatch and inbox
persistence. The template intentionally keeps business modules behind template
messaging contracts instead of exposing Rebus directly to module code.

This gives the generated product:

- in-memory transport for tests and local development when configured
- Azure Service Bus transport for deployed environments
- a single broker envelope type for durable intermodule delivery
- at-least-once delivery with inbox idempotency at the module boundary

The template does not currently use every Rebus feature. In particular, it does
not expose Rebus sagas, request/reply, native pub/sub subscriptions,
deferred-message APIs, data bus, or Rebus' SQL Server outbox. Add those only
when a product workflow needs them and after an accepted OpenSpec change or
durable architecture decision.

## Naming The Application API

Use names that describe caller intent.

- Use Mediator directly for in-process same-module commands and queries.
- Use `IDurableCommandSubmitter` when the caller explicitly wants durable
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
9. add the module context to the Migrator
10. add module docs and focused tests

## Adding A Durable Command

1. Define a command that implements `IDurableCommand`.
2. Put it in a contracts project unless it is truly platform-wide.
3. Register the CLR type with a stable message type name in
   `IMessageTypeRegistry`.
4. Submit it with `IDurableCommandSubmitter`, including source and target
   module names.
5. Implement `IDurableCommandHandler<TCommand>` in the target module.
6. If callers need progress or results, model that as operation/read-model state
   and expose it through a query contract or endpoint.

## Adding An Integration Event

1. Keep the source domain event internal to the source module.
2. Add an `IIntegrationEvent` contract only when there is a real consumer.
3. Register the event with a stable message type name.
4. Add an `IIntegrationEventMapper<TDomainEvent>` in the source module.
5. Register subscribers through `ILocalSubscriptionRegistry`.
6. Implement `IIntegrationEventHandler<TEvent>` in subscriber modules.

Generated products should add tests for real communication workflows as those
workflows appear. The template factory keeps small pattern examples in its root
framework test project so generated products are not seeded with fake workflow
tests.
