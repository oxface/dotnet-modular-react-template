# Testing

Testing is part of the template contract. The template should verify both
application behavior and template mechanics.

Area details:

- [Server testing](testing/server.md)
- [Web testing](testing/web.md)
- [E2E testing](testing/e2e.md)
- [Eval testing](testing/eval.md)

Current validation entrypoints:

- `dotnet test ModularTemplate.slnx`
- `pnpm frontend:typecheck`
- `pnpm frontend:test`
- `pnpm frontend:build`
- `pnpm frontend:lint`
- `pnpm api-client:check`

`pnpm api-client:check` is also wired into the local Husky pre-commit hook and
should be included in future CI workflows.
