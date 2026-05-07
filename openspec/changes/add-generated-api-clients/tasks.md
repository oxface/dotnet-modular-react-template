## 1. Host OpenAPI Source

- [x] 1.1 Add central package versions and Host references for ASP.NET Core
      OpenAPI generation and build-time document generation.
- [x] 1.2 Configure the Host to register the template API OpenAPI document.
- [x] 1.3 Add endpoint metadata for `GET /api/me` so the generated operation and
      response schemas are stable and useful for frontend generation.
- [x] 1.4 Ensure browser auth routes are excluded from generated frontend API
      operations.

## 2. Frontend API Client Package

- [x] 2.1 Add `web/packages/api-client` to the pnpm workspace with package,
      TypeScript, and generator configuration.
- [x] 2.2 Add pinned frontend generator dependencies and scripts for generating
      the API client.
- [x] 2.3 Generate the initial API client from the Host OpenAPI document.
- [x] 2.4 Add a small hand-written package entrypoint/configuration layer that
      defaults generated browser calls to same-origin `/api/` routes.

## 3. Frontend Consumption

- [x] 3.1 Replace hand-maintained current-user fetch/types in
      `web/packages/auth` with the generated current-user client and types.
- [x] 3.2 Preserve login/logout helpers as same-origin browser navigations to
      Host-owned auth routes.
- [x] 3.3 Update auth package tests so current-user success and unauthenticated
      behavior are verified through the generated client boundary.
- [x] 3.4 Verify browser code does not read, store, or send identity-provider
      access tokens or refresh tokens.

## 4. Scripts And Documentation

- [x] 4.1 Add root scripts for OpenAPI/client generation and generated-client
      freshness checks.
- [x] 4.2 Add a Husky pre-commit hook for generated-client freshness checks.
- [x] 4.3 Update server/web architecture docs with the OpenAPI source, generated
      package shape, and same-origin browser constraints.
- [x] 4.4 Update template docs with the completed generated-client gate and the
      follow-up OIDC/browser-session smoke UI gate.

## 5. Verification

- [x] 5.1 Run OpenSpec strict validation for this change.
- [x] 5.2 Run backend restore, build, and tests.
- [x] 5.3 Run frontend typecheck, tests, build, lint, and formatting checks.
- [x] 5.4 Run the generated-client freshness check and confirm stale generated
      output fails validation.
