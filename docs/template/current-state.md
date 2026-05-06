# Template Current State

Status: Gate checkpoint for the next implementation session.

This file is planning context for building the template itself. It is not
intended to be inherited as stable product documentation.

## Completed Gates

### Gate 1: Repo Skeleton And Documentation Indexes

Completed.

Includes:

- Root README and AGENTS index.
- Stable documentation indexes under `docs/`.
- Top-level skeleton folders for `server`, `web`, `orchestration`, `deploy`,
  `scripts`, and the former `openspec` placeholder.

Note: `openspec/` was created as a Gate 1 placeholder before the SDD-tooling
pivot. The accepted direction is now Spec Kit with Codex, Archive, and Refine.

### Gate 2: Solution And Repository Infrastructure

Completed.

Includes:

- `ModularTemplate.slnx`.
- `global.json`.
- `Directory.Build.props`.
- `Directory.Packages.props`.
- Root pnpm workspace files.
- Empty .NET project shells for Host, Migrator, ServiceDefaults, SharedKernel,
  Identity contracts/module/infrastructure, and Orchestration.

Gate 2 intentionally does not include domain behavior, persistence,
auth/session plumbing, frontend apps, Aspire resources, CI workflows, generated
migrations, tests, or template automation.

## Last Verified Tool Versions

- .NET SDK: `10.0.203`.
- Node: `v24.15.0`.
- pnpm: `10.33.3`.

## Last Verification Commands

These passed after Gate 5:

```sh
dotnet build ModularTemplate.slnx --no-restore
pnpm format:check
```

## Devcontainer Tooling Step

Completed after Gate 2.

Includes:

- `.devcontainer/devcontainer.json`.
- `.devcontainer/README.md`.

The devcontainer is based on the saved
[devcontainer-baseline.md](devcontainer-baseline.md), with pnpm provided by a
devcontainer feature rather than a post-create script.

This was a tooling checkpoint, not Gate 3.

## Spec Kit Tooling Step

Completed after the devcontainer checkpoint.

Includes:

- `scripts/setup-speckit.sh`.
- `.specify/` initialized with Spec Kit `0.8.5`.
- Codex integration under `.agents/skills`.
- Archive extension pinned to `stn1slv/spec-kit-archive` `v1.0.0`.
- Refine extension pinned to `Quratulain-bilal/spec-kit-refine` `v1.0.0`.
- Initial `.specify/memory/constitution.md`.
- Removal of the obsolete `openspec/` placeholder.

This was an SDD tooling checkpoint, not Gate 3.

### Gate 3: SharedKernel Primitives

Completed.

Includes:

- `Entity<TId>` base type with identity equality.
- `AggregateRoot<TId>` base type with pending domain-event collection.
- `IDomainEvent` and `DomainEvent` metadata primitives.
- `ValueObject` structural equality base type.
- `DomainException` base type.

Gate 3 intentionally does not include persistence, EF Core mappings, Mediator
pipeline behavior, outbox storage, module behavior, or generated tests.

### Gate 4: ServiceDefaults

Completed.

Includes:

- `ModularTemplate.ServiceDefaults` extension methods for OpenTelemetry,
  service discovery, default HTTP resilience, health checks, and development
  health endpoints.
- Central package versions for ServiceDefaults dependencies.
- Host calls to `AddServiceDefaults` and `MapDefaultEndpoints`.

Gate 4 intentionally does not include feature endpoints, persistence,
auth/session plumbing, Aspire resource topology, or production health-check
policy.

### Gate 5: Host Foundation

Completed.

Includes:

- Host problem-details registration with trace identifiers.
- Host exception handler and status-code-pages middleware.
- Small Host error-handling configuration extension methods to keep
  `Program.cs` thin.

Gate 5 intentionally does not include feature endpoints, OpenAPI generation,
auth/session plumbing, persistence, module registration behavior, or frontend
integration.

## Next Intended Step

Review Gate 5 Host foundation before proceeding to persistence foundation.

Recommended next gate: persistence foundation.

Expected scope:

- Shared Host-owned EF Core DbContext shell.
- Narrow module DbContext interface pattern, starting with an empty Identity
  persistence surface if needed for composition.
- Migrator wiring that can later apply Host-owned migrations.
- No generated migrations yet.
- No domain behavior, Identity behavior, auth/session plumbing, API endpoints,
  outbox/Rebus, Aspire resource topology, or frontend work.

Spec Kit note: the next persistence-only gate can remain documentation/code
driven. The first upcoming gate that should use Spec Kit is the initial
auth/session and `/api/me` behavior slice.

## Fresh Agent Handoff

Start with this file, then read:

- `AGENTS.md`.
- `.specify/memory/constitution.md`.
- `docs/architecture/server.md`.
- `docs/template/template-decisions.md`.
- `docs/template/implementation-plan.md` for historical planning context only.

Treat this file as the current gate source of truth when it differs from older
planning notes.

Before editing, inspect the dirty worktree and do not revert existing changes.
The current uncommitted change set includes the completed devcontainer, Spec
Kit, Gate 3, Gate 4, and Gate 5 work.

## Notes

- `.github/skills/aspire/SKILL.md` is an Aspire-installed skill artifact. It was
  not part of Gate 1 or Gate 2 template scaffolding.
- Spec Kit should not be installed automatically by devcontainer rebuilds.
- The initial constitution governs this template repository. Decide during the
  product-bootstrap work whether generated repositories inherit it unchanged or
  receive a product-specific bootstrap constitution.
- Current worktree changes include prior Gate 1/Gate 2 work plus devcontainer,
  Spec Kit, SharedKernel, ServiceDefaults, and Host foundation updates.
