---
name: modular-template-module
description: Scaffold or extend backend modules in the generated modular monolith. Use when adding a new module, module Contracts project, module Infrastructure project, module DbContext/schema/migrations, module endpoint mapping, module docs, or module tests under server/src/modules.
---

# Modular Template Module

Use this skill to add or evolve backend modules without breaking the template's
module boundaries.

## Read First

Before editing, read:

- `AGENTS.md`
- `docs/governance.md`
- `docs/architecture/server.md`
- `docs/architecture/intermodule-communication.md`
- `docs/modules/README.md`

If the module adds product behavior, persistence behavior, APIs, orchestration,
durable messaging, auth/session behavior, migrations, or frontend surfaces,
confirm there is an accepted OpenSpec artifact or durable architecture decision.

## Module Shape

Create this shape under `server/src/modules`:

```text
{Product}.{Module}/
{Product}.{Module}.Contracts/
{Product}.{Module}.Infrastructure/
```

Use the existing Identity and Products modules as local examples.

Dependency direction:

- `{Module}` may reference `{Module}.Contracts` and SharedKernel.
- `{Module}.Infrastructure` may reference `{Module}`, `{Module}.Contracts`,
  SharedKernel, and shared Infrastructure.
- `{Module}.Contracts` must stay provider-neutral and must not reference EF
  Core, ASP.NET Core, Infrastructure, domain entities, `ClaimsPrincipal`, or
  provider SDK types.
- No module may reference another module's domain or infrastructure project.
  Cross-module reads use contracts; cross-module async work uses durable
  commands or integration events.

## Scaffold Steps

1. Derive names:
   - Pascal module name: `Catalog`
   - schema/module key: `catalog`
   - projects: `{Product}.Catalog`, `{Product}.Catalog.Contracts`,
     `{Product}.Catalog.Infrastructure`
2. Add project files with references that match the dependency direction.
3. Add the new projects to the solution file.
4. Add project references from Host and Migrator when they need to compose or
   migrate the module.
5. Add module registration in `{Module}ModuleConfiguration`.
6. Add infrastructure registration in `{Module}InfrastructureConfiguration`.
7. Add a narrow module DbContext interface, such as `ICatalogDbContext`.
8. Add `{Module}DbContext` implementing the narrow interface and
   `IModuleDbContext`.
9. In `OnModelCreating`, call:

```csharp
modelBuilder.HasDefaultSchema("catalog");
modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
modelBuilder.ApplyOutboxConfiguration("catalog");
```

10. Expose `OutboxMessages`, `InboxMessages`, and `DomainEvents` through
   `IModuleDbContext`.
11. Register the context with a module-local migrations history table:

```csharp
options.UseNpgsql(
    connectionString,
    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"));
```

12. Register:
    - the narrow DbContext interface
    - module repositories/query services
    - `IModuleDbContext`
    - `IOutboxWriter` as `OutboxWriter<CatalogDbContext>`
13. Add the module to Host module composition when the Host must compose it.
14. Add the module context to the Migrator explicitly.
15. Add module endpoint mapping only if accepted scope requires endpoints.
16. Add `docs/modules/{module}.md` and link it from `docs/modules/README.md`.
17. Add focused unit tests for domain behavior and integration tests for
    persistence only when the module owns real persistence behavior.

## Minimal DbContext Skeleton

```csharp
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext, IModuleDbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();

    string IModuleDbContext.ModuleName => "catalog";
    DbSet<OutboxMessage> IModuleDbContext.OutboxMessages => OutboxMessages;
    DbSet<InboxMessage> IModuleDbContext.InboxMessages => InboxMessages;
    DbSet<StoredDomainEvent> IModuleDbContext.DomainEvents => DomainEvents;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.ApplyOutboxConfiguration("catalog");
    }
}
```

## Verification

Prefer the narrowest useful checks:

- `dotnet build server/{Product}.slnx`
- affected module test project
- migrator tests when adding a context to the Migrator
- full backend tests when project references or shared infrastructure change

Do not leave temporary generated migrations, scratch modules, or bootstrap
outputs in source unless the user explicitly asks to keep them.
