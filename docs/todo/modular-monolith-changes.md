# Modular Monolith Migration Runbook

## Status

This document is now a migration runbook based on the current template payload.
It records the modular-monolith shape that was implemented after the initial
planning notes, so the same approach can be applied to an existing product
repository.

The template now has:

- module-owned `DbContext` types, PostgreSQL schemas, and EF migrations
- outbox, inbox, and persisted domain-event tables inside each module schema
- shared infrastructure folders for persistence, outbox, and transport
- module-local outbox writers selected by source module
- an acceptance-only durable command submitter
- durable outbox dispatch and inbox processing workers
- Rebus transport with in-memory and Azure Service Bus modes
- Rebus inbox writes with duplicate-envelope protection
- startup validation for Azure Service Bus transport
- an Operations module and query contract example
- executable examples for in-process queries, durable commands, choreography,
  module-owned orchestration, and host-level orchestration
- unit-of-work enforcement that rejects direct multi-module persistence

Product-facing docs under `template/docs/` should become the durable reference
for generated repositories. This file is a maintainer handoff/runbook.

## Migration Map

Use the template as the target shape, not as a one-shot copy operation. For an
existing project, migrate by moving one boundary at a time:

1. separate the project/folder structure so module domain/application,
   contracts, and infrastructure code are distinct
2. split the global/app `DbContext` into module-owned contexts and schemas
3. move each module's domain-event persistence into the same context as its
   aggregates
4. add module-owned outbox and inbox tables to each module schema
5. replace direct cross-module writes with contracts, durable commands, or
   integration events
6. add Rebus transport and workers after outbox/inbox persistence is in place
7. prove one durable vertical slice before moving the rest of the modules

The migration is successful when the old platform persistence layer has no
module entity mappings left. Shared infrastructure can coordinate persistence
and transport, but it should not own product data.

## Target Architecture

The application remains a modular monolith:

- one deployable host
- one runtime process
- one physical PostgreSQL database
- one observability surface
- explicit module boundaries

Each module owns:

- its domain/application code
- its contracts project
- its infrastructure project
- its `DbContext`
- its PostgreSQL schema
- its EF migrations and migrations history table
- its domain-event, outbox, and inbox tables
- its endpoint registration

The important shift is that persistence is no longer centralized in a platform
`DbContext`. The platform supplies shared infrastructure, but the module owns
its data.

## Current Project Shape

The implemented template uses this shape:

```text
template/server/src/
  ModularTemplate.Host/
    Configuration/
      ModuleConfiguration.cs

  ModularTemplate.Infrastructure/
    Outbox/
      DurableCommandSubmitter.cs
      DurableMessagingOptions.cs
      InboxMessageConfiguration.cs
      InboxMessage.cs
      InboxProcessor.cs
      InboxProcessorBackgroundService.cs
      IInboxProcessor.cs
      ILocalSubscriptionRegistry.cs
      IOutboxDispatcher.cs
      IOutboxTransport.cs
      IOutboxWriter.cs
      LocalSubscriptionRegistry.cs
      OutboxMessageConfiguration.cs
      OutboxDispatcher.cs
      OutboxDispatcherBackgroundService.cs
      OutboxMessage.cs
      OutboxWriter.cs
      RetryDelays.cs
    Persistence/
      DomainEvents/
        StoredDomainEvent.cs
        StoredDomainEventConfiguration.cs
      Transactions/
        ModuleUnitOfWorkBehavior.cs
      IModuleDbContext.cs
      ModuleUnitOfWork.cs
      OutboxModelBuilderExtensions.cs
    Transport/
      AzureServiceBusNamespaceProbe.cs
      DurableTransportEnvelope.cs
      IServiceBusNamespaceProbe.cs
      MessagingTransportConfiguration.cs
      RebusDurableTransportHandler.cs
      RebusOutboxTransport.cs
      ServiceBusTransportStartupValidationHostedService.cs
      TransportConfiguration.cs

  ModularTemplate.Migrator/
    MigratorRunner.cs

  ModularTemplate.SharedKernel/
    Domain/
    Messaging/
    Validation/

  modules/
    ModularTemplate.Identity/
    ModularTemplate.Identity.Contracts/
    ModularTemplate.Identity.Infrastructure/
      Migrations/
      Persistence/
        IdentityDbContext.cs

    ModularTemplate.Operations/
    ModularTemplate.Operations.Contracts/
    ModularTemplate.Operations.Infrastructure/
      Migrations/
      Persistence/
        OperationsDbContext.cs
```

For a product repository, preserve the same dependency direction even if the
module names differ.

Tests mirror the same split:

```text
template/server/tests/
  ModularTemplate.Host.Tests/
    Configuration/
      ServiceBusTransportStartupValidationHostedServiceTests.cs

  ModularTemplate.Migrator.Tests/

  ModularTemplate.Framework.Tests/
    Communication/
      CommunicationPatternExamplesTests.cs
    Messaging/
      MessageTypeRegistryTests.cs
    Persistence/
      DurableCommandSubmitterTests.cs
      DurableMessagingTests.cs
      MessagePersistenceTests.cs
      ModuleUnitOfWorkTests.cs
      RebusTransportTests.cs
```

For a real migration, keep these test categories separate:
communication-pattern examples explain architectural choices, while
PostgreSQL-backed persistence tests prove durability, locking, retries, and
schema ownership. Framework guardrails belong in the factory root when they are
not useful generated-product tests.

## Dependency Rules

Use these rules as the architectural contract:

- Host composes modules and may reference module contracts, module projects, and
  module infrastructure projects.
- A module may reference its own contracts and shared kernel.
- A module infrastructure project may reference its own module and contracts.
- A module contracts project must not reference EF Core, ASP.NET Core,
  infrastructure, application internals, or domain entities.
- A module must not reference another module's domain/application/infrastructure.
- Cross-module synchronous access goes through another module's contracts only.
- Cross-module commands and asynchronous facts go through durable messaging.
- Direct writes to more than one module `DbContext` in the same unit of work are
  rejected.

Architecture tests should enforce these rules once the migration settles.

## Module Persistence

Each module has one concrete EF context implementing `IModuleDbContext`.
That context maps both product tables and the module's messaging tables. This
is the key implementation detail: domain events, outbox rows, inbox rows, and
aggregate changes all live in the same module schema and are saved by the same
module context.

Example:

```csharp
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IIdentityDbContext, IModuleDbContext
{
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();
    public DbSet<ApplicationAccess> ApplicationAccess => Set<ApplicationAccess>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();

    string IModuleDbContext.ModuleName => "identity";

    DbSet<OutboxMessage> IModuleDbContext.OutboxMessages => OutboxMessages;
    DbSet<InboxMessage> IModuleDbContext.InboxMessages => InboxMessages;
    DbSet<StoredDomainEvent> IModuleDbContext.DomainEvents => DomainEvents;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyOutboxConfiguration("identity");
    }
}
```

Register the context with a module-local migrations history table:

```csharp
services.AddDbContext<IdentityDbContext>((sp, options) =>
{
    string connectionString = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("modular-template-host")
        ?? throw new InvalidOperationException("Connection string is required.");

    options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"));
});

services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
services.AddScoped<IOutboxWriter, OutboxWriter<IdentityDbContext>>();
```

The migration output for a module should create all module-owned tables in that
module schema, including:

- domain tables, such as `identity.local_users`
- `identity.domain_events`
- `identity.outbox_messages`
- `identity.inbox_messages`
- `identity.__EFMigrationsHistory`

Do not keep a global `platform` schema for messaging.

When migrating an existing database with production data, decide per module
whether the first module migration creates new tables or moves existing tables
into the module schema. For data-preserving migrations, prefer explicit SQL in
the EF migration over drop/recreate behavior. Typical operations are:

- create the module schema if it does not exist
- move or rename existing tables into that schema
- move indexes, constraints, and sequences with the table when PostgreSQL does
  not do so automatically for the specific object
- create the module-local `__EFMigrationsHistory` table
- seed or mark the module migration as applied only after the physical database
  shape matches the model

Do this one module at a time. The app should be able to start and run against
the migrated module before the next module is moved.

## Unit Of Work

`ModuleUnitOfWork` is registered as the shared `IUnitOfWork`. It inspects all
registered `IModuleDbContext` instances and saves exactly one changed context.

This is intentional. A command handler should mutate one module transaction. If
it needs another module to do work, it should call an in-process contract for a
query or submit durable work through the outbox.

The unit of work also:

1. collects domain events from tracked aggregate roots
2. writes `StoredDomainEvent` rows into the same module context
3. maps selected domain events to integration events
4. writes integration events to the same module outbox
5. saves everything in one module transaction

This replaces the old pattern of sharing an `NpgsqlConnection` and transaction
across a platform context plus several module contexts.

The boundary violation is deliberate operational feedback. During migration,
use it to find workflows that still depend on direct multi-module writes. Do
not bypass it by reintroducing a shared transaction across contexts; split the
workflow into one module transaction plus durable follow-up work.

## Domain Events And Integration Events

A domain event is an internal module fact raised by aggregate behavior. It is
stored in the owning module's `domain_events` table.

An integration event is a public, stable message contract emitted only when a
real consumer needs it. Do not publish every domain event automatically.

Because `StoredDomainEvent` is part of each module context, persisted domain
events should be treated as module-private audit/diagnostic state. Other modules
should never query another module's `domain_events` table. Cross-module
notification starts only after a mapper creates an integration event and the
unit of work writes it to the same module's outbox.

Use explicit mappers:

```csharp
public interface IIntegrationEventMapper<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    string SourceModule { get; }

    IReadOnlyCollection<IIntegrationEvent> Map(TDomainEvent domainEvent);
}
```

The mapper creates integration events, and the unit of work persists them as
outbox messages with stable message type names from `IMessageTypeRegistry`.

## Durable Messaging Model

Durable messaging is used for:

- cross-module commands
- cross-module events
- retryable side effects
- externally observable work
- work that must survive process restart

Use in-process contracts for immediate queries and rare immediate commands.

Durable commands are always send-and-forget. Submitting a durable command can
return an acceptance record, submission id, and optional operation id, but it
must not wait for the target module handler or return the handler's result
payload. If the caller needs a result, model that result as state:

1. create or reuse an operation/read model owned by the initiating workflow
2. submit the durable command with the operation id
3. let the target handler update its module state and, when needed, publish an
   integration event or update an agreed status contract
4. expose the observable result through a query contract or status endpoint

Do not make durable command handlers request/response APIs. The command result
is observed later through reads, operation status, or follow-up integration
events.

`IDurableCommandSubmitter` is the application-facing API. It resolves the
source module's `IOutboxWriter`, serializes the command with a stable message
type name, writes an outbox row, and returns `CommandSubmissionStatus.Accepted`.
It does not dispatch the message directly and it does not know whether the
target handler will eventually succeed. That separation keeps command
submission in the caller's module transaction and leaves delivery to the outbox
worker.

Use `OperationId` when a caller needs to observe progress later. It should be a
stable identifier for state owned by a module, not an implicit RPC correlation
that promises an immediate handler result.

### Choosing A Communication Pattern

Use these rules when replacing cross-module calls:

- In-process query contracts are the default for synchronous cross-module reads.
  Query contracts live in the target module's `.Contracts` project and return
  provider-neutral DTOs.
- Immediate in-process command contracts are allowed only when a caller truly
  needs same-request consistency and the command can still respect the
  single-module unit-of-work rule.
- Durable commands are for asynchronous work requested of a specific target
  module. They require `TargetModule` and are accepted, retried, or dead-lettered
  independently from the caller. Application code should submit them through
  `IDurableCommandSubmitter` instead of constructing raw outbox rows.
- Integration events are public facts emitted by one module and consumed by zero
  or more subscribers. Use them for choreography and eventual consistency, not
  for asking a specific module to do work.

When a workflow spans modules:

- Put module-owned orchestration inside the module that owns the business
  process or aggregate lifecycle. That module may read other modules through
  query contracts and request asynchronous work through durable commands.
- Put host-level orchestration in the Host only for API/user workflows that do
  not naturally belong to one module. The Host coordinates contracts and command
  submissions; it does not mutate multiple module contexts directly.
- Add a separate orchestration layer only when a durable process manager,
  scheduler, or workflow runtime is explicitly accepted as product scope.
- Prefer choreography through integration events when independent modules can
  react to facts without a central coordinator.

Executable examples live in
`tests/ModularTemplate.Framework.Tests/Communication/CommunicationPatternExamplesTests.cs`.
They currently cover:

- synchronous reads through a target module query contract
- durable command submission that returns acceptance only
- module-owned orchestration that reads through a contract and submits durable
  work with an operation id
- choreography through integration event handlers
- host-level orchestration that composes contracts without touching persistence

These are good examples to keep. They make the intended communication choices
concrete without inventing product domain behavior. Treat them as executable
architecture documentation, not as durability tests. The durability guarantees
belong in the PostgreSQL-backed outbox, inbox, unit-of-work, and Rebus tests.

### Outbox

Each module owns an outbox table in its own schema:

```text
identity.outbox_messages
operations.outbox_messages
```

An outbox message records:

- message identity and kind (`Command` or `Event`)
- stable message type name
- source and optional target module
- correlation, causation, and optional operation id
- JSON payload and metadata
- status, attempts, retry timing, locks, and error text

Outbox rows are created in two places:

- `ModuleUnitOfWork` maps selected domain events to integration events and adds
  event outbox rows to the changed module context before saving
- `IDurableCommandSubmitter` writes command outbox rows through the source
  module's `IOutboxWriter`

`OutboxDispatcher` loops through all registered `IModuleDbContext` instances.
For each context it atomically claims a batch using PostgreSQL `FOR UPDATE SKIP
LOCKED`, including stale `Processing` rows whose lock timed out.

Dispatch behavior:

- events resolve subscribers through `ILocalSubscriptionRegistry`
- events with no subscribers are marked processed
- commands require `TargetModule`
- failures increment retry metadata
- exhausted messages become dead-lettered

The dispatcher delegates delivery to `IOutboxTransport`. In the current
template, that transport is Rebus. If a product later swaps the broker, keep the
outbox/inbox database contract stable and replace only the transport adapter.

### Inbox

Each module owns an inbox table in its own schema:

```text
identity.inbox_messages
operations.inbox_messages
```

`InboxProcessor` also loops through all registered module contexts and claims
pending/failed/stale rows with `FOR UPDATE SKIP LOCKED`.

Inbox rows are written by the transport handler. With Rebus,
`RebusDurableTransportHandler` receives a `DurableTransportEnvelope`, locates
the target module context by `TargetModule`, and inserts an inbox row into that
module's schema. Duplicate envelopes are ignored by checking the message id and
target module before insert.

Processing behavior:

1. resolve CLR type from stable message type name
2. deserialize JSON payload
3. build `MessageContext`
4. resolve `IDurableCommandHandler<T>` or `IIntegrationEventHandler<T>`
5. run the handler through the current DI scope
6. mark the inbox message processed only after handler success
7. retry or dead-letter on failure

Handlers are resolved from the processor's current scope so target-module state
changes and inbox status are saved through the same scoped module context.
The handler should mutate only the target module context. If it needs more
cross-module work, it should submit another durable command or publish an
integration event from its own module transaction.

## Transport

Transport is infrastructure, not a module. It lives under
`ModularTemplate.Infrastructure/Transport`.

The implemented transport uses Rebus:

- `RebusOutboxTransport` converts outbox rows to a durable transport envelope
- `RebusDurableTransportHandler` receives envelopes and writes inbox rows
- `DurableTransportEnvelope` is the broker payload
- `TransportConfiguration` registers Rebus, workers, registries, and options
- `IOutboxTransport` is the adapter boundary between outbox persistence and the
  broker

Transport modes:

- `InMemory` for testing/local scenarios when configured
- `AzureServiceBus` for normal non-testing default

`MessagingTransportConfiguration.ResolveTransport` chooses the configured
`Messaging:Transport`; if omitted, it uses `InMemory` in the `Testing`
environment and `AzureServiceBus` otherwise.

When Azure Service Bus is selected:

- `ConnectionStrings:service-bus` is required
- `ServiceBusTransportStartupValidationHostedService` probes the namespace at
  startup
- validation failures should fail fast instead of letting durable messages pile
  up behind a broken broker configuration

This is intentionally not a second application host. The modular monolith still
runs in one deployable process; Rebus and Service Bus provide durable handoff
between module-owned outbox and inbox tables so work can survive restarts and
transient failures.

## Host Composition

The host composes modules and platform infrastructure.

Current composition points:

- `builder.AddModularTemplateHost()` configures the host
- `services.AddModularTemplateMediator()` registers Mediator and
  `ModuleUnitOfWorkBehavior<,>`
- `services.AddModularTemplateModules()` registers Identity and Operations
  module/application/infrastructure services
- `builder.AddTransport()` registers durable messaging infrastructure
- `endpoints.MapModularTemplateModuleEndpoints()` maps module endpoints

When migrating another product, keep `Program.cs` thin and move composition into
host configuration extensions. Composition is the Host's job; business state is
still owned by the modules.

Do not let host-level workflows become a back door to shared persistence. A Host
endpoint may call module query contracts and submit durable commands, but it
should not resolve multiple module contexts and save them directly.

## Migrator

The migrator should migrate module contexts only:

1. `IdentityDbContext`
2. `OperationsDbContext`
3. additional module contexts as they are added

There is no platform persistence context to migrate.

For an existing product, migration runner changes should be boring and explicit.
List each module context by name, migrate it, and avoid reflection-based
"migrate everything" logic until the boundary model is stable. This makes
release reviews and rollback planning much easier.

When adding a module:

1. create the module infrastructure project
2. add its `DbContext`
3. configure schema and migrations history table
4. add initial migration under the module infrastructure project
5. register the context as `IModuleDbContext`
6. add it to `MigratorRunner`

If the module is migrated from an existing shared context, remove its entities
from the old context in the same migration step that proves the new module
context owns those tables. Running two EF models against the same mutable table
is a temporary bridge at best and should not survive the module cutover.

## Operations Module

Operations is the neutral example domain module shipped with the template. It
is not transport infrastructure.

It currently owns:

- `operations.operations`
- `OperationsDbContext`
- `IOperationsQueries`
- operation status endpoint registration

Use it as the pattern for a module-owned schema, contracts project,
infrastructure project, endpoint registration, and tests. Product repositories
can rename or replace it with their first real domain module.

## Message Contracts

Message contracts live in shared kernel or module contracts depending on who
owns the contract. Persist stable names rather than CLR full names.

Example naming:

```text
ModularTemplate.identity.user-registered.v1
ModularTemplate.operations.rebuild-read-model.v1
```

Rules:

- include a version suffix from the beginning
- add `v2` for incompatible payload changes
- keep old handlers if old unprocessed messages may still exist
- do not rename persisted message names casually

### Adding A Durable Command

Use a durable command when one module needs another module to do work
asynchronously.

1. Define a command type that implements `IDurableCommand`. Put it in shared
   kernel only when it is platform-wide; otherwise put it in the owning
   contracts package.
2. Register the CLR type with a stable name in `IMessageTypeRegistry`.
3. Submit it through `IDurableCommandSubmitter` with `SourceModule` and
   `TargetModule`.
4. Implement `IDurableCommandHandler<TCommand>` in the target module.
5. If the initiating caller needs a later result, include an `OperationId` on
   the submission options and expose status/result through a query contract or
   endpoint.

Example shape:

```csharp
public sealed record RebuildCustomerSummary(Guid CustomerId) : IDurableCommand;

messageTypeRegistry.Register<RebuildCustomerSummary>(
    "ModularTemplate.reporting.rebuild-customer-summary.v1");

CommandSubmission submission = durableCommandSubmitter.Submit(
    command,
    new DurableCommandSubmissionOptions(
        SourceModule: "identity",
        TargetModule: "reporting",
        OperationId: operationId));
```

The handler returns `Task`, not `Task<T>`:

```csharp
public sealed class RebuildCustomerSummaryHandler
    : IDurableCommandHandler<RebuildCustomerSummary>
{
    public async Task HandleAsync(
        RebuildCustomerSummary command,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        // Mutate reporting state and optionally update operation/read-model
        // state using context.OperationId.
    }
}
```

`IDurableCommandSubmitter` writes to the source module outbox writer selected by
`SourceModule`. The caller's current module unit of work commits the outbox row
with the source module state change.

### Adding An Integration Event

Use an integration event when one module has completed a fact that other modules
may independently react to.

1. Keep the internal domain event inside the source module.
2. Add an `IIntegrationEvent` contract only when a real consumer exists.
3. Register the event with a stable message type name.
4. Add an `IIntegrationEventMapper<TDomainEvent>` that maps selected domain
   events to integration events.
5. Register event subscribers through `ILocalSubscriptionRegistry`.
6. Implement `IIntegrationEventHandler<TEvent>` in each subscriber module.

Events are fan-out messages. If no subscriber is registered, the outbox event is
marked processed without creating an inbox row.

## Migration Sequence For A Product Repository

Use this sequence when applying the template architecture to an existing
project.

### 1. Inventory Current Boundaries

List:

- modules and current project references
- all EF contexts and migrations
- schemas and tables
- current migration history tables and migration assemblies
- cross-schema foreign keys and EF navigation properties
- background workers
- domain event implementation
- cross-module calls and direct table access
- current broker or queue usage
- request flows that currently update multiple modules
- workflows that currently expect an immediate command result from another
  module

Do not move code until the current dependency graph is understood.

### 2. Create Shared Infrastructure

Add or move:

- `SharedKernel/Domain`
- `SharedKernel/Messaging`
- `Infrastructure/Persistence`
- `Infrastructure/Persistence/DomainEvents`
- `Infrastructure/Persistence/Transactions`
- `Infrastructure/Outbox`
- `Infrastructure/Transport`

Keep Outbox and Transport as folders in shared Infrastructure, not as domain
modules.

At this stage, keep behavior mostly unchanged. The goal is to create the places
where module-owned persistence and durable messaging will live, then move
features into them gradually.

### 3. Convert One Module To Its Own Context

Pick a low-risk module first.

For that module:

- create `{Module}`, `{Module}.Contracts`, and `{Module}.Infrastructure`
  projects or folders if they do not already exist
- create `{Module}DbContext`
- set `HasDefaultSchema("{module}")`
- configure module migrations history table
- move EF configurations into module infrastructure
- expose only a narrow module context interface to module code
- register concrete context as `IModuleDbContext`
- generate a module-owned migration

Acceptance checks:

- module tables are created in the module schema
- existing data is preserved when this is a production migration
- no global app/platform context maps the module's entities
- app starts
- module tests pass

### 4. Add Module-Owned Domain Events, Outbox, And Inbox

In the module context:

- add `DbSet<StoredDomainEvent>`
- add `DbSet<OutboxMessage>`
- add `DbSet<InboxMessage>`
- call `ApplyOutboxConfiguration("{module}")`
- expose the messaging sets through explicit `IModuleDbContext`
  implementation
- register `IOutboxWriter` as `OutboxWriter<{Module}DbContext>`

Regenerate the module migration so the same schema contains:

- `domain_events`
- `outbox_messages`
- `inbox_messages`

Acceptance checks:

- aggregate changes and domain events commit together
- mapped integration events create outbox rows
- failed transactions do not leave orphan messages
- outbox and inbox uniqueness constraints prevent duplicate processing

### 5. Replace Cross-Module Writes

Search for code that mutates more than one module in one request or command.
Also search for direct reads against another module's tables; those should move
behind target-module contracts even when they are read-only.

Replace with:

- in-process query contracts for reads
- explicit immediate command contracts only when immediate consistency is truly
  required
- durable command submission for asynchronous cross-module work
- integration events for facts consumed by other modules

If old code expected a result from another module's command, split the flow:

- submit durable work and return acceptance plus an optional operation id
- record any result in module-owned state, an operation record, or a read model
- expose that result through a query contract or endpoint
- use integration events for completion facts that other modules can react to

Acceptance checks:

- `ModuleUnitOfWork` can reject multi-context mutations without breaking valid
  flows
- modules do not reference other modules' infrastructure or domain projects
- durable command submissions do not block waiting for handler results
- result-oriented workflows expose operation/read-model status through queries

Use the communication examples as decision tests here. Add or adapt examples
when a real workflow introduces a new pattern, but keep them small and
domain-neutral enough that reviewers can understand the architectural choice.

### 6. Add Transport And Workers

Register:

- `ILocalSubscriptionRegistry`
- `IMessageTypeRegistry`
- `IDurableCommandSubmitter`
- `IOutboxDispatcher`
- `IInboxProcessor`
- `OutboxDispatcherBackgroundService`
- `InboxProcessorBackgroundService`
- Rebus in-memory transport for tests
- Rebus Azure Service Bus transport for deployed environments

Configure:

```json
{
  "Messaging": {
    "Enabled": true,
    "Transport": "AzureServiceBus",
    "PollingInterval": "00:00:02",
    "BatchSize": 20,
    "MaxAttempts": 5,
    "LockTimeout": "00:05:00"
  },
  "ConnectionStrings": {
    "service-bus": "<connection-string>"
  }
}
```

Acceptance checks:

- `InMemory` transport works in tests
- Azure Service Bus transport fails fast when configured incorrectly
- outbox dispatcher and inbox processor can run with no pending messages
- duplicate Rebus envelopes create only one target inbox row

### 7. Add One End-To-End Durable Flow

Implement one vertical slice:

1. HTTP endpoint accepts work
2. application changes one module
3. module transaction persists state, domain event, and outbox message
4. outbox dispatches through Rebus
5. inbox writes into the target module schema
6. inbox processor invokes a strongly typed handler
7. handler mutates the target module
8. target module saves its inbox status and state together

Acceptance checks:

- flow works after process restart
- handler failure retries
- repeated failure dead-letters
- concurrent workers do not double-process the same row
- operation id/correlation metadata reaches the durable command handler when a
  caller will query status later

Pick a slice with real business value but low blast radius. Avoid migrating the
most entangled workflow first; the first slice should validate the architecture,
not carry every legacy edge case at once.

### 8. Move Remaining Modules

Repeat the module-context conversion one module at a time.

For each module:

- own schema
- own migrations
- own context
- own domain-event/outbox/inbox tables
- no cross-module EF navigation properties
- no cross-module foreign keys by default
- explicit contracts only where there is a real consumer

Do not postpone cleanup of the old shared context until the end. After each
module moves, remove the obsolete mappings, services, and tests for that module
from the old layer so later work cannot accidentally depend on them again.

### 9. Stabilize And Document The New Rules

After all modules move:

- add architecture tests for project references and forbidden dependencies
- document how to add module migrations
- document how to add durable commands and integration events
- document local `InMemory` transport and deployed Azure Service Bus setup
- add operational notes for inspecting pending, failed, and dead-lettered
  outbox/inbox rows
- remove temporary migration bridges and compatibility code

This is when the runbook content should be promoted into stable product docs.

## Testing Checklist

Keep these tests or equivalents:

- communication-pattern examples for in-process query contracts, durable
  command acceptance, module-owned orchestration, choreography, and host
  orchestration
- module context creates its schema and tables
- module migrations apply through the migrator
- unit of work persists aggregate, domain events, and outbox atomically
- unit of work rejects changes in multiple module contexts
- outbox command delivery creates target inbox row
- durable command submitter writes to the requested source module outbox
- durable command handlers expose no direct response payload
- durable command operation ids reach handler `MessageContext`
- module-owned orchestration can combine query contracts with durable command
  submission without direct multi-module persistence
- integration event inbox processing invokes typed event handlers
- event with no subscribers is marked processed
- event with subscribers creates one inbox row per subscriber
- outbox transport failure retries and dead-letters
- inbox handler success marks processed
- inbox handler failure retries and dead-letters
- stale `Processing` messages are reclaimed
- concurrent workers claim each message once
- target module routing writes to the target schema and not the source schema
- duplicate transport envelopes are idempotent at the inbox boundary
- Rebus in-memory transport writes inbox messages
- Azure Service Bus startup validation handles configured/unconfigured modes
- generated API client remains stable after bootstrap rename

Use PostgreSQL-backed tests for durability behavior. Mocking EF or the broker is
not enough for the locking and transaction guarantees.

The current communication examples are worth keeping because they document
intent in code. I would keep them in a `Communication` folder, separate from
`Persistence`, and avoid making them heavier. They should answer "which pattern
should this workflow use?" The persistence and transport tests should answer
"does this survive concurrency, retries, restarts, and duplicate delivery?"

## Documentation Checklist

Update product docs after the migration:

- module structure and dependency rules
- when to use contracts vs durable messaging
- how to observe durable command results through operation/read-model queries
- where to place host-level, module-owned, or separate orchestration
- how to add a module
- how to add a durable command
- how to add an integration event and subscriber
- how to add a module migration
- how to configure local/in-memory transport
- how to configure Azure Service Bus
- how to inspect outbox/inbox/dead-lettered messages

## Non-Goals

Do not add these during the migration unless a separate accepted design calls
for them:

- distributed transactions
- cross-module EF navigation properties
- generic CRUD integration events
- publishing every domain event automatically
- event sourcing
- complex saga DSL
- Quartz/Hangfire/Wolverine as a second background-work stack
- speculative public contracts without a consumer

## Done Criteria

The migration is complete when:

- there is no platform/global persistence context
- every module has its own schema, `DbContext`, and migrations
- each module schema contains its own domain-event, outbox, and inbox tables
- durable workers run through shared Infrastructure
- Rebus in-memory and Azure Service Bus modes are configured and tested
- direct multi-module persistence is rejected
- cross-module reads use contracts
- cross-module commands/events use durable messaging
- migrator applies module contexts only
- architecture tests cover forbidden project references
- end-to-end tests cover outbox, inbox, retries, dead-lettering, and locking
