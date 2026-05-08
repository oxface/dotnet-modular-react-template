# modular-template

Domain-neutral .NET + React modular-monolith product repository.

Substantial runtime behavior starts from accepted OpenSpec artifacts or durable
architecture decisions. Stable governance, architecture, platform, testing, and
module guidance lives under `docs/`.

Start with:

- [docs/README.md](docs/README.md) for stable product-facing documentation.
- [docs/governance.md](docs/governance.md) for hard project rules.
- [docs/openspec.md](docs/openspec.md) for the spec-driven development
  workflow.

## Initial Setup

Use Node 24 and pnpm 10.33.x for frontend and repository tooling.

Use either Docker or Podman for local container services. When using rootless
Podman on Linux, start the Podman socket and expose it to Testcontainers before
running backend verification:

```sh
systemctl --user enable --now podman.socket

export DOCKER_HOST="unix://${XDG_RUNTIME_DIR}/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true
export ASPIRE_CONTAINER_RUNTIME=podman
```

Generate the initial EF migration before using the local Aspire platform:

```sh
dotnet tool restore
dotnet restore ModularTemplate.slnx

DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
  dotnet ef migrations add InitialCreate \
  --project server/src/ModularTemplate.Persistence/ModularTemplate.Persistence.csproj \
  --startup-project server/src/ModularTemplate.Host/ModularTemplate.Host.csproj \
  --context ModularTemplateDbContext \
  --output-dir Migrations
```

The repository starts without generated EF migrations, and `.gitignore` does
not ignore migration folders so the product can track its own migration history.

Start the local platform:

```sh
ASPIRE_CONTAINER_RUNTIME=${ASPIRE_CONTAINER_RUNTIME:-docker} \
  aspire start --apphost orchestration/ModularTemplate.Orchestration/ModularTemplate.Orchestration.csproj --isolated
```

Use `aspire describe` to inspect resource status and endpoints after startup.

Useful commands:

- `pnpm verify`
- `pnpm verify:backend`
- `pnpm verify:frontend`
- `pnpm api-client:generate`
- `pnpm api-client:check`
- `pnpm scripts:lint`
- `pnpm frontend:typecheck`
- `pnpm frontend:test`
- `pnpm frontend:build`
- `pnpm frontend:lint`
