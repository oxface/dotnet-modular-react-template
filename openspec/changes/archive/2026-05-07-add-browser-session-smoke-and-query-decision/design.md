## Context

The accepted frontend foundation already has two React/Vite apps,
browser-safe auth helpers, same-origin `/api/` and `/auth/` routing, TanStack
Query composition in `web/packages/auth`, and generated SDK/types in
`web/packages/api-client`.

The missing MVP 1 gate is not a product workflow. It is a deliberately small
developer smoke surface that makes the local browser session path observable in
both apps: unauthenticated, authenticated without application access,
authenticated with application access, and logout.

## Goals / Non-Goals

**Goals:**

- Add an obvious domain-neutral session smoke surface to both frontend apps.
- Keep current-user API loading behind `web/packages/auth` and the generated
  current-user client.
- Keep login/logout as same-origin browser navigations to Host-owned auth
  routes.
- Add focused frontend tests for the visible smoke surface states.
- Record the MVP 1 decision for Hey API generated TanStack Query helpers.

**Non-Goals:**

- Add product workflows, sample domain data, roles, or provider-specific
  authorization UI.
- Store or expose identity-provider tokens in browser code.
- Add identity-provider Admin API provisioning.
- Add generated TanStack Query helpers in this MVP 1 gate.
- Add CI workflows for full browser/platform smoke execution.

## Decisions

### Keep Template-Owned Query Composition For MVP 1

Keep `web/packages/auth` as the app-facing TanStack Query boundary and continue
generating SDK/types only from Hey API for now.

Rationale: the only accepted Host API operation is current-user loading, and it
has app-specific semantics around `401` mapping, access-state resolution, and
same-origin session behavior. A template-owned wrapper is clearer than exposing
generated query helpers directly before there is a broader API shape.

Alternative considered: enable `@tanstack/react-query` generation immediately
in `web/packages/api-client/openapi-ts.config.ts`. That would prove the plugin
works, but it would also expose generator-shaped query APIs before the template
knows whether apps should consume them directly or through shared wrappers.

### Make The Smoke Surface Visible In The Existing Shell

Extend the existing admin and web shells rather than adding a hidden route.

Rationale: the first viewport stays useful to developers running the local
platform manually, and the existing tests can verify states without requiring
Keycloak startup.

Alternative considered: add a Playwright-only route. That would be cleaner for
automation but less useful for manual local inspection and would duplicate the
same state handling.

### Defer Full Local-Platform Browser Automation

Document the intended local-platform smoke path and keep automated coverage
focused on frontend behavior for this gate.

Rationale: Aspire, Keycloak login automation, local database state, and
application-access bootstrap are heavier than the existing MVP validation
entrypoints. This change should leave the repo verifiable without requiring a
long-running local platform in default checks.

Alternative considered: add a full Playwright suite that starts Aspire and
drives Keycloak. That belongs in a later CI/e2e gate once platform startup
expectations and test credentials are stable.

## Risks / Trade-offs

- Visible smoke UI could become accidental product UI -> keep labels
  domain-neutral and update planning docs to treat it as template verification
  surface.
- Deferring generated query helpers could require a later migration -> keep the
  decision explicit and revisit when additional Host API operations exist.
- Deferring full platform automation leaves manual risk -> preserve documented
  smoke steps and keep unit/component tests around state rendering.
