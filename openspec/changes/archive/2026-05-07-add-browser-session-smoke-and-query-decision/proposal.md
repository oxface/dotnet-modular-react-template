## Why

The template now has Host OIDC sessions, frontend app shells, Aspire-managed
frontend resources, and a generated Host API client, but it still lacks a small
browser-level smoke surface that proves those pieces work together through the
real local session boundary.

This is also the right point to make the MVP 1 TanStack Query helper decision:
the smoke surface should confirm whether the current template-owned query
wrapper is enough or whether Hey API generated query helpers should be adopted
now.

## What Changes

- Add a small domain-neutral smoke surface to both frontend apps for
  login/current-user/logout session verification.
- Keep browser calls on same-origin `/api/` and `/auth/` routes and keep
  identity-provider tokens out of browser code.
- Add browser-level smoke verification for the local Aspire platform path.
- Decide the MVP 1 generated TanStack Query helper direction and record it in
  docs/specs.
- Update template planning docs so steps 14 and 15 are no longer listed as open
  MVP 1 gates after the change is accepted.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `web-app-foundation`: add the browser-session smoke surface requirement for
  both frontend apps.
- `generated-api-clients`: record the MVP 1 generated TanStack Query helper
  decision.
- `frontend-orchestration`: add browser-level smoke verification expectations
  for the local Aspire platform session path.

## Impact

- `web/apps/admin` and `web/apps/web` app shells and tests.
- `web/packages/auth` shared session/query helpers and tests if needed.
- `web/packages/api-client` generation configuration only if the query helper
  decision requires it.
- Local platform/browser smoke test documentation or scripts.
- Template planning docs under `docs/template/`.
