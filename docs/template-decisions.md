# Template Decisions

This file records decisions made while building this template repository. It is
maintenance context for the template itself and is not intended to become
inherited product ADR history.

Stable product-facing rules belong in `template/docs/`, and accepted behavior
contracts belong in `template/openspec/specs/`.

## Implemented Direction

- Keep the factory at the repository root and the generated-product payload
  under `template/`.
- Use a generated-product monorepo with `server`, `web`, `orchestration`,
  `docs`, `deploy`, and `scripts` roots.
- Use `ModularTemplate` as the placeholder .NET namespace and package prefix.
- Keep the root solution in `.slnx` format as `ModularTemplate.slnx`.
- Use OpenSpec as the default spec-driven development workflow for accepted
  runtime and product-facing behavior contracts.
- Keep hard product governance in `template/docs/governance.md`.
- Use a shared Host-owned EF Core DbContext in `ModularTemplate.Persistence`,
  with narrow module DbContext interfaces to preserve module boundaries.
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
- Keep app-facing TanStack Query composition in `web/packages/auth`; generated
  TanStack Query helpers are not enabled in the API-client generator.
- Use a default GitHub Actions workflow named `Verify` for pull requests and
  pushes to `main`.
- Keep OpenSpec validation in its own CI job.
- Run explicit backend restore, build, and filtered `Unit`/`Application` test
  steps in CI, with backend test coverage collected and uploaded as a workflow
  artifact.
- Keep focused bootstrap verification in factory CI. Reserve full
  generated-product verification for release readiness.
- Provide out-of-place template rename/bootstrap automation that accepts one
  product name and output path, derives all first-version naming forms, and
  verifies a temporary generated repository.
- Keep root `pnpm` scripts as the public template automation interface.
- Keep template automation internals as Node `.js` scripts so the scripts are
  easier to package and test later.
- Treat product creation as copy from `template/` plus explicit
  rename/bootstrap automation.
- Keep template-factory planning context directly under root `docs/`.
- Publish the factory root as the `dotnet-modular-react-template` npm CLI
  package. The package ships the generated-product payload and exposes
  `dotnet-modular-react-template` as the bootstrap command.

## Naming Model

The bootstrap command accepts one display-oriented product name and derives the
first-version naming forms:

- Input display name: `Acme Desk`
- .NET namespace, project, and solution prefix: `AcmeDesk`
- npm package scope and local service slug: `@acme-desk` and `acme-desk`
- database names: `acme_desk`
- visible app/docs display text: `Acme Desk`
