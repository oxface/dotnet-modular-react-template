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

- OIDC/browser-session smoke UI that consumes generated clients from both
  frontend apps.
- Generated TanStack Query helpers from Hey API, after the app-facing query
  shape is proven by the smoke UI.
- Shared UI package conventions beyond simple scaffolding.
- CI workflow definition.
- Generated migrations.
- Template automation for creating product repositories.
- Mailpit or other local service resources.
- Durable intermodule messaging and outbox processing.

## Suggested Next Gate

The next runtime gate should add a small domain-neutral smoke surface in both
apps that uses the generated clients for login/current-user/logout verification
against the real Host OIDC session path.

After that smoke gate, reconcile planning material before deleting it: preserve
durable guidance in stable docs, accepted OpenSpec specs, or template-only docs
that the future bootstrap script ignores. Then prune obsolete plan documents,
run code review, collect testing feedback, and review again.
