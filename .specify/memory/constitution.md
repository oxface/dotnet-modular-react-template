<!--
Sync Impact Report
Version change: template -> 1.0.0
Modified principles:
- Placeholder principles replaced with template-specific governance.
Added sections:
- Template Constraints
- Development Workflow
Removed sections:
- Placeholder sections and examples from the default Spec Kit template.
Templates requiring updates:
- .specify/templates/plan-template.md: pending review during first feature spec
- .specify/templates/spec-template.md: pending review during first feature spec
- .specify/templates/tasks-template.md: pending review during first feature spec
Follow-up TODOs:
- Decide whether generated product repositories inherit this constitution
  unchanged or receive a product-bootstrap constitution during rename/setup.
-->

# Modular Template Constitution

## Core Principles

### I. Domain-Neutral Template First

This repository MUST remain a reusable .NET and React modular-monolith template.
Template work MUST NOT introduce product-specific domain behavior, sample
business workflows, generated migrations, production secrets, or provider-bound
authorization decisions unless a reviewed gate explicitly allows that scope.

Rationale: future products should inherit a clean starting point, not hidden
assumptions from an imagined sample domain.

### II. Reviewed Gates Before Runtime Surface

Implementation MUST proceed through small reviewed gates. A gate MUST state its
scope in repository documentation before it adds runtime behavior, persistence,
auth/session plumbing, frontend apps, orchestration resources, CI workflows, or
template automation. Work outside the active gate MUST be deferred or recorded
as a follow-up.

Rationale: the template is intended to be inspectable and teachable, and broad
unreviewed scaffolding makes later decisions depend on accidental structure.

### III. Durable Decisions Live In The Repository

Durable project knowledge MUST live in versioned repository files. Stable
architecture rules belong under `docs/`; template-construction reasoning belongs
under `docs/template/`; accepted feature behavior and task plans belong in Spec
Kit artifacts. Agent instructions MAY summarize or route to those files, but
MUST NOT be the only source of an important rule.

Rationale: humans and agents need the same durable source of truth after tool
state, containers, or local sessions are rebuilt.

### IV. Explicit Modular-Monolith Boundaries

Backend modules MUST preserve visible dependency direction. Module contracts
MUST NOT expose EF entities, aggregate internals, provider SDK types, or
infrastructure details. Module implementations MUST NOT use another module's
DbSet surface directly. Host composition MAY wire modules together, but business
behavior MUST stay in module-owned contracts and feature slices.

Rationale: the template should demonstrate modular-monolith benefits without
pretending the system is already a distributed architecture.

### V. Verification Scales With Risk

Every gate MUST leave the repository in a verifiable state. Narrow
infrastructure or documentation gates MAY rely on restore, build, and formatting
checks. Runtime behavior, shared abstractions, persistence, auth/session flows,
frontend workflows, and generated clients MUST include tests or explicit
verification steps proportionate to their blast radius.

Rationale: the template is a learning and restartability tool; broken baseline
verification undermines every generated product.

## Template Constraints

- The default backend stack is .NET 10, ASP.NET Core Minimal APIs, EF Core with
  PostgreSQL, a shared Host-owned DbContext with module-owned schemas, Mediator,
  and Aspire service defaults.
- The default frontend stack is React, Vite, TypeScript, pnpm workspaces,
  TanStack libraries, Tailwind, and shadcn-style components once frontend gates
  are reviewed.
- Keycloak MAY be used as the local OIDC provider, but product authorization
  MUST be application-owned and provider-neutral.
- Durable intermodule messaging, outbox processing, orchestration resources,
  CI workflows, generated migrations, and template automation MUST remain
  deferred until their gate is reviewed.
- Spec Kit is the default SDD tool. The approved initial extension set is
  Archive and Refine only.

## Development Workflow

Substantial behavior MUST start from Spec Kit feature artifacts before code is
added. Architecture and template-building decisions MUST first be recorded in
`docs/template/template-decisions.md` or the relevant stable architecture doc.
When a feature is completed and accepted, durable lessons SHOULD be archived
into Spec Kit memory before the next related feature begins.

Generated product repositories MAY amend this constitution after bootstrap, but
any amendment MUST preserve an explicit decision trail and update affected docs,
templates, and task workflows in the same change.

## Governance

This constitution supersedes conflicting generated instructions and local agent
memory for template work. If a Spec Kit spec, plan, task list, or implementation
conflicts with a MUST rule here, the artifact or implementation MUST change
unless this constitution is explicitly amended first.

Amendments require:

- a documented rationale;
- a semantic version bump;
- updates to affected docs, Spec Kit templates, and agent instructions;
- a verification note describing which checks were run or why checks were not
  applicable.

Versioning policy:

- MAJOR: removes or redefines a core principle.
- MINOR: adds a principle, required workflow, or new governed surface.
- PATCH: clarifies wording without changing obligations.

**Version**: 1.0.0 | **Ratified**: 2026-05-05 | **Last Amended**: 2026-05-05
