## Why

The backend auth/session slice now exposes a stable cookie-based `/api/me`
contract, but the template has no browser app surface to consume it. Adding the
first frontend foundation makes the BFF session model visible and gives future
product work a reusable app/package shape.

## What Changes

- Add initial React/Vite app shells for `web/apps/admin` and `web/apps/web`.
- Add shared browser auth helpers that call same-origin Host auth routes and
  `GET /api/me` without storing identity-provider tokens.
- Add frontend routing/query foundations that model unauthenticated,
  authenticated-without-access, and authenticated-with-access states.
- Add local development proxy behavior for `/api/` and `/auth/` requests to the
  Host.
- Add focused frontend tests for current-user loading and route/access guard
  behavior.
- Leave generated OpenAPI clients, provisioning workflows, and CI workflows for
  later scoped changes.

## Capabilities

### New Capabilities

- `web-app-foundation`: Browser application foundation for the admin and web
  apps, including shared auth/session helpers and current-user consumption.

### Modified Capabilities

- None.

## Impact

- Adds frontend workspace packages and app source under `web/apps/*` and
  `web/packages/*`.
- Updates root pnpm scripts and frontend package dependencies as needed for
  build and test entrypoints.
- Updates web architecture/testing docs if implementation choices need durable
  clarification.
- Does not change backend API routes, auth/session semantics, Identity
  behavior, orchestration resources, generated clients, or CI workflows.
