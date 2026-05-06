## Context

The repository currently has a pnpm workspace and frontend architecture docs,
but only `web/README.md` exists under the web tree. The backend has accepted
cookie-session auth, `/auth/login`, `/auth/logout`, and `GET /api/me`
semantics, so the first browser slice can consume the Host as a same-origin BFF
without introducing token storage or provider-specific behavior.

This change establishes the frontend app/package shape that later admin,
generated-client, and product UI work can build on.

## Goals / Non-Goals

**Goals:**

- Create buildable React/Vite shells for `web/apps/admin` and `web/apps/web`.
- Create shared browser auth/session helpers that consume same-origin Host
  routes and model the accepted `/api/me` response shape.
- Add route/query foundations for loading current-user state and distinguishing
  unauthenticated, authenticated-without-access, and authenticated-with-access
  UI states.
- Add local Vite proxying for `/api/` and `/auth/` to the Host.
- Add focused frontend tests for auth helpers and access guard behavior.

**Non-Goals:**

- Do not introduce generated OpenAPI clients.
- Do not add admin provisioning workflows, user management, product roles, or
  domain-specific screens.
- Do not change backend API/auth/session behavior.
- Do not add Aspire frontend resources, Docker images, CI workflows, or
  template rename automation.
- Do not store or expose identity-provider access or refresh tokens in browser
  code.

## Decisions

### Use Minimal App Shells With Shared Auth Package

Create `web/apps/admin` and `web/apps/web` as separate Vite React apps, and put
current-user/session helpers in `web/packages/auth`.

Alternatives considered:

- A single frontend app with mode-based routing would be less ceremony, but it
  would hide the intended admin/user-facing boundary.
- Duplicating auth code in both apps would make the first slice smaller but
  would invite drift around the BFF session rules.

### Keep API Consumption Hand-Written For This Slice

Represent the `/api/me` response with a small TypeScript contract in the auth
package and call it with `fetch`.

Alternatives considered:

- Adding OpenAPI generation now would reduce future drift, but the generator
  choice and generated package shape should be decided in a focused change.
- Importing backend DTOs directly is not viable across the .NET/TypeScript
  boundary and would blur the frontend API contract.

### Use Same-Origin Relative Routes

Browser helpers call `/api/me`, `/auth/login`, and `/auth/logout` using relative
paths. Local Vite config proxies `/api/` and `/auth/` to the Host.

Alternatives considered:

- Configuring provider URLs in the browser would expose identity-provider
  concerns and contradict the BFF boundary.
- Storing tokens client-side would conflict with accepted auth-session specs.

### Introduce Lightweight Guard Components/Utilities

The auth package should expose reusable loading/error/access-state utilities
that apps can compose into routes. The initial app screens can stay simple, but
the underlying states must be testable.

Alternatives considered:

- App-specific guards only would make the shell faster, but future apps would
  need to rediscover the same state model.
- A full design system and navigation framework is too broad for the first
  foundation slice.

## Risks / Trade-offs

- The hand-written `/api/me` TypeScript type can drift from the Host response
  shape. Mitigation: keep the contract tiny now and schedule generated OpenAPI
  client work as the next frontend/API contract slice.
- Two app shells introduce duplicated layout/bootstrap code early. Mitigation:
  only share auth/session behavior in this change; extract UI/layout primitives
  later when duplication is real.
- Route guard tests may overfit the first UI copy. Mitigation: test observable
  state transitions and commands rather than detailed layout text.
