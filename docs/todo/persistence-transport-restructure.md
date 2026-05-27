# Persistence / Transport / Outbox Restructure

## Status

Implementation appears substantially complete in the template payload:
`ModularTemplate.Persistence` has been removed, Persistence/Outbox/Transport
now live as folders inside `ModularTemplate.Infrastructure`, module `DbContext`
types own their outbox/inbox/domain-event tables, and the Migrator now migrates
module contexts only.

Remaining sweep items are quality and boundary checks rather than the original
file-move plan. In particular, keep unit-of-work handling limited to one
changed module context per save; use module contracts or durable messaging for
cross-module work. The detailed move inventory below is historical and may
name the earlier separate Outbox/Transport project split.

## 2026-05-27 Sweep Notes

- `ModularTemplate.Persistence`, the platform DbContext, and the `platform.*`
  messaging schema are gone from the template payload.
- Durable messaging workers are registered through Infrastructure transport
  configuration, not a persistence configuration type.
- Inbox processing must resolve durable message handlers from the processor's
  current DI scope so target-module mutations and inbox status are saved
  through the same module context.
- Completed OpenSpec changes for durable message workers and the Operations
  status slice remain under `template/openspec/changes/`; archive them when the
  team is ready for accepted specs to become the source of truth.

## Context

The current `ModularTemplate.Persistence` project conflates three concerns:

- Outbox and inbox table definitions and polling machinery
- Rebus transport bridge
- Cross-module context enrollment for shared transactions

This creates a dependency inversion: `ModularTemplate.Persistence` directly references
`ModularTemplate.Identity.Infrastructure` and `ModularTemplate.Operations.Infrastructure`
so it can register their `DbContext` instances. All module contexts share one
`NpgsqlConnection` and are enlisted in a cross-context transaction inside
`CommandTransactionBehavior` solely because the outbox table lives in a separate context.

The fix is to move the outbox tables into each module's own context and schema, which
eliminates the shared platform context and the cross-context transaction machinery.

## Target Layout

```
server/src/
  ModularTemplate.SharedKernel/          ← unchanged: domain primitives
  ModularTemplate.Infrastructure/        ← platform infrastructure library
  │ Persistence/                              IUnitOfWork, IModuleDbContext,
  │                                           ModuleUnitOfWork, EF config helpers,
  │                                           stored domain events
  │ Outbox/                                   outbox/inbox DB entities, dispatcher,
  │                                           processor, background services, retry logic,
  │                                           IOutboxWriter, IOutboxDispatcher,
  │                                           IInboxProcessor, IOutboxTransport
  │ Transport/                                DurableTransportEnvelope,
  │                                           RebusOutboxTransport,
  │                                           RebusDurableTransportHandler,
  │                                           transport config, ASB startup probe
  ModularTemplate.Host/                  ← registers Infrastructure transport,
  │                                           wires Rebus, background services, unit of work
  modules/
    ModularTemplate.Identity.Infrastructure/   → references Infrastructure, stamps tables into identity.*
    ModularTemplate.Operations.Infrastructure/ → references Infrastructure, stamps tables into operations.*
```

`ModularTemplate.Persistence` is deleted. Its `platform.*` schema (outbox, inbox, domain_events)
and its EF migrations are removed. Each module migration replaces them.

## Why Outbox and Transport Are Not Modules

Modules have domain boundaries, aggregates, and bounded-context APIs consumed through
`.Contracts` projects. Persistence, Outbox, and Transport are platform infrastructure
capabilities that modules depend on, not domains of their own. They now live together
inside `ModularTemplate.Infrastructure` beside `SharedKernel` as a platform dependency.

## Operations Module Rename Note

`ModularTemplate.Operations` is the neutral placeholder name for the first domain module
shipped with the template. Template users rename it to match their first real domain
(e.g. `Orders`, `Incidents`, `Records`). It is a full domain module — aggregate, repository,
HTTP endpoint, migrations — not an infrastructure project. It stays as-is structurally.

## Steps

### 1. Create ModularTemplate.Infrastructure.Outbox

- Move from `ModularTemplate.Persistence/Messaging/`:
  `OutboxMessage`, `OutboxMessageConfiguration`, `InboxMessage`, `InboxMessageConfiguration`,
  `OutboxDispatcher`, `InboxProcessor`, `OutboxDispatcherBackgroundService`,
  `InboxProcessorBackgroundService`, `RetryDelays`, `DurableMessagingOptions`,
  `IOutboxDispatcher`, `IInboxProcessor`, `IOutboxTransport`, `IOutboxWriter` (new interface),
  `ILocalSubscriptionRegistry`, `LocalSubscriptionRegistry`
- Move from `ModularTemplate.Persistence/DomainEvents/`:
  `StoredDomainEvent`, `StoredDomainEventConfiguration`
- Move from `ModularTemplate.Persistence/Transactions/`:
  `CommandTransactionBehavior` (simplified — single-context, see step 3)
- Add `ApplyOutboxConfiguration(string schema)` extension on `ModelBuilder` that stamps
  `outbox_messages`, `inbox_messages`, and `domain_events` into the given schema.
- References: `Microsoft.EntityFrameworkCore.Relational`, `Microsoft.Extensions.Hosting.Abstractions`,
  `Mediator.Abstractions`, `Microsoft.Extensions.Options`

### 2. Create ModularTemplate.Infrastructure.Transport

- Move from `ModularTemplate.Persistence/Messaging/`:
  `DurableTransportEnvelope`, `RebusOutboxTransport`, `RebusDurableTransportHandler`,
  `AzureServiceBusNamespaceProbe`, `IServiceBusNamespaceProbe`,
  `MessagingTransportConfiguration`, `ServiceBusTransportStartupValidationHostedService`
- References: `ModularTemplate.Infrastructure.Outbox`, `Rebus.AzureServiceBus`, `Rebus.ServiceProvider`,
  `Rebus.Core`, `Microsoft.Extensions.Hosting.Abstractions`

### 3. Simplify CommandTransactionBehavior

Currently it takes three `DbContext` instances to enlist them in a shared transaction.
Once the outbox is co-located with domain data (same context, same schema), the behavior
becomes a standard single-context pipeline behavior:

```
BeginTransaction → invoke next → capture domain events → write outbox rows → commit
```

No `NpgsqlConnection` sharing, no `UseTransactionAsync` across contexts.
The behavior lives in `ModularTemplate.Infrastructure.Outbox` and receives `IModuleDbContext` (a marker
interface module contexts implement) rather than a concrete type.

### 4. Update module infrastructure projects

Each module's `DbContext.OnModelCreating` calls:

```csharp
modelBuilder.ApplyOutboxConfiguration("identity"); // or "operations"
modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
```

Each module's `Add*Infrastructure` registration:

```csharp
services.AddScoped<IOutboxWriter, OutboxWriter<IdentityDbContext>>();
// CommandTransactionBehavior is already registered globally via AddModularTemplateMediator
```

### 5. Update Host composition

`PersistenceConfiguration` becomes `TransportConfiguration` (or inline in `Program.cs`):

- Registers Rebus with in-memory or ASB transport
- Registers `IOutboxTransport → RebusOutboxTransport`
- Registers `ILocalSubscriptionRegistry` singleton
- Registers background services from `ModularTemplate.Infrastructure.Outbox`
- No `DbContext` registrations (those move to module infra)
- No `NpgsqlConnection` sharing (no longer needed)

### 6. Regenerate migrations

- Delete `ModularTemplate.Persistence/Migrations/`
- Add new migration to `ModularTemplate.Identity.Infrastructure/Migrations/` that includes
  `identity.outbox_messages`, `identity.inbox_messages`, `identity.domain_events`
- Add new migration to `ModularTemplate.Operations.Infrastructure/Migrations/` for the same
  tables in the `operations` schema

### 7. Delete ModularTemplate.Persistence

Remove the project and its `<ProjectReference>` from:

- `ModularTemplate.Host.csproj`
- `ModularTemplate.Migrator.csproj`
- Any test projects

### 8. Update MigratorRunner

`MigratorRunner` migrates only the module contexts.
No platform context.

### 9. Update tests

- `DurableMessagingTests` moves to each module's test project or a shared test helper
  assembly. The fixture creates only the module's own `DbContext`.
- `RebusTransportTests` moves to a dedicated transport test project or stays in Identity
  tests with the updated context.

## Current File Inventory (handoff reference)

### ModularTemplate.Persistence — everything here moves or is deleted

```
src/ModularTemplate.Persistence/
  ModularTemplate.Persistence.csproj          ← DELETE
  ModularTemplateDbContext.cs                  ← DELETE (platform.* schema goes away)
  Migrations/
    20260526183605_InitialCreate.*             ← DELETE (platform schema replaced by per-module)
    ModularTemplateDbContextModelSnapshot.cs   ← DELETE

  DomainEvents/
    StoredDomainEvent.cs                       → ModularTemplate.Infrastructure.Outbox/DomainEvents/
    StoredDomainEventConfiguration.cs          → ModularTemplate.Infrastructure.Outbox/DomainEvents/

  Messaging/
    OutboxMessage.cs                           → ModularTemplate.Infrastructure.Outbox/Outbox/
    OutboxMessageConfiguration.cs              → ModularTemplate.Infrastructure.Outbox/Outbox/
    InboxMessage.cs                            → ModularTemplate.Infrastructure.Outbox/Inbox/
    InboxMessageConfiguration.cs               → ModularTemplate.Infrastructure.Outbox/Inbox/
    IOutboxDispatcher.cs                       → ModularTemplate.Infrastructure.Outbox/
    IInboxProcessor.cs                         → ModularTemplate.Infrastructure.Outbox/
    IOutboxTransport.cs                        → ModularTemplate.Infrastructure.Outbox/  (no Rebus dependency)
    OutboxDispatcher.cs                        → ModularTemplate.Infrastructure.Outbox/
    InboxProcessor.cs                          → ModularTemplate.Infrastructure.Outbox/
    OutboxDispatcherBackgroundService.cs       → ModularTemplate.Infrastructure.Outbox/
    InboxProcessorBackgroundService.cs         → ModularTemplate.Infrastructure.Outbox/
    RetryDelays.cs                             → ModularTemplate.Infrastructure.Outbox/
    DurableMessagingOptions.cs                 → ModularTemplate.Infrastructure.Outbox/
    ILocalSubscriptionRegistry.cs             → ModularTemplate.Infrastructure.Outbox/
    LocalSubscriptionRegistry.cs              → ModularTemplate.Infrastructure.Outbox/
    IServiceBusNamespaceProbe.cs               → ModularTemplate.Infrastructure.Transport/
    AzureServiceBusNamespaceProbe.cs           → ModularTemplate.Infrastructure.Transport/
    DurableTransportEnvelope.cs                → ModularTemplate.Infrastructure.Transport/
    IOutboxTransport.cs (interface only)       → ModularTemplate.Infrastructure.Outbox/  ← shared boundary
    RebusOutboxTransport.cs                    → ModularTemplate.Infrastructure.Transport/
    RebusDurableTransportHandler.cs            → ModularTemplate.Infrastructure.Transport/
    MessagingTransportConfiguration.cs         → ModularTemplate.Infrastructure.Transport/
    ServiceBusTransportStartupValidationHostedService.cs → ModularTemplate.Infrastructure.Transport/

  Transactions/
    CommandTransactionBehavior.cs              → ModularTemplate.Infrastructure.Outbox/ (simplified)

  Configuration/
    PersistenceConfiguration.cs               ← DELETE (replaced by Transport registration in Host)
```

### Current namespaces (must update on move)

| Current namespace                           | New namespace                                                                               |
| ------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `ModularTemplate.Persistence`               | deleted                                                                                     |
| `ModularTemplate.Persistence.Messaging`     | split: `ModularTemplate.Infrastructure.Outbox` / `ModularTemplate.Infrastructure.Transport` |
| `ModularTemplate.Persistence.DomainEvents`  | `ModularTemplate.Infrastructure.Persistence.DomainEvents`                                   |
| `ModularTemplate.Persistence.Transactions`  | `ModularTemplate.Infrastructure.Persistence.Transactions`                                   |
| `ModularTemplate.Persistence.Configuration` | deleted                                                                                     |

### Current project references to ModularTemplate.Persistence

| Project                         | csproj location                                                |
| ------------------------------- | -------------------------------------------------------------- |
| `ModularTemplate.Host`          | `src/ModularTemplate.Host/ModularTemplate.Host.csproj`         |
| `ModularTemplate.Migrator`      | `src/ModularTemplate.Migrator/ModularTemplate.Migrator.csproj` |
| `Identity.Infrastructure.Tests` | `tests/modules/ModularTemplate.Identity.Infrastructure.Tests/` |

All three must drop the `ModularTemplate.Persistence` reference and add the appropriate
`ModularTemplate.Infrastructure.Outbox` / `ModularTemplate.Infrastructure.Transport` references.

### ModularTemplate.Persistence.csproj — current package refs (redistribute on move)

```xml
<!-- → ModularTemplate.Infrastructure.Outbox -->
<PackageReference Include="Mediator.Abstractions" />
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<!-- → ModularTemplate.Infrastructure.Transport -->
<PackageReference Include="Rebus.AzureServiceBus" />
<PackageReference Include="Rebus.ServiceProvider" />
<!-- ModularTemplate.Persistence.csproj also has ProjectReferences to module infra — DELETE these -->
<ProjectReference Include="..\modules\ModularTemplate.Identity.Infrastructure\..." />
<ProjectReference Include="..\modules\ModularTemplate.Operations.Infrastructure\..." />
```

### CommandTransactionBehavior — what it does today (must simplify)

Constructor: `(ModularTemplateDbContext, IdentityDbContext, OperationsDbContext, IServiceProvider, IMessageTypeRegistry)`

- Opens transaction on `platformContext`, enlists `identityContext` and `operationsContext`
  via `UseTransactionAsync` on the shared `NpgsqlConnection`
- Calls `SaveChangesAsync` on all three, then commits
- Iterates `ChangeTracker.Entries<IAggregateRoot>()` on module contexts to capture domain
  events, persists `StoredDomainEvent` to `platformContext.DomainEvents`, and writes
  `OutboxMessage` to `platformContext.OutboxMessages` via `IIntegrationEventMapper<T>`

After refactor: constructor is `(TModuleDbContext, IServiceProvider, IMessageTypeRegistry)` where
`TModuleDbContext : DbContext`. Single `SaveChangesAsync`. Domain events, outbox, and domain
data are all in the same context.

### MigratorRunner — current (must update)

Migrates three contexts in order:

1. `ModularTemplateDbContext` (platform) ← DELETE this step
2. `IdentityDbContext`
3. `OperationsDbContext`

After: migrates only module contexts. Add new contexts here when new modules are added.

### Existing migrations (must regenerate)

| Context                    | Migration timestamp | Schema                                       |
| -------------------------- | ------------------- | -------------------------------------------- |
| `ModularTemplateDbContext` | `20260526183605`    | `platform` (outbox, inbox, domain_events)    |
| `IdentityDbContext`        | `20260526183612`    | `identity` (local_users, application_access) |
| `OperationsDbContext`      | `20260526185415`    | `operations` (operations)                    |

After: Identity migration gains `identity.outbox_messages`, `identity.inbox_messages`,
`identity.domain_events`. Operations migration gains the same in `operations` schema.
Platform migration and context are deleted entirely.

### Test project — current state

`tests/modules/ModularTemplate.Identity.Infrastructure.Tests/`

- `Persistence/DurableMessagingTests.cs` — 8 tests covering dispatch, fan-out, dead-letter,
  full pipeline, stale-lock reclaim, concurrent claiming
- `Persistence/RebusTransportTests.cs` — 3 tests: handler direct, dedup, e2e Rebus roundtrip
- `Persistence/IdentityRepositoryTests.cs` — uses `IdentityDbContext` + `ModularTemplateDbContext`
  (the platform context ref must be removed once outbox moves to `IdentityDbContext`)
- `Support/PostgreSqlFixture.cs` — `IAsyncLifetime`, detects rootless Podman socket,
  `IClassFixture<PostgreSqlFixture>` pattern; each test calls `EnsureDeleted`+`EnsureCreated`

After refactor: tests only need `IdentityDbContext`. Remove `ModularTemplateDbContext`
from test helpers. `CreateDbContext()` factory in test classes simplifies to one context.

### Raw SQL column names — critical for FOR UPDATE SKIP LOCKED queries

Both `OutboxDispatcher` and `InboxProcessor` use `ExecuteSqlAsync(FormattableString)` with
quoted Pascal-case column names. These are schema-qualified and must match the EF configuration
of the entity in the new per-module schema:

```sql
UPDATE {schema}.outbox_messages
SET "Status" = ..., "LockedAtUtc" = ..., "LockedBy" = ...
WHERE "Id" = ANY(
    SELECT "Id" FROM {schema}.outbox_messages
    WHERE ("Status" = ... OR "Status" = ...) AND "NextAttemptAtUtc" <= ...
    ...
    FOR UPDATE SKIP LOCKED
)
```

The schema prefix (`platform.` today) must be parameterised or templated when outbox
config is applied per-module schema.

## What Does Not Change

- `LocalSubscriptionRegistry` pattern for event fan-out is correct. Module infra registers
  subscribers via `IHostedService` when the first real cross-module event is added.
- `SendLocal` on Rebus is correct for the modular monolith topology (same process, same queue).
- Operations module shape, endpoints, and aggregate are unchanged.
- The `SharedKernel` messaging contracts (`IDurableCommand`, `IIntegrationEventMapper`, etc.)
  remain in `SharedKernel/Messaging/` unchanged.
