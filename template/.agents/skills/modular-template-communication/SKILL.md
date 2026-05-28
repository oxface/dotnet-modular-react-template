---
name: modular-template-communication
description: Add or change intermodule communication in the generated modular monolith. Use for cross-module query contracts, durable commands, integration events, message type registration, event mappers, durable command handlers, integration event handlers, operation-status flows, outbox/inbox tests, or Rebus transport-adjacent changes.
---

# Modular Template Communication

Use this skill to choose and scaffold cross-module communication without hiding
the consistency model.

## Read First

Before editing, read:

- `AGENTS.md`
- `docs/governance.md`
- `docs/architecture/server.md`
- `docs/architecture/intermodule-communication.md`
- `docs/modules/README.md`
- `openspec/config.yaml`

If the change adds product behavior, durable messaging behavior, orchestration,
persistence, APIs, or runtime infrastructure, confirm there is accepted
OpenSpec scope or a durable architecture decision.

## Pick The Pattern

Use this decision order:

1. Same-module command or query: use Mediator or a module-local service.
2. Cross-module synchronous read: add a query contract to the target module's
   `.Contracts` project.
3. Cross-module asynchronous request to a specific module: add a durable
   command and handler.
4. Cross-module fact consumed by zero or more modules: add an integration event,
   mapper, subscriber registration, and handler.
5. Long-running observable workflow: include an `OperationId` and expose status
   through operation/read-model state and query contracts.

Do not use durable commands as request/response APIs. A durable command returns
acceptance only; results are observed later through state.

## Add A Query Contract

1. Put the interface and DTOs in the target module's `.Contracts` project.
2. Keep DTOs provider-neutral.
3. Implement the contract in the target module Infrastructure project or module
   application layer, depending on local patterns.
4. Register the implementation in the target infrastructure/module
   configuration.
5. Inject only the contract into callers.

## Add A Durable Command

1. Define a command implementing `IDurableCommand`.
2. Put it in a module Contracts project unless it is truly platform-wide.
3. Register the command CLR type with a stable message type name in
   `IMessageTypeRegistry`.
4. Submit through `IDurableCommandSubmitter` with `SourceModule` and
   `TargetModule`.
5. Include `OperationId` when a caller needs later progress/result observation.
6. Implement `IDurableCommandHandler<TCommand>` in the target module.
7. Keep the handler return type as `Task`, not `Task<T>`.
8. Make the handler mutate only the target module context. If it needs more
   cross-module work, submit another durable command or publish an integration
   event from its own module transaction.

Command shape:

```csharp
public sealed record RebuildCatalogProjection(Guid OperationId)
    : IDurableCommand;
```

Submission shape:

```csharp
CommandSubmission submission = durableCommandSubmitter.Submit(
    new RebuildCatalogProjection(operationId),
    new DurableCommandSubmissionOptions(
        SourceModule: "identity",
        TargetModule: "catalog",
        OperationId: operationId));
```

Handler shape:

```csharp
public sealed class RebuildCatalogProjectionHandler
    : IDurableCommandHandler<RebuildCatalogProjection>
{
    public Task HandleAsync(
        RebuildCatalogProjection command,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## Add An Integration Event

1. Keep the source domain event internal to the source module.
2. Add an `IIntegrationEvent` only when a real subscriber exists.
3. Put the event contract where consumers can reference it without depending on
   source-module internals.
4. Register the event CLR type with a stable message type name.
5. Add an `IIntegrationEventMapper<TDomainEvent>` in the source module.
6. Register subscribers through `ILocalSubscriptionRegistry`.
7. Implement `IIntegrationEventHandler<TEvent>` in each subscriber module.

Event names should be stable and versioned, for example:

```text
ModularTemplate.catalog.item-created.v1
```

Do not rename persisted message type names casually. Add a `v2` message for
incompatible payload changes and keep old handlers while old messages may still
exist.

## Tests

Use small communication-pattern tests for architectural intent, and
PostgreSQL-backed tests for durability.

Cover these when relevant:

- query contract caller uses only the target module contract
- durable command submission writes to the source module outbox
- durable command returns acceptance only
- operation id reaches `MessageContext`
- handler success commits target state and inbox status together
- handler failure rolls back target state and records retry/dead-letter status
- integration event with no subscribers is marked processed
- integration event with subscribers creates one inbox row per subscriber
- duplicate transport delivery is idempotent at the inbox boundary

## Rebus Boundary

Business modules should not depend on Rebus directly. Use
`IDurableCommandSubmitter`, `IDurableCommandHandler<T>`,
`IIntegrationEventMapper<TDomainEvent>`, and `IIntegrationEventHandler<TEvent>`.

Rebus-specific changes belong in shared Infrastructure/Transport. Treat new
Rebus features such as sagas, native pub/sub subscription management,
request/reply, deferred delivery, or data bus as separate architecture changes,
not incidental scaffolding.
