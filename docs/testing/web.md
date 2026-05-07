# Web Testing

The frontend target uses Vitest, React Testing Library, and Playwright.

Shared frontend packages should include focused tests for auth helpers, route
guards, generated API client behavior, and reusable UI behavior once those
packages exist.

Current frontend validation entrypoints:

- `pnpm api-client:check`
- `pnpm frontend:typecheck`
- `pnpm frontend:test`
- `pnpm frontend:build`
- `pnpm frontend:lint`

`pnpm api-client:check` is wired into the local Husky pre-commit hook and should
be part of future CI workflows.
