# Template Current State

Status: Reconciled checkpoint for the next template planning session.

This file is planning context for building the template itself. It is not
intended to be inherited as stable product documentation.

## Current Source Of Truth

- Hard repository rules: `docs/governance.md`.
- Stable architecture and platform docs: `docs/architecture.md`,
  `docs/architecture/*.md`, `docs/platform/*.md`, and `docs/testing.md`.
- Accepted behavior: `openspec/specs/`.
- Active proposed behavior: `openspec/changes/`.
- Archived accepted changes: `openspec/changes/archive/`.

There are currently no active OpenSpec changes.

## Completed Gates

### Gate 1: Repo Skeleton And Documentation Indexes

Completed.

Includes:

- Root README and AGENTS index.
- Stable documentation indexes under `docs/`.
- Top-level skeleton folders for `server`, `web`, `orchestration`, `deploy`,
  and `scripts`.

### Gate 2: Solution And Repository Infrastructure

Completed.

Includes:

- `ModularTemplate.slnx`.
- `global.json`.
- `Directory.Build.props`.
- `Directory.Packages.props`.
- Root pnpm workspace files.
- .NET project shells for Host, Migrator, ServiceDefaults, SharedKernel,
  Persistence, Identity contracts/module/infrastructure, tests, and
  Orchestration.

Gate 2 intentionally did not include domain behavior, persistence behavior,
auth/session plumbing, frontend apps, Aspire resources, CI workflows, generated
migrations, tests, or template automation.

## Tooling Checkpoints

### Devcontainer Tooling

Completed after Gate 2.

Includes:

- `.devcontainer/devcontainer.json`.
- `.devcontainer/README.md`.

The devcontainer is a tooling checkpoint, not runtime behavior.

### OpenSpec Tooling

Completed and accepted through archived change
`2026-05-06-adopt-openspec-sdd`.

Includes:

- `scripts/setup-openspec.sh`.
- `openspec/` initialized with OpenSpec `1.3.1`.
- Codex OpenSpec skills under `.codex/skills`.
- Durable governance in `docs/governance.md`.
- Accepted current behavior represented under `openspec/specs/`.

OpenSpec is the current spec-driven development workflow for substantial
runtime behavior in this repository.

## Implemented Runtime And Platform Gates

### Gate 3: SharedKernel Primitives

Completed.

Includes:

- `Entity<TId>` base type with identity equality.
- `AggregateRoot<TId>` base type with pending domain-event collection.
- `IDomainEvent` and `DomainEvent` metadata primitives.
- `ValueObject` structural equality base type.
- `DomainException` base type.

### Gate 4: ServiceDefaults

Completed.

Includes:

- `ModularTemplate.ServiceDefaults` extension methods for OpenTelemetry,
  service discovery, default HTTP resilience, health checks, and development
  health endpoints.
- Central package versions for ServiceDefaults dependencies.
- Host calls to `AddServiceDefaults` and `MapDefaultEndpoints`.

### Gate 5: Host Foundation

Completed.

Includes:

- Host problem-details registration with trace identifiers.
- Host exception handler and status-code-pages middleware.
- Small Host error-handling configuration extension methods to keep
  `Program.cs` thin.

### Gate 6: Persistence Foundation

Completed.

Includes:

- `ModularTemplate.Persistence` Host-owned composition project.
- Concrete EF Core `ModularTemplateDbContext` shell.
- Narrow Identity persistence interface implemented by the shared concrete
  DbContext.
- Shared PostgreSQL persistence registration through `AddPersistence`.
- Migrator wiring that resolves `ModularTemplateDbContext` and calls
  `Database.MigrateAsync`.

Generated EF migrations are still intentionally absent.

### Gate 7: Identity Current-User And Application Access

Accepted into OpenSpec current specs.

Current specs:

- `openspec/specs/identity-current-user/spec.md`.
- `openspec/specs/host-api/spec.md`.

Includes:

- Provider-neutral current-user contracts.
- Local user resolution and lazy local user creation by `(provider, subject)`.
- Application-owned access records.
- Idempotent minimal initial application access bootstrap.
- `GET /api/me` response semantics.
- Host claim parsing separated from Identity local-user and access decisions.

The direct `IIdentityStore` abstraction remains transitional. It can stay until
the durable DDD/CQRS persistence conventions are documented and implemented,
but it should be replaced or reshaped into repository/query-handler patterns
before becoming final template guidance.

### Gate 8: Local OIDC Session Platform

Implemented and archived under OpenSpec change
`2026-05-06-add-oidc-session-platform`.

Current specs:

- `openspec/specs/auth-session/spec.md`.
- `openspec/specs/local-oidc-session-platform/spec.md`.

Includes:

- Host-owned cookie and OpenID Connect authentication composition.
- API redirect suppression for unauthenticated and forbidden API requests.
- Minimal Host-owned Redis-backed authentication ticket store.
- Browser login and logout Host routes.
- Provider-neutral claim mapping for OIDC `sub`, `iss`, `name`, and `email`
  claims.
- Aspire app host with PostgreSQL, Redis, Keycloak, Migrator, and Host
  resources.
- Checked-in Keycloak realm import for the local `modular-template` realm and
  `modular-template-host` client.

Custom request-header authentication is test-only backend verification
scaffolding and is not production Host authentication.

### Gate 9: Web App Foundation

Implemented and archived under OpenSpec change
`2026-05-06-add-web-app-foundation`.

Current spec:

- `openspec/specs/web-app-foundation/spec.md`.

Includes:

- `web/apps/admin` React/Vite app shell.
- `web/apps/web` React/Vite app shell.
- Shared `web/packages/auth` browser-safe session helpers.
- Shared `web/packages/config` TypeScript, Vite, and Vitest configuration.
- TanStack Query and TanStack Router bootstrap in both apps.
- Same-origin `/api/me`, `/auth/login`, and `/auth/logout` browser flows.
- Access-state handling for loading, unauthenticated, authenticated without
  access, authenticated with access, and error states.
- Frontend tests for auth helpers and app shell access states.

The frontend apps intentionally avoid product-specific workflows and do not
store identity-provider tokens in browser code.

### Gate 10: Frontend Orchestration

Implemented and archived under OpenSpec change
`2026-05-06-add-frontend-orchestration`.

Current spec:

- `openspec/specs/frontend-orchestration/spec.md`.

Includes:

- Aspire-managed Vite resources for the admin and web apps.
- Host origin propagation through `VITE_HOST_ORIGIN`.
- Local frontend proxy preservation for `/api/` and `/auth/`.
- Stable docs for the local platform resource graph.

### Gate 11: Generated API Clients

Implemented under active OpenSpec change `add-generated-api-clients`.

Includes:

- Host OpenAPI generation for template-owned `/api/` endpoints.
- Build-time OpenAPI document output at
  `web/packages/api-client/openapi/host.json`.
- Generated TypeScript API client package at `web/packages/api-client`.
- Same-origin API client configuration for browser consumers.
- `web/packages/auth` current-user loading through the generated client
  boundary.
- Root generation and freshness-check scripts.
- Husky pre-commit generated-client freshness check.

## Deferred Or Absent

The following remain intentionally absent until accepted OpenSpec artifacts or
durable architecture decisions define their scope:

- Shared UI package conventions beyond the current frontend foundation.
- Generated TanStack Query helpers from Hey API.
- CI workflow definition.
- Generated EF migrations.
- Template automation for creating product repositories.
- Mailpit or other additional local service resources.
- Durable intermodule messaging and outbox processing.
- Identity-provider Admin API provisioning workflows.

## Last Verified Tool Versions

- .NET SDK: `10.0.203`.
- Node: `v24.15.0`.
- pnpm: `10.33.3`.

## Last Verification Commands

These passed after this documentation reconciliation:

```sh
dotnet test ModularTemplate.slnx
pnpm api-client:check
pnpm format:check
pnpm frontend:typecheck
pnpm frontend:test
pnpm frontend:build
pnpm frontend:lint
openspec validate --all --strict
```

Recent archived changes also record successful verification with combinations
of:

```sh
dotnet restore ModularTemplate.slnx
dotnet build ModularTemplate.slnx --no-restore
dotnet test ModularTemplate.slnx
```

## Suggested Next Step

The next substantial runtime/platform gate should add an OIDC/browser-session
smoke surface that uses the generated clients from both frontend apps to
exercise the real login, current-user, and logout path. This should start with
OpenSpec because it adds intentional frontend behavior and verification
expectations around the browser session boundary.

After that smoke gate, consider a generated TanStack Query helpers gate. Hey API
supports this directly, but the template should first decide whether app code
uses generated hooks/options directly or consumes template-owned wrappers from
shared packages.

After the smoke gate, reconcile planning material before pruning it: move any
still-useful guidance into stable docs, accepted OpenSpec specs, or
template-only docs that the future bootstrap script explicitly ignores. Then
delete obsolete plan documents, run another code review, gather testing
feedback, and review again.

Pure documentation freshness, broken reference cleanup, formatting, restore,
build, test, and lint fixes can be handled directly when they do not alter
runtime behavior.

## Fresh Agent Handoff

Start with this file, then read:

- `AGENTS.md`.
- `docs/governance.md`.
- `docs/openspec.md`.
- `openspec/specs/`.
- `docs/architecture/server.md`.
- `docs/architecture/web.md`.
- `docs/architecture/orchestration.md`.
- `docs/platform/auth-and-authorization.md`.
- `docs/platform/local-services.md`.
- `docs/template/README.md`.
- `docs/template/implementation-plan.md` for historical planning context and
  still-open template lanes.

Before editing, inspect the dirty worktree and do not revert existing changes.

## Notes

- OpenSpec should not be installed automatically by devcontainer rebuilds.
- `docs/governance.md` governs this template repository. Decide during product
  bootstrap work whether generated repositories inherit it unchanged or receive
  a product-specific bootstrap governance document.
