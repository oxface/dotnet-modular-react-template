# Testing

Testing is part of the product contract. The repository should verify
application behavior, generated clients, and durable architecture decisions.

Area details:

- [Server testing](testing/server.md)
- [Web testing](testing/web.md)
- [E2E testing](testing/e2e.md)
- [Eval testing](testing/eval.md)

Validation entrypoints:

- `dotnet restore ModularTemplate.slnx`
- `dotnet build ModularTemplate.slnx --configuration Release --no-restore`
- `dotnet test ModularTemplate.slnx`
- `pnpm frontend:typecheck`
- `pnpm frontend:test`
- `pnpm frontend:build`
- `pnpm frontend:lint`
- `pnpm scripts:lint`
- `pnpm api-client:check`

`pnpm api-client:check` is also wired into the local Husky pre-commit hook and
the default CI workflow.

The default CI workflow lives at `.github/workflows/verify.yml`, is named
`Verify`, and runs on pull requests and pushes to `main`. It runs backend
restore, build, filtered tests with coverage collection, frontend validation,
and generated API client drift checks. Aspire/browser automation is outside the
default CI surface.
