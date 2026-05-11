# Agent Index

This repository is the maintenance home for `dotnet-modular-react-template`,
an npm CLI that bootstraps a domain-neutral .NET + React modular-monolith
product repository.

Start with:

- [README.md](README.md) for the factory purpose, local bootstrap commands, and
  verification entrypoints.
- [docs/README.md](docs/README.md) for factory maintainer documentation.
- [docs/template-decisions.md](docs/template-decisions.md) for durable template
  factory decisions.
- [docs/release-readiness.md](docs/release-readiness.md) and
  [docs/to_test.md](docs/to_test.md) for release validation context.
- [scripts/README.md](scripts/README.md) before changing bootstrap or
  verification automation.
- [template/AGENTS.md](template/AGENTS.md) before changing generated-product
  behavior under `template/`.

## Operating Rules

- Do not vibe-code. Read the relevant docs and existing implementation first,
  state the intended change, then make the smallest coherent edit.
- Implement gradually. Prefer narrow, reviewable steps over broad rewrites, and
  verify each risky step before expanding scope.
- Do not run git commands directly. If repository history, status, diffs,
  branches, commits, tags, or pushes are needed, ask the user to run the git
  operation or provide the relevant output.
- Do not overwrite or revert user changes. Work with the current files and ask
  when local edits make the requested change ambiguous.
- Keep generated artifacts, packed tarballs, migrations created during manual
  tests, and temporary bootstrap outputs out of committed source unless the user
  explicitly asks to preserve them.

## Repository Boundaries

- Root files maintain the template factory: npm package metadata, bootstrap
  scripts, factory tests, release workflows, changelog, and maintainer docs.
- `template/` is the product repository payload copied into new generated
  repositories.
- Root `docs/` are factory-maintenance context and should not be copied into
  generated-product documentation.
- Product-facing docs, OpenSpec specs, runtime source, product CI, and product
  governance belong under `template/`.
- When changing bootstrap behavior, consider both the source payload in
  `template/` and the transform logic in [scripts/bootstrap-template.js](scripts/bootstrap-template.js).

## Common Commands

Use Node 24 and pnpm 10.33.x.

- `pnpm verify` runs the normal factory verification suite.
- `pnpm scripts:lint` lints factory scripts.
- `pnpm scripts:test` runs focused Node tests for factory scripts.
- `pnpm template:verify:dry-run` checks bootstrap name derivation without
  writing an output repository.
- `pnpm template:verify` checks focused generated-repository bootstrap output.
- `pnpm template:verify:full` runs full generated-product verification and may
  require Docker or a Docker-compatible Podman socket.

Prefer the narrowest command that validates the change. Run `pnpm verify` for
bootstrap or payload-layout changes when practical, and reserve
`pnpm template:verify:full` for release readiness or runtime changes with a
larger blast radius.

## Bootstrap Notes

The public bootstrap interface is:

```sh
pnpm template:bootstrap -- --product-name "Acme Desk" --output ../acme-desk
```

The published CLI exposes the same flow as `dotnet-modular-react-template`.
The bootstrap command derives `AcmeDesk`, `acme-desk`, `acme_desk`,
`@acme-desk`, and `Acme Desk` from one display-oriented product name.

When editing rename or placeholder behavior, update or add focused coverage in
`scripts/bootstrap-template.test.js` and keep the manifest in
`scripts/bootstrap-template.js` explicit.

## Generated Product Rules

Before adding substantial runtime behavior under `template/`, read
`template/docs/governance.md`, the relevant stable docs in `template/docs/`,
and current specs in `template/openspec/specs/`.

Do not add domain behavior, auth/session plumbing, migrations, frontend apps,
or orchestration resources unless accepted specs or durable decisions define
the scope. Productized changes should be intentional because generated
repositories inherit them.
