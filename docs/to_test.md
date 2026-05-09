# Release Test Plan

Use this checklist after the current factory changes are committed. The goal is
to prove the template payload works locally, the packed CLI bootstraps a clean
product, and the published npm package works from a separate repository.

## 1. Confirm Factory Baseline

From the factory root:

```sh
node -v
pnpm exec node -v
pnpm verify
pnpm template:verify:full
```

Expected:

- `node -v` reports Node 24.15.0 or newer. This matters for `pnpm pack` and
  `npm publish`, because package-manager commands check package `engines` and
  `devEngines` with the ambient Node runtime that launched them.
- `pnpm exec node -v` reports Node 24.15.0 or newer.
- `pnpm verify` passes.
- `pnpm template:verify:full` passes against Docker or Podman.

If `node -v` is not Node 24, activate Node 24 in the shell before release
testing with your local version manager, then re-run the checks above.

For rootless Podman on Linux, set:

```sh
systemctl --user enable --now podman.socket

export DOCKER_HOST="unix://${XDG_RUNTIME_DIR}/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true
export ASPIRE_CONTAINER_RUNTIME=podman
```

## 2. Run The Template Payload Locally

From `template/`:

```sh
rm -rf server/src/ModularTemplate.Persistence/Migrations
pnpm install --frozen-lockfile
dotnet tool restore
dotnet restore ModularTemplate.slnx
```

Create the initial product-owned EF migration:

```sh
DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
  dotnet ef migrations add InitialCreate \
  --project server/src/ModularTemplate.Persistence/ModularTemplate.Persistence.csproj \
  --startup-project server/src/ModularTemplate.Host/ModularTemplate.Host.csproj \
  --context ModularTemplateDbContext \
  --output-dir Migrations
```

Start Aspire:

```sh
ASPIRE_CONTAINER_RUNTIME=${ASPIRE_CONTAINER_RUNTIME:-docker} \
  aspire start --apphost orchestration/ModularTemplate.Orchestration/ModularTemplate.Orchestration.csproj --isolated
```

Expected:

- PostgreSQL, Redis, Keycloak, Migrator, Host, Admin, and Web resources start.
- Migrator applies the initial migration.
- Admin and Web apps load.
- Browser auth smoke works with:
  - `admin@example.test` / `Password123!`
  - `user@example.test` / `Password123!`
- Logout returns the app to an unauthenticated state.

After this test, remove the generated migration before returning to factory
maintenance:

```sh
rm -rf server/src/ModularTemplate.Persistence/Migrations
```

## 3. Test The Local Packed CLI

From the factory root:

```sh
rm -rf template/server/src/ModularTemplate.Persistence/Migrations
mkdir -p /tmp/dotnet-modular-react-template-pack
pnpm pack --pack-destination /tmp/dotnet-modular-react-template-pack

pnpm dlx /tmp/dotnet-modular-react-template-pack/dotnet-modular-react-template-0.0.0.tgz \
  -- --product-name "Smoke Desk" --output /tmp/smoke-desk
```

In `/tmp/smoke-desk`:

```sh
pnpm install --frozen-lockfile
pnpm verify
```

Then repeat the initial migration and Aspire startup from section 2, using the
renamed solution and project paths generated for `Smoke Desk`.

Expected:

- The generated repository contains product files at its root, not under
  `template/`.
- Names are rewritten to `SmokeDesk`, `smoke-desk`, and `smoke_desk`.
- Product CI, Husky hooks, VS Code/Aspire config, docs, and OpenSpec artifacts
  are present.
- Factory-only docs and packaging files are absent.
- The generated product starts locally from Aspire.

## 4. Publish And Test From npm

Before publishing:

- Create or sign into an npm account.
- Confirm `dotnet-modular-react-template` is available.
- Configure npm trusted publishing for the package and GitHub Actions workflow.
  Use repository `The-Supremacy/net-react-modular-template` and workflow
  filename `release-please.yml` for automated releases. The manual retry
  workflow uses `publish-npm.yml`.
- Let Release Please create the release PR and set the first real version.

After the Release Please workflow creates the GitHub release and publishes the
npm package, test from a separate throwaway directory outside the factory repo:

```sh
mkdir -p /tmp/npm-template-consumer
cd /tmp/npm-template-consumer

pnpm dlx dotnet-modular-react-template -- --product-name "Published Desk" --output ./published-desk
cd published-desk
pnpm install --frozen-lockfile
pnpm verify
```

Then create the initial EF migration and start Aspire as in section 2.

Expected:

- `pnpm dlx dotnet-modular-react-template` resolves from npm.
- The generated repository behaves the same as the local packed CLI output.
- Verify, migration creation, Aspire startup, auth smoke, hooks, and inherited
  CI files all work.

## 5. Release Decision

Ship v1 only after these pass:

- Factory verification passes.
- Template payload runs locally from Aspire.
- Local packed CLI creates a working product.
- Published npm package creates a working product from a separate directory.
- Any generated EF migration used during testing is intentionally removed from
  the factory payload before release.
