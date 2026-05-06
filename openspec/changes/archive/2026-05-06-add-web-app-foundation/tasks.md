## 1. Workspace And Tooling

- [x] 1.1 Add frontend package manifests for `web/apps/admin`, `web/apps/web`, and required shared packages.
- [x] 1.2 Add Vite, React, TanStack Query, TanStack Router, Tailwind, React Testing Library, jsdom, and related frontend dependencies.
- [x] 1.3 Add root pnpm scripts for frontend build, test, typecheck, and lint where supported by the initial tooling.
- [x] 1.4 Add shared TypeScript/Vite/Tailwind configuration files using the repository's existing formatting conventions.

## 2. Shared Auth Package

- [x] 2.1 Create `web/packages/auth` with TypeScript contracts for the accepted `GET /api/me` response shape.
- [x] 2.2 Implement current-user loading through same-origin `GET /api/me` with explicit handling for `401 Unauthorized`.
- [x] 2.3 Implement login and logout helpers that navigate through same-origin Host auth routes.
- [x] 2.4 Add reusable access-state helpers or guard primitives for unauthenticated, no-access, and has-access states.
- [x] 2.5 Ensure browser auth code does not read, store, or send identity-provider access or refresh tokens.

## 3. App Shells

- [x] 3.1 Create the `web/apps/admin` React/Vite app shell with TanStack Query and Router bootstrap.
- [x] 3.2 Create the `web/apps/web` React/Vite app shell with TanStack Query and Router bootstrap.
- [x] 3.3 Wire both apps to consume shared auth/access-state helpers.
- [x] 3.4 Add domain-neutral authenticated, unauthenticated, loading, error, and no-access states.
- [x] 3.5 Configure each Vite app to proxy `/api/` and `/auth/` requests to the local Host target.

## 4. Tests

- [x] 4.1 Add tests for current-user success and `401 Unauthorized` handling in `web/packages/auth`.
- [x] 4.2 Add tests for login and logout navigation helpers.
- [x] 4.3 Add tests for unauthenticated, authenticated-without-access, and authenticated-with-access guard behavior.
- [x] 4.4 Add app-level smoke tests that verify each app shell renders without a real identity provider.

## 5. Documentation And Verification

- [x] 5.1 Update web architecture/testing docs with any durable package, script, or local development conventions introduced by the implementation.
- [x] 5.2 Run `pnpm format:check`.
- [x] 5.3 Run frontend build, test, typecheck, and lint scripts added by this change.
- [x] 5.4 Run `openspec validate add-web-app-foundation --strict`.
