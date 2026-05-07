## Why

The template now has a stable initial Host API contract and frontend app
foundation, but browser code still hand-maintains the `/api/me` client shape.
Generated API clients are the next gate because they make API/frontend contract
drift visible before the template grows additional endpoints.

## What Changes

- Add Host OpenAPI generation for the accepted API surface.
- Add a generated frontend API client package under `web/packages/`.
- Wire frontend auth/current-user helpers to consume the generated client while
  preserving same-origin browser calls.
- Add a repeatable client generation workflow and verification commands.
- Document the generation boundary, package shape, and update workflow.

## Capabilities

### New Capabilities

- `generated-api-clients`: Defines OpenAPI publication, generated frontend
  client package behavior, same-origin browser constraints, and verification for
  generated clients.

### Modified Capabilities

- `web-app-foundation`: Frontend current-user loading must use the generated
  client package instead of hand-maintained response/client types.

## Impact

- Host API/OpenAPI configuration and package references.
- Current-user endpoint metadata and response typing where needed for OpenAPI.
- New frontend workspace package for generated API clients.
- Existing `web/packages/auth` current-user loading.
- Frontend app/package typecheck, test, build, lint, and formatting scripts.
- Stable web/server/template documentation.
