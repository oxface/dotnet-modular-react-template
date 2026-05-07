# Template Decisions

This file records decisions made while building this template repository. It is
planning context for the template itself and is not intended to become inherited
product ADR history.

Stable product-facing rules still belong in `docs/`, and accepted behavior
still belongs in `openspec/specs/`.

## Accepted Direction

- Use a monorepo with `server`, `web`, `orchestration`, `docs`, `deploy`, and
  `scripts` roots.
- Use `ModularTemplate` as the placeholder .NET namespace and package prefix.
- Keep the root solution in `.slnx` format as `ModularTemplate.slnx`.
- Use OpenSpec as the current spec-driven development workflow.
- Keep hard template governance in `docs/governance.md`.
- Use a shared Host-owned EF Core DbContext in `ModularTemplate.Persistence`,
  with narrow module DbContext interfaces to preserve module boundaries.
- Keep generated EF migrations out of the template until naming/bootstrap
  behavior is intentionally defined.
- Keep auth mechanics in the Host and local identity/application-access
  behavior in the Identity module.
- Use BFF-style browser sessions with Host-owned OIDC, application cookies, and
  Redis-backed server-side authentication tickets.
- Treat Keycloak as a local OIDC provider, not an application authorization
  source.
- Use Aspire as the local platform entrypoint.
- Start with separate `admin` and `web` React/Vite apps.
- Keep browser auth helpers in `web/packages/auth`; browser code must use
  same-origin app routes and must not store identity-provider tokens.
- Use `web/packages/api-client` as the generated TypeScript client package for
  template-owned Host API endpoints.
- Defer Hey API generated TanStack Query helpers until the next UI smoke gate
  proves whether apps should consume generated query helpers directly or through
  template-owned shared wrappers.
- Defer Scalar or other interactive API reference UIs until a dedicated local
  API documentation/testing gate defines the auth model.
- Defer durable intermodule messaging and outbox processing until a concrete
  workflow needs them.
- Defer Mailpit until a mail workflow exists.
- Defer CI workflows, Dependabot, Release Please, and template
  rename/bootstrap automation until their own accepted scopes exist.
- Add a later browser-session smoke surface in both frontend apps after
  generated clients exist, so the template exercises login, current-user, and
  logout through the real Host OIDC session path.

## Current Open Questions

- Should the OIDC/browser-session smoke surface be a tiny visible app section,
  Playwright-only route, or both?
- Should Hey API generated TanStack Query helpers be imported directly by apps,
  or wrapped by shared frontend packages?
- If a future local API reference UI is added, should it use the existing BFF
  cookie session, a separate OIDC client, or remain unauthenticated
  documentation only?
- What exact paths, package names, service names, realm/client names, and docs
  should rename automation modify?
- Should generated product repositories inherit `docs/governance.md` unchanged
  or receive product-specific bootstrap governance?
- What should the first template-change export/import packet schema include?
