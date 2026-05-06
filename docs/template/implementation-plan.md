# Net React Modular Template Implementation Plan

Status: Discussion Draft

Location note: this file lives under `docs/template/` because it is planning context for building the template, not product documentation that must survive unchanged in repositories created from the template.

This document captures the starting implementation plan for a domain-neutral .NET + React modular-monolith template. It is intentionally written before scaffolding so architectural choices, defaults, and unresolved tradeoffs are visible instead of being hidden in generated code.

Archived attempts were research context only. The implementation should be fresh code, not a bulk copy of an older project.

## Current Goals

- Create a reusable educational template for rapidly rebuilding product experiments.
- Keep the template domain-neutral while providing realistic Identity/Auth infrastructure.
- Prefer explicit, inspectable architecture over framework magic.
- Optimize for learning, replacement, and restartability rather than production longevity.
- Document decisions as first-class artifacts before implementation.
- Make future agent work easier through short AGENTS indexes, durable docs, Spec Kit, and reusable scripts/skills.

These are current goals, not permanent product constraints. The template is expected to evolve as implementation pressure reveals better choices.

## Current Non-Goals

- Do not build a production platform in this planning phase.
- Do not preserve existing archived domain code.
- Do not build microservices or service extraction scaffolding.
- Do not implement durable intermodule messaging until a concrete use case exists.
- Do not introduce external authorization engines such as OpenFGA, Cedar, or OPA.
- Do not make the template a full code generator unless we explicitly choose that direction later.

These are also current non-goals. They can be revisited when a later template version has a concrete need.

## Proposed Repository Shape

```text
/
  README.md
  AGENTS.md
  docs/
    README.md
    architecture.md
    architecture/
      server.md
      web.md
      orchestration.md
    constitution.md
    constitution/
      server.md
      web.md
      docs-and-agents.md
    modules/
      README.md
      identity.md
    platform/
      auth-and-authorization.md
      local-services.md
      observability.md
      ai.md
    testing.md
    testing/
      server.md
      web.md
      e2e.md
      eval.md
    template-decisions.md
  .specify/
  .agents/
  Template.slnx
  Directory.Build.props
  Directory.Packages.props
  server/
    src/
      Template.Host/
      Template.Migrator/
      Template.ServiceDefaults/
      Template.SharedKernel/
      modules/
        Template.Identity/
        Template.Identity.Contracts/
        Template.Identity.Infrastructure/
    tests/
  web/
    apps/
    packages/
  orchestration/
    Template.Orchestration/
  deploy/
  scripts/
  .devcontainer/
```

Decision: use lower-case `server/src/modules`. It reads like a repository root and avoids treating modules as a .NET namespace layer.

Decision: keep the `.slnx` file at the repository root. Use the new solution format, not the old `.sln` format.

Do not include ADRs as a first-class artifact in the template source. The template repository can keep a `docs/template/template-decisions.md` planning ledger, but generated/copied product repositories should not inherit a stale ADR history. Product repositories can introduce ADRs later when they have real project-specific decisions.

Architecture docs should use the thin-index pattern:

- `docs/architecture.md` is the stable high-level index.
- `docs/architecture/*.md` contains detailed area guidance.
- The index links forward and summarizes; it does not duplicate detailed rules.

## Naming Strategy

The template needs two naming modes:

- Source template name: use a neutral placeholder such as `ModularTemplate`, `Template`, or `Company.Product`.
- Consumer project name: copied projects should be easy to find-replace.

Recommendation:

- Use one obvious placeholder namespace prefix, probably `ModularTemplate`.
- Use package/project names such as `ModularTemplate.Host`, `ModularTemplate.Identity`, `ModularTemplate.Identity.Contracts`.
- Keep docs clear that project creation is initially copy-paste plus find-replace.
- Add a rename script once the scaffold exists. Use a skill around the script later so agents also run builds/tests, inspect missed references, and repair compile errors.

The first rename entrypoint should accept:

- `productName`, such as `AcmeDesk`.

Derive slugs deterministically from the product name. Add explicit slug override support later only if real product names prove the derived form is wrong. The script should cover .NET namespaces/projects, `.slnx` references, pnpm package names/scopes, Aspire resource names, identity-provider realm/client names, docs, and README references where safe.

## Backend Stack

Accepted direction:

- .NET 10.
- ASP.NET Core Minimal APIs.
- EF Core with PostgreSQL.
- Modular monolith.
- Focused DDD: aggregate roots, entities, value objects, domain events.
- CQRS-style split between command-side aggregate stores/repositories and read-side query services.
- Mediator from `martinothamar/Mediator`.
- Rebus as the preferred durable transport when async messaging becomes concrete.
- Custom outbox when Rebus is introduced for durable intermodule or external messaging.
- OpenTelemetry through Aspire service defaults.
- OpenAPI from the Host.
- Microsoft Agent Framework for AI workflows/evals when a product needs AI behavior.

Rejected/default-deferred direction:

- Marten as the primary store.
- Wolverine as the messaging/workflow foundation.
- Brighter as the in-process modular-monolith message transport.
- Microservices from the initial template.
- Durable messaging and outbox before a concrete workflow requires them.

## Module Model

Each business module with persistence or external adapters uses three projects:

```text
server/src/modules/ModularTemplate.<Module>.Contracts/
server/src/modules/ModularTemplate.<Module>/
server/src/modules/ModularTemplate.<Module>.Infrastructure/
```

Decision: use a separate `.Infrastructure` project by default for modules with persistence or external adapters. The ceremony is worth it because dependency direction stays visible: Module does not depend on Infrastructure, while Host composes infrastructure.

Module implementation layout:

```text
ModularTemplate.<Module>/
  Domain/
    Aggregates/
    Events/
    ValueObjects/
  Features/
    <FeatureName>/
      Endpoint.cs
      Command.cs
      CommandHandler.cs
      Query.cs
      QueryHandler.cs
      Request.cs
      Response.cs
      Validator.cs
      I<FeatureName>Store.cs
  Config/
    <Module>Module.cs

ModularTemplate.<Module>.Infrastructure/
  Config/
    <Module>InfrastructureServiceCollectionExtensions.cs
  Configurations/
  Features/
    <FeatureName>/
      Ef<FeatureName>Store.cs
  Persistence/
    I<Module>DbContext.cs
  ExternalAdapters/
```

Rules:

- Contracts expose public commands, queries, DTOs, exceptions, integration events, and public API abstractions.
- Contracts must not expose EF entities or aggregate internals.
- If a contract request/response, command/result, or query/result pair has only one use, prefer keeping the pair in the same contract file.
- Module implementation should be feature-slice first, not Clean Architecture layers.
- Aggregate-root behavior is the default for business behavior.
- Plain entities and transaction-script-style persistence are allowed only for technical records such as logs, inbox/outbox records, read-model maintenance tables, or other non-domain append-only support data.
- Complex slices can introduce aggregate roots, policies, domain services, or slice-local stores.
- Cross-module synchronous access goes through `.Contracts` APIs, typically Mediator requests or narrow API-like interfaces.
- Modules register themselves through extension methods; the Host only composes those registrations.

DDD rules:

- Aggregate roots inherit from SharedKernel `AggregateRoot`.
- Aggregate roots expose `Create` factory methods and specialized behavior methods.
- Aggregate roots do not expose public constructors for normal creation.
- External code must not directly change aggregate state through public setters.
- Domain events are raised by aggregate behavior and stored on the aggregate root until the persistence pipeline records them.
- Domain events are persisted to shared storage for audit, future timelines, and later integration-event projection.
- Domain event metadata must be explicit and stable: module name, logical event name, and version.
- Put domain event classes related to the same aggregate in one file unless the file becomes genuinely hard to navigate.
- The template should combine explicit module-owned event metadata with registry-style validation for missing or duplicate event names.

## Persistence Decision

This is the main architectural tradeoff.

### Option A: Shared DbContext

One concrete `ModularTemplateDbContext` lives at the Host persistence boundary and implements narrow module context interfaces such as `IIdentityDbContext`.

Pros:

- One command can update multiple modules in one database transaction.
- EF migrations are simpler at the beginning.
- In-process modular-monolith benefits are real, not theoretical.
- Outbox/durable messaging can be deferred without weakening immediate consistency.

Cons:

- The concrete DbContext can become an accidental coupling point.
- The Host references module domain/configuration assemblies.
- Module extraction later requires persistence untangling.
- Code review and architecture tests must defend module boundaries.

### Option B: Module-Owned DbContexts

Each module owns its own DbContext, schema, migration history, unit of work, and domain event storage.

Pros:

- Stronger module ownership and extraction story.
- Less temptation to join across modules.
- Module persistence can evolve more independently.

Cons:

- Cross-module consistency needs durable messaging, orchestration, or compensating behavior much sooner.
- Simple in-process flows pay distributed-system complexity early.
- Global transactions across multiple DbContexts are awkward and should not become a hidden default.

Decision: start with Option A, the shared-DbContext approach, but make the guardrails explicit.

Guardrails:

- Concrete DbContext lives in `Host` or a Host-owned persistence project.
- Tables are schema-separated by module.
- Each module defines an `I<Module>DbContext` persistence interface exposing only its DbSets.
- Module stores inject the narrow module DbContext interface, not the concrete DbContext.
- Cross-module reads/writes never use another module's DbSet.
- Architecture tests enforce project references and forbidden namespace access.
- Domain event records are persisted centrally, but every event declares its owning module explicitly.
- Durable outbox is deferred until a workflow has at least one meaningful async consumer.
- Host owns EF migrations and a separate `.Migrator` project applies them.
- Generated migrations should not be committed to the template source before final naming is stable. Product repositories generate their own initial migration after namespace/project rename.

This choice should be recorded in `docs/template/template-decisions.md` and reflected in architecture docs before scaffolding.

## Transactions, Domain Events, And Outbox

Initial command pipeline:

1. A command enters Mediator.
2. A transaction behavior opens an EF transaction for commands that use persistence.
3. The handler mutates aggregates through module stores/repositories.
4. The pipeline calls `SaveChangesAsync` once at the top application boundary.
5. `SaveChangesAsync` appends pending domain events to a central `event_log.domain_events` table.
6. The transaction commits.

Rules:

- Queries do not save and prefer no-tracking reads.
- Manual `IUnitOfWork.SaveChangesAsync` inside handlers is allowed only for rare intermediate flushes.
- Domain events are durable history and future integration-event projection input; they are not event sourcing.
- Event identity uses stable logical names such as `identity.user-created.v1`, not CLR type names.
- Same-module pre-commit handlers can be added when needed, inside the same transaction.
- Post-commit notifications and cross-module async reactions require an outbox.
- Rebus is the preferred transport once durable messaging exists.

Decision: use `event_log.domain_events`, because the table is durable domain history and a future replay/projection source, not only audit and not transport-specific outbox storage.

Durable messaging should evolve naturally. The template should not force sagas, choreography, or orchestration infrastructure just because modules exist. When a real workflow needs eventual consistency, retries, delayed processing, external side effects, external-event replay to new consumers, or module extraction pressure, that module can opt in to outbox/Rebus and document the new behavior.

## Identity And Auth Boundary

Identity is the first concrete module because authentication and local authorization are template-level concerns.

Identity module owns:

- Local `UserIdentity` records mapped to OIDC `sub`.
- Lazy user creation when an authenticated OIDC principal first calls the application.
- Staff/application authorization records owned by the app.
- Idempotent bootstrap of one initial application admin for local and real environments.
- `GET /api/me` current-user context.
- Provider-neutral user context contracts.
- Identity-provider provisioning abstractions and infrastructure adapters when an admin portal feature needs them.

Host owns:

- ASP.NET Core authentication middleware.
- OpenID Connect challenge/callback/logout mechanics.
- Cookie session configuration.
- Redis-backed `ITicketStore`.
- API authentication behavior: unauthenticated API requests return `401`, forbidden requests return `403`, and API endpoints do not redirect.

The identity provider owns:

- OIDC authentication.
- Account lifecycle, profile, MFA, required actions, password recovery.
- Local realm and client configuration for development.

Application owns:

- Product authorization.
- Staff/admin assignment.
- Organization/workspace/customer membership in future products.
- Resource-specific authorization policies.

Important rule: identity-provider roles, groups, and organization membership are not authoritative for product authorization. They can be useful diagnostics or provider data, but application behavior must be decided from application-owned records.

Open question: should auth be represented as a module?

Recommendation:

- Do not create a separate `Auth` module initially.
- Treat auth mechanics as Host/platform responsibility.
- Put identity translation and application authorization in the Identity module.
- Create `docs/platform/auth-and-authorization.md` to make the boundary very explicit.

Lazy user creation needs an explicit rule: authenticating through the identity provider proves identity, but it does not grant product access. The first authenticated request can create or upsert a local `UserIdentity` from OIDC claims. Staff/admin/customer access still requires app-owned authorization records.

Admin bootstrap rule:

- The template should support one configured initial admin identity.
- Bootstrap is idempotent and creates the local admin authorization record after the user identity exists or can be resolved.
- Real application data and later user/customer provisioning happens through admin portal functionality.
- Do not rely on identity-provider roles for this bootstrap.
- Identity-provider Admin API integration can be added when the admin portal implements invitation/provisioning workflows; it is not required for the first login/current-user slice.

## Frontend Stack

Accepted direction:

- React.
- Vite.
- pnpm workspace.
- TanStack Query.
- TanStack Router.
- Zustand.
- Tailwind.
- shadcn-style components, copied/owned in a shared UI package.
- Playwright for e2e.

Initial template shape:

```text
web/
  apps/
    admin/
    web/
  packages/
    api-client/
    auth/
    ui/
    config/
```

Decision: start with `admin` and `web`.

- `admin` is for app-owned staff/admin capabilities.
- `web` is the neutral user-facing portal. Avoid `workspace` in the template because workspace terminology is domain-specific.
- `web/packages/auth` should contain browser-safe BFF session helpers and route guard utilities. It must never store or expose Keycloak tokens.

Frontend rules:

- Browser code never stores Keycloak access or refresh tokens.
- Browser apps call same-origin BFF/API endpoints.
- Local Vite proxies `/api/` and `/auth/` routes to the Host.
- Shared frontend packages should stay boring: UI primitives, typed clients, route/auth helpers, test utilities.
- Do not mirror backend modules one-to-one in frontend folders.

## Local Platform And Orchestration

Aspire is the local entrypoint.

Initial resources:

- Host API.
- Migrator.
- PostgreSQL.
- Redis for BFF session ticket storage.
- Keycloak.
- Mailpit.
- Vite frontend app(s).

Potential later resources:

- pgvector in PostgreSQL for semantic retrieval.
- Ollama for local LLM behavior.
- Qdrant only if a specific vector-store comparison is useful.
- Rebus transport backing services when durable messaging becomes concrete.

Decision: keep orchestration in top-level `orchestration/` rather than under `server/`. The top-level shape is clearer for a monorepo template than a server-contained AppHost.

Redis is part of the first local topology because BFF session ticket storage is part of the target auth architecture.

Document Ollama and local AI resources now, but do not wire them into the first required startup path. AI-heavy products can opt in later. The MAF implementation should be designed fresh from lessons learned, not copied from archived code.

## Devcontainer Strategy

The template should preserve the current devcontainer shape before archived context is removed. See [devcontainer-baseline.md](devcontainer-baseline.md).

Recommendation: include a `.devcontainer` by default, but keep local non-container development fully supported. A ready dev environment is excellent for onboarding and agent repeatability; the risk is that rebuilding a container can lose AI-agent history or tool-local state if that state lives inside the container filesystem.

Guardrails:

- Keep important agent/project knowledge in the repository, not only in tool-local history.
- Prefer host-mounted state for AI tooling when available.
- Avoid devcontainer rebuilds as a routine dependency-update mechanism.
- Document which state is safe to lose and which state should be mounted or backed up.
- Treat devcontainer as an accelerator, not as the only supported development path.

## Testing Strategy

Backend:

- xUnit.
- NSubstitute.
- Shouldly.
- Testcontainers for all integration tests with real IO.
- No in-memory persistence stack for integration behavior.
- Architecture tests for project references, module boundary rules, and forbidden dependencies.

Test categories:

- `Unit`: one class/function, no IO.
- `Application`: several application classes, no IO.
- `Integration`: real IO through Testcontainers.
- `Eval`: MAF/LLM evaluation when AI behavior exists.

Every backend test should declare a category trait so CI can choose test bands explicitly:

```csharp
[Trait("Category", "Unit")]
```

Default CI should run `Unit` and `Application` on every pull request. `Integration` should run in scheduled, main-branch, or explicitly requested workflows once Testcontainers service cost is acceptable. `Eval` should never run by default. E2E should run only in controlled environments where the full platform and browser auth path are intentionally available.

Recommended projects:

```text
server/tests/
  architecture/
    ModularTemplate.Architecture.Tests/
  host/
    ModularTemplate.Host.Tests/
  modules/
    Identity/
      ModularTemplate.Identity.Tests/
  testing/
    ModularTemplate.Testing.Shared/
```

Use folders to group test ownership instead of adding `.Modules` to every test project name. Split module tests into Unit/Application/Integration projects only when the module grows enough to justify it.

Frontend:

- Vitest.
- React Testing Library.
- Playwright for e2e.
- Keep cross-app Playwright tests under `web/tests` if multiple apps exist.

Template-specific testing is mandatory. The template should test not only application code, but also the template mechanics:

- Architecture tests for module dependency direction, namespace rules, forbidden references, and domain-event metadata uniqueness.
- Bootstrap/rename tests that generate or copy a temporary product name and verify no placeholders remain in expected paths.
- Build/test smoke checks against the renamed generated repository.
- Tests or scripted checks for package/version centralization, `.slnx` membership, pnpm workspace membership, and docs links.
- Auth/session integration tests for BFF cookie behavior, API `401`/`403` behavior, Redis ticket storage, lazy user creation, and initial admin bootstrap.
- Event-log persistence tests that verify aggregate domain events are recorded with stable module/name/version metadata.
- Frontend tests for auth package behavior, route guards, API client generation, and `/api/me` consumption.
- E2E smoke tests for the controlled local platform once Aspire can run the Host, identity provider, Redis, PostgreSQL, Mailpit, and Vite apps together.

The bootstrap and rename path should be treated like product code. If the template cannot reliably produce a buildable renamed repository, the template is broken.

## Automation Baseline

Initial automation:

- `Directory.Build.props` for shared .NET compiler/project policy.
- `Directory.Packages.props` for central NuGet package versions.
- Root `package.json` and pnpm workspace scripts for repo-level validation.
- Husky + commitlint for semantic commits.
- Prettier for consistent docs/frontend formatting.
- GitHub Actions CI for formatting, backend restore/build/test, frontend lint/build/test.
- Dependabot for GitHub Actions, devcontainer, npm, and NuGet updates.
- Release Please with config and manifest files.

Recommendations:

- Reference Rebus packages in `Directory.Packages.props` only when we are ready to show the intended package baseline, but do not wire Rebus runtime services until an outbox/message workflow exists.
- Keep central package management from day one; it makes rename/copy behavior and Dependabot grouping easier.
- Do not commit template-generated EF migrations until naming is stable. Product repositories should generate their own first migration after rename.

## Documentation And Agent System

Documentation rules:

- Durable human-useful rules live in `docs/`, not only in AGENTS files.
- AGENTS files are short navigational indexes with agent-specific reminders.
- README files explain local folder purpose and commands.
- Architecture docs describe current shape.
- `docs/template/template-decisions.md` explains why template-building decisions were made.
- Spec Kit feature artifacts describe accepted behavior and planned work.
- Planning notes remain clearly marked until converted into architecture docs, the template decision ledger, or Spec Kit artifacts.

Initial docs to create before or during scaffolding:

- `docs/README.md`.
- `docs/architecture.md`.
- `docs/architecture/server.md`.
- `docs/architecture/web.md`.
- `docs/platform/auth-and-authorization.md`.
- `docs/testing.md`.
- `docs/template/template-decisions.md` for template-building decisions that should not automatically become product ADRs.

Skills:

- Keep repo-local skills under `.codex/skills`.
- Add skills only for workflows that repeat and benefit from instructions or scripts.
- Likely first skills/scripts: initialize pinned Spec Kit tooling, add server module, run template rename/verification.
- Add ADR management skills as optional product-repo skills, even though this template does not ship its own ADR history.
- Add a bootstrap skill or script for creating a fresh product repository from the template once the first delivery is stable.
- Consider template-change export/import skills for flowing proven product improvements back into the template.
- Spec Kit Codex skills should be initialized from the `specify init --integration codex` flow rather than hand-authored from scratch.
- Do not use ADR skills as the template's own decision ledger; use `docs/template/template-decisions.md` while building the template.

Template evolution skill idea:

- `template-change-export` runs in a product repository and writes a structured change packet describing a stabilized improvement, affected files, generic decision, tests, commands, and product-specific details to exclude.
- `template-change-import` runs in the template repository and turns that packet into generalized template changes plus documentation updates.
- `template-maintain` may run in the template repository to check placeholder consistency, docs consistency, bootstrap/rename behavior, generated-repo smoke tests, and stale planning artifacts.
- Export/import should support review; it should not blindly sync product code into the template.
- Import and maintain tools are manual-only. They must not automatically upgrade product repositories from the template because mature products may intentionally diverge.

Manual packet flow:

1. Product repository runs `template-change-export`.
2. Product repository writes a packet under `.template-changes/`.
3. Human moves the packet into this template repository under `docs/template/inbox/`.
4. Template repository runs `template-change-import`.
5. The import skill generalizes the packet into template docs/code/tests.
6. Accepted packets move to `docs/template/archive/` or are removed after the resulting template change is reviewed.

Do not build automatic template-update application for product repositories in the first version. A product can record its originating template version or commit, but newer template patterns should be ported into products manually and one scoped change at a time.

## Spec Kit Strategy

Use Spec Kit for greenfield spec-driven development, not every architecture preference.

Decision: pivot from OpenSpec to Spec Kit while the repository is still early.
The former `openspec/` Gate 1 placeholder should be replaced by Spec Kit
artifacts.

Initial Spec Kit stack:

- Spec Kit core, installed through the official `specify-cli` GitHub source and pinned to a reviewed version.
- Codex integration, installed through `specify init --integration codex`.
- Archive extension, pinned to `stn1slv/spec-kit-archive` `v1.0.0`.
- Refine extension, pinned to `Quratulain-bilal/spec-kit-refine` `v1.0.0`.

Do not auto-install Spec Kit from the devcontainer lifecycle. The devcontainer
should provide prerequisites such as Python and `uv`; a repo script should run
the actual Spec Kit initialization intentionally so generated files can be
reviewed.

Initial accepted specs should be small:

- Backend auth/session context.
- Identity current-user context.
- Staff-admin bootstrap.

Rules:

- Do not implement substantial new behavior from planning notes alone.
- Template architecture decisions go to `docs/template/template-decisions.md` first, then stable current-state rules are reflected in architecture docs.
- Behavior contracts go to Spec Kit feature artifacts first.
- If docs, specs, and code drift, call out the drift before extending it.

Planning decisions such as folder shape, `.Infrastructure` projects, `.slnx` placement, testing categories, and rename automation do not need Spec Kit feature specs. Auth/session behavior, `/api/me`, admin bootstrap, and future admin provisioning workflows do.

The first setup script should be manual and reviewable, likely under
`scripts/setup-speckit.sh`. It should install or verify `specify`, initialize
the project with the Codex integration, add only the approved Archive and
Refine extensions, print installed versions, and refuse to overwrite an
existing `.specify/` setup unless an explicit force flag is provided.

## Copy-Paste Versus Opinionated Flows

Question: should this repository be a simple source template or provide more opinionated creation flows?

Recommendation: phase it.

Phase 1: Copy-paste plus rename automation.

- Keep placeholder names consistent.
- Keep docs explicit.
- Provide a script for deterministic rename chores by the end of first delivery.
- Provide a skill that runs the script, verifies builds/tests, searches for missed placeholders, and repairs straightforward fallout.
- Learn from the first one or two rebuilds.

Phase 2: Thin helper scripts.

- Add validation scripts.
- Add a rename checklist or script if find-replace becomes repetitive.
- Add module scaffolding only after the module shape stabilizes.
- Wrap scripts with skills when agent behavior matters: running build, finding missed names, interpreting compiler errors, and updating docs.

Phase 3: Opinionated generator only if proven useful.

- Consider `dotnet new` templates or a custom script later.
- Avoid turning the educational template into its own product too early.

## Initial Scaffold Order

1. Documentation skeleton and template decision ledger.
2. Solution and repository infrastructure.
3. Devcontainer and Spec Kit tooling.
4. SharedKernel.
5. ServiceDefaults.
6. Host.
7. Shared DbContext and Migrator.
8. Identity.Contracts, Identity, and Identity.Infrastructure.
9. Initial Spec Kit feature specs for auth/session, `/api/me`, and admin bootstrap.
10. BFF auth with Keycloak and Redis ticket storage.
11. Aspire orchestration with PostgreSQL, Redis, Keycloak, Mailpit, Host, Migrator, frontend.
12. Admin and web frontend apps with shared auth package and `/api/me` integration.
13. OpenAPI client generation after initial endpoint contracts settle.
14. Tests and CI.
15. Agent docs and first skills, including rename/verification.
16. Dependabot and Release Please.

## Proposed Template Decision Records

Record these in `docs/template/template-decisions.md`, not as ADRs inherited by generated products:

- Use monorepo with server, web, orchestration, docs, and deploy roots.
- Use shared DbContext with module schemas and narrow module context interfaces.
- Use BFF sessions with server-side OIDC tokens and Redis ticket storage.
- Treat Keycloak as authentication provider, not product authorization source.
- Use feature-slice-first module structure.
- Defer durable intermodule messaging until a concrete async consumer exists.
- Use Aspire for local platform topology.
- Use React, TanStack, Tailwind, shadcn, pnpm, and Vite for frontends.
- Use Testcontainers for integration tests and no in-memory integration stack.
- Include Dependabot and Release Please in the initial automation baseline.
- Use separate `.Infrastructure` projects for modules with persistence or external adapters.
- Bootstrap one app-owned initial admin.
- Generate OpenAPI clients as part of first delivery after initial endpoint contracts settle.
- Provide rename automation plus a verification skill.
- Use Spec Kit with Codex, Archive, and Refine for greenfield SDD.

## Open Discussion Items

1. Should the domain event log table stay `event_log.domain_events`, or do we want a more explicit name such as `event_log.recorded_domain_events`?
2. Should the first delivery include identity-provider Admin API integration, or only bootstrap one app-owned admin and leave provisioning for the first admin-portal feature?
3. Which OpenAPI generator should be the default for the frontend package?
4. Should `.devcontainer` be included by default despite AI-agent history/state concerns?
5. What exact files and resource names should rename automation modify in the first version?
6. What should the first template-change packet schema include?

## Current Working Recommendations

- Choose shared DbContext for the template's first version.
- Keep durable messaging and Rebus absent from runtime until a concrete async use case exists.
- Keep Auth mechanics in Host; keep Identity and app-owned authorization in the Identity module.
- Start with Redis-backed BFF session storage because it is part of the target architecture and Aspire can make it cheap locally.
- Start with `admin` and `web` frontend apps plus `web/packages/auth`.
- Use copy-paste plus a rename script for the first version; add a skill around verification and repair.
- Build docs and a template decision ledger before code so future scaffold work has a stable decision trail without inheriting stale ADRs.

## Implementation Handoff Notes

The implementation phase may be split across several agents after archived source context is removed. Agents should treat this document as planning input and should create stable product-facing docs under `docs/architecture`, `docs/platform`, `docs/testing`, `docs/modules`, and local README/AGENTS files as they scaffold.

Do not copy planning-only uncertainty into runtime docs. Stable docs should describe the chosen implementation. If a decision is still unsettled, keep it in `docs/template/template-decisions.md` until accepted.

Implementation must move slowly. Do not create a giant scaffold in one pass. Each step or substep should be small enough for review, discussion, and approval before the next layer is added.

Suggested gates:

1. Repo skeleton and documentation indexes only.
2. Central props, `.slnx`, empty projects, and package baselines.
3. Devcontainer and Spec Kit tooling.
4. SharedKernel DDD primitives.
5. ServiceDefaults.
6. Host foundation without Identity behavior.
7. Persistence foundation: shared DbContext, Migrator wiring, and event log
   persistence.
8. Identity module contracts/domain/application, then infrastructure
   separately.
9. Host auth/session plumbing, then `/api/me`.
10. Aspire resources, then frontend apps, then auth package integration.
11. OpenAPI generation, CI automation, rename script, and maintenance skills.

`docs/template/current-state.md` is the authoritative source for the latest
completed gate and next step. This section is planning context and may lag if a
gate is split during implementation.

Recommended split for implementation agents:

- Repo foundation: solution, central package management, pnpm workspace, formatting, Husky, commitlint, CI, Dependabot, Release Please.
- Backend foundation: SharedKernel, ServiceDefaults, Host, Migrator, shared DbContext, transaction pipeline, domain event persistence.
- Identity/auth: Identity module, lazy user creation, `/api/me`, app-owned admin authorization, Host OIDC/cookie/Redis session mechanics, Keycloak local setup.
- Web foundation: `admin` and `web` apps, `web/packages/auth`, `web/packages/ui`, Vite proxying, TanStack Router/Query baseline.
- Orchestration: Aspire topology with PostgreSQL, Redis, Keycloak, Mailpit, Host, Migrator, Vite apps.
- Verification: architecture tests, focused host/module tests, basic web tests, e2e smoke path when auth is runnable.

Cross-agent coordination rules:

- Keep write scopes disjoint where possible.
- Do not introduce product domain concepts beyond Identity/Auth unless explicitly approved.
- Do not wire Rebus/outbox runtime services until a concrete async workflow exists.
- Do not commit generated EF migrations before naming is stable.
- If implementation pressure changes a planning choice, update `docs/template/template-decisions.md` and the affected stable architecture docs in the same change.
