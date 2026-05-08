# Repository Governance

This document contains the durable project rules for this repository. It
replaces the former Spec Kit constitution as the tool-neutral governance source.

## Core Principles

### I. Domain-Neutral Product First

This repository MUST remain a reusable .NET and React modular-monolith
foundation. Changes MUST NOT introduce unrelated sample business workflows,
unchecked generated migrations, production secrets, or provider-bound
authorization decisions.

### II. Reviewed Runtime Surface

Substantial runtime behavior MUST start from accepted spec-driven development
artifacts or a durable architecture decision before code is added.
Auth/session plumbing, persistence behavior, frontend apps, orchestration
resources, CI workflows, and generated clients MUST NOT be introduced without
accepted feature artifacts or a durable architecture decision that states the
scope.

The default SDD workflow is OpenSpec. Accepted current behavior lives under
`openspec/specs/`; active proposed changes live under `openspec/changes/`.

### III. Durable Decisions Live In The Repository

Durable project knowledge MUST live in versioned repository files. Stable
architecture rules belong under `docs/`; accepted feature behavior belongs in
`openspec/specs/`. Agent instructions MAY summarize or route to those files, but
MUST NOT be the only source of an important rule.

### IV. Explicit Modular-Monolith Boundaries

Backend modules MUST preserve visible dependency direction. Module contracts
MUST NOT expose EF entities, aggregate internals, provider SDK types, or
infrastructure details. Module implementations MUST NOT use another module's
DbSet surface directly. Host composition MAY wire modules together, but
business behavior MUST stay in module-owned contracts and feature slices.

### V. Verification Scales With Risk

Every behavior or infrastructure change MUST leave the repository in a
verifiable state. Narrow infrastructure or documentation changes MAY rely on
restore, build, and formatting checks. Runtime behavior, shared abstractions,
persistence, auth/session flows, frontend workflows, and generated clients MUST
include tests or explicit verification steps proportionate to their blast
radius.

## Repository Constraints

- Specs and plans MUST load repository context before making design choices:
  this document, `openspec/specs/`, `docs/architecture.md`, relevant
  `docs/architecture/*.md`, relevant `docs/platform/*.md`, `docs/testing.md`,
  and relevant testing detail docs when verification is in scope.
- The canonical stack and architecture defaults live in `docs/architecture`
  and related platform docs. OpenSpec artifacts MUST follow those docs unless
  the change explicitly proposes and justifies a different durable direction.
- Product authorization MUST be application-owned and provider-neutral.
  Identity-provider roles, groups, organizations, or provider-specific claims
  MUST NOT be authoritative for product authorization.
- Durable intermodule messaging, outbox processing, orchestration resources,
  CI workflows, and generated migrations MUST remain out of scope unless
  accepted feature artifacts or durable architecture decisions state their
  scope.

## Development Workflow

Substantial behavior MUST start from an OpenSpec change before code is added.
Durable architecture decisions MUST first be recorded in the
relevant stable architecture, platform, testing, module, or governance doc.
OpenSpec planning MUST prefer repository docs as context over duplicating long
architecture guidance in prompts.

When a change is completed and accepted, archive it so accepted behavior is
merged into `openspec/specs/` before the next related change begins.

Product repositories MAY amend this governance document, but any amendment MUST
preserve an explicit decision trail and update affected docs, templates, and
task workflows in the same change.

## Governance Changes

This document supersedes conflicting generated instructions and local agent
memory for repository work. If an OpenSpec change, task list, or implementation
conflicts with a MUST rule here, the artifact or implementation MUST change
unless this document is explicitly amended first.

Amendments require:

- a documented rationale;
- updates to affected docs, OpenSpec artifacts, and agent instructions;
- a verification note describing which checks were run or why checks were not
  applicable.
