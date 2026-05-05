# Template Decision Ledger

Status: Template planning artifact

Location note: this file lives under `docs/template/` so future scaffold/cleanup scripts can ignore or remove template-construction context without touching product-facing architecture docs.

This file records decisions made while building the template itself. It is not intended to be inherited as a product ADR history. A future scaffold/rename script should either remove this file or replace it with a fresh product-specific decision system.

Architecture docs describe the current template shape. This ledger records the reasoning trail that led there.

## Accepted Decisions

### Use A Monorepo Shape

Use top-level roots for `server/`, `web/`, `orchestration/`, `docs/`, `deploy/`, and `scripts/`.

Reason: backend, frontend, orchestration, docs, and automation should evolve together during educational rebuilds.

### Use Thin Architecture Indexes

Use `docs/architecture.md` as a high-level index and `docs/architecture/*.md` for detailed area guidance.

Reason: this gives agents and humans an obvious starting point without duplicating the same rules in multiple places.

### Do Not Ship Template ADRs

Do not include an ADR folder as a first-class inherited template artifact.

Reason: copied product repositories should not inherit a stale list of decisions that were about template construction rather than the new product.

### Use Lower-Case Module Folder

Use `server/src/modules`.

Reason: it reads like a repository folder, not a .NET namespace layer.

### Start With A Shared Host DbContext

Use one Host-level EF Core DbContext with module-owned schemas and narrow module DbContext interfaces.

Reason: same-transaction workflows are a major modular-monolith benefit. Durable messaging, sagas, and choreography should be introduced only when a concrete workflow needs them.

Guardrails:

- Module stores inject only their narrow `I<Module>DbContext`.
- Modules do not use another module's DbSet surface.
- Host owns migrations and a `.Migrator` project applies them.
- Architecture tests enforce project references and forbidden namespace access.

### Use Separate Module Infrastructure Projects

Use three projects for modules with persistence or external adapters:

```text
{Product}.{Module}.Contracts
{Product}.{Module}
{Product}.{Module}.Infrastructure
```

Reason: the project boundary keeps EF Core, provider SDKs, Keycloak/Kiota clients, and adapter details out of the module's domain/application assembly. It is more ceremony, but it gives agents and humans a clearer dependency rule.

### Persist Domain Events In An Event Log

Persist aggregate-raised domain events to `event_log.domain_events`.

Reason: the table is not only an audit table. It is durable domain history and a future replay/projection source for external events, integration events, and new consumers. A later outbox can project from this log into transport-specific messages.

### Validate Domain Event Metadata

Use explicit event metadata with module name, logical event name, and version. Add registry or architecture-test validation for missing metadata and duplicate event names.

Reason: replay and projection need stable logical event names. Module ownership must be explicit and must not be inferred from namespaces.

### Keep Auth Mechanics In Host

Host owns ASP.NET Core authentication, OIDC challenge/callback/logout, cookie configuration, Redis ticket storage, and API redirect suppression.

Reason: browser session mechanics are a platform boundary, not a business module.

### Keep Identity And Authorization Data In Identity

Identity owns local `UserIdentity`, lazy user creation, current-user context, and app-owned staff/admin authorization records.

Reason: Keycloak proves identity; the app owns product authorization.

### Treat Keycloak As OIDC Provider

Do not use Keycloak roles, groups, or organization membership as authoritative product authorization.

Reason: Keycloak is the local-first identity provider and may not be the real production provider. Product authorization should remain provider-neutral.

### Use Redis For BFF Session Tickets

Redis is part of the initial Aspire topology.

Reason: server-side OIDC tokens and session tickets are part of the target BFF architecture, and Aspire makes the local dependency acceptable.

### Start With Admin And Web Apps

Create `web/apps/admin` and `web/apps/web`, plus shared packages such as `web/packages/auth`.

Reason: admin is a known app-owned capability. `web` is a neutral user-facing portal name that avoids domain-specific terms such as workspace.

### Bootstrap One Initial Admin

Begin with Aspire/local identity-provider setup, lazy local user creation, and one bootstrapped application admin.

Reason: product authorization belongs to the app. The first admin can use the admin portal to provision actual application data and users. Identity-provider users can authenticate, but they do not receive product access until app-owned authorization records exist.

### Generate OpenAPI Client In First Delivery

Include OpenAPI client generation in the first delivery once the initial auth/current-user endpoint shape exists.

Reason: generated clients reduce drift between Host APIs and frontend packages. The setup should happen after the first endpoint contracts settle enough to avoid reworking generator configuration repeatedly.

### Use Copy-Paste Plus Rename Script First

Start with a script for repeatable rename chores, and wrap it in a skill for agent verification.

Reason: scripts are better for deterministic replacement; skills are better for build/test repair and missed-reference inspection.

The first script version should accept only a product name and derive slugs deterministically. Add explicit slug overrides later only when real product names prove the derived form is wrong.

### Include Dependabot And Release Please

Include Dependabot and Release Please in the automation baseline.

Reason: dependency and release hygiene should be present from the first scaffold, especially with central NuGet and pnpm workspace management.

### Use Stepwise Implementation Gates

Build the template in small reviewable steps. Each step should update docs, code, and tests only for its scoped concern.

Reason: the previous broad implementation style produced too many files too quickly. This template should be built slowly enough that each decision can be reviewed, discussed, and corrected before later agents depend on it.

## Pending Decisions

### Rename Script Coverage

Question: exactly how much should the first rename script cover?

Current target:

- .NET namespaces, project names, and `.slnx` solution references.
- pnpm package scopes and names.
- Aspire resource names.
- Docker/image names if present.
- Identity-provider realm/client names.
- README and docs references.

The script should be aggressive enough that folder renaming is not a manual project, but conservative enough to avoid corrupting unrelated prose or generated files.

### Template Evolution Workflow

Question: how should improvements discovered in real product repositories flow back into the template?

Current idea: use an explicit change packet workflow rather than ad hoc copying.

Possible skills:

- `template-change-export`: run inside a product repository after an explored improvement stabilizes. It gathers the problem, decision, affected files, docs, tests, commands, and migration notes into a structured change packet.
- `template-change-import`: run inside this template repository. It reads the packet, compares it with current template decisions/docs, applies the relevant generalized changes, and updates template docs.
- `template-maintain`: run manually in the template repository to check placeholder consistency, docs consistency, bootstrap/rename behavior, generated-repo smoke tests, and stale planning artifacts.

The packet should be reviewable text, not an automatic patch that blindly applies. A product implementation may include domain-specific compromises that should not enter the template unchanged.

Candidate first use case: a product module requires durable communication, so the product explores outbox/Rebus/event-log projection. Once proven, export a packet that explains the stable generic pattern and import it into the template.

Import and maintain tools must be manual-only. They must not automatically upgrade product repositories from the template. As products grow independently, applying every new template pattern can be risky and wrong. Product repositories should pull template improvements deliberately, one scoped change at a time, with tests and review.

Manual packet flow:

1. Run `template-change-export` in a product repository.
2. The product repository writes a packet under a local folder such as `.template-changes/`.
3. Manually copy that packet into the template repository under `docs/template/inbox/`.
4. Run `template-change-import` in the template repository.
5. The import skill reads the packet, helps generalize the change, updates template docs/code/tests, and asks for review.
6. After the template change is accepted, move the packet to `docs/template/archive/` or delete it as part of the same reviewed change.

The template should not publish an automatic "template update available" mechanism for product repositories in the first version. A product can record which template version or commit initialized it, but pulling newer template patterns into a product remains a deliberate human-guided port.

### Devcontainer Strategy

Question: should the template include a `.devcontainer` by default?

Current leaning: yes, but with documentation that AI-agent history/state may live outside the container or be lost during rebuilds depending on the tool. The devcontainer should optimize onboarding without making local non-container development second-class.

## Decision Hygiene

This section applies only while building the template.

When a pending decision becomes accepted:

- Move it from `Pending Decisions` to `Accepted Decisions`.
- Update `docs/template/implementation-plan.md` if scaffold order, module shape, or handoff expectations changed.
- Update product-facing architecture docs once they exist.
- Keep reasoning concise; avoid turning this file into a duplicate architecture guide.

When a decision becomes product-specific rather than template-specific:

- Remove it from this ledger.
- Put the current-state rule in the relevant architecture/platform/testing/module doc.
- Let the generated product create ADRs only if that product wants ADRs.
