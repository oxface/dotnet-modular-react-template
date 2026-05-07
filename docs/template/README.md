# Template Plan

This page tracks the next template lanes at a durable but lightweight level.
Accepted runtime behavior still lives under `openspec/specs/`, and proposed
behavior starts under `openspec/changes/`.

Related planning files:

- [current-state.md](current-state.md) records the reconciled implemented
  checkpoint.
- [implementation-plan.md](implementation-plan.md) records the broader forward
  plan and historical context.
- [template-decisions.md](template-decisions.md) records template-building
  decisions that should not become inherited product ADR history.

## Current Foundation

Accepted behavior currently covers:

- Host-owned OIDC browser authentication and Redis-backed session tickets.
- Provider-neutral current-user and application-access behavior.
- `GET /api/me` response semantics.
- Local Keycloak, Redis, PostgreSQL, Migrator, Host, admin frontend, and web
  frontend orchestration.
- Admin and web React app shells with shared browser-safe auth helpers.
- Frontend loading, unauthenticated, no-access, has-access, and error states.
- Same-origin frontend `/api/` and `/auth/` proxying for local development.
- Generated Host API client package consumed by frontend auth helpers.
- Domain-neutral browser-session smoke surface in both frontend apps.
- MVP 1 query helper decision: keep app-facing TanStack Query composition in
  `web/packages/auth` and defer Hey API generated query helpers.

## Next Lanes

### Technical Cleanup

These changes can usually be handled directly when they do not alter runtime
behavior:

- Documentation freshness and broken reference cleanup.
- Formatting, restore, build, test, and lint fixes.
- Verification-only changes that do not add new behavior.
- Removing stale planning references that conflict with accepted specs.

### Requires OpenSpec Or Durable Architecture Decision

These lanes need accepted artifacts before implementation:

- Generated TanStack Query helpers from Hey API, after additional Host API
  operations prove the app-facing query shape.
- Shared UI package conventions beyond simple scaffolding.
- CI workflow definition.
- Generated migrations.
- Template automation for creating product repositories.
- Mailpit or other local service resources.
- Durable intermodule messaging and outbox processing.

## Suggested Next Gate

Review checkpoint on 2026-05-07: the browser-session smoke surface and MVP 1
query helper decision are implemented through OpenSpec change
`add-browser-session-smoke-and-query-decision`.

The next lightweight gate is planning-material reconciliation and pruning. Pure
documentation freshness, stale-reference cleanup, formatting, restore, build,
test, lint, and generated-client drift checks can continue directly when they
do not change runtime behavior.

The consolidated MVP 1 remaining-step list now lives in
[implementation-plan.md](implementation-plan.md#mvp-1-remaining-steps). Add new
candidate steps there first, then promote implementation-sized runtime work
into OpenSpec changes when the scope is ready.
