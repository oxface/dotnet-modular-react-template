# Web Architecture

The frontend is a pnpm workspace with React, Vite, TanStack Query, TanStack
Router, Tailwind, and shared packages for auth helpers, configuration, and
generated API clients.

- `web/apps/admin` is the app-owned administration portal.
- `web/apps/web` is the neutral user-facing portal.
- `web/packages/auth` owns browser-safe BFF session helpers, current-user
  loading, access-state utilities, app-facing TanStack Query composition, and
  the domain-neutral browser-session smoke surface. Host API calls from this
  package should use generated client operations when they exist.
- `web/packages/api-client` owns the generated TypeScript client for
  template-owned Host API endpoints. Generated files live under
  `src/generated/`; hand-written same-origin configuration and exports stay
  outside that folder.
- `web/packages/config` owns shared Vite, Vitest, and TypeScript configuration
  used by frontend packages and apps.
- Browser code calls same-origin BFF/API endpoints and does not store identity
  provider access or refresh tokens.
- Browser login uses same-origin navigation to `/auth/login`; browser logout
  submits a same-origin `POST /auth/logout`.
- Local Vite apps proxy `/api/` and `/auth/` to the Host. Set
  `VITE_HOST_ORIGIN` to override the default Host target of
  `http://localhost:5162`.

## UI Composition

Product UI should use the shared `web/packages/ui` component package before
duplicating app-local components or one-off styles. Use Tailwind tokens and
shadcn-style component conventions consistently across apps. App packages may
own route composition and product-specific screens, but shared controls,
layout primitives, form fields, overlays, and visual tokens belong in the
shared package once they are reused or expected to stay visually consistent.

Design responsive layouts from the first implementation. Do not treat mobile,
tablet, keyboard navigation, or dense desktop states as follow-up work for
shared UI primitives.

Light and dark mode should default to the operating-system color-scheme
preference. Add an explicit app preference only when the product experience
needs one. Avoid app-local color palettes, handwritten CSS components, and
route-specific style overrides unless the product artifact documents why the
shared system cannot express the need.

## Generated API Clients

Refresh the Host OpenAPI document and frontend generated client with:

```sh
pnpm api-client:generate
```

Check that generated output is current with:

```sh
pnpm api-client:check
```

The generated client defaults browser API calls to the current browser origin
and the `/api/` route space. It must not be configured with identity-provider
origins, provider access tokens, refresh tokens, or provider authorization
payloads.

The local pre-commit hook and default CI workflow run `pnpm api-client:check`
so OpenAPI/client drift is caught before merge.

Hey API also supports TanStack Query generation. The template generates
SDK/types only and keeps app-facing query composition in
`web/packages/auth`. The generated-client configuration does not enable the
TanStack Query plugin:

```ts
plugins: ["@tanstack/react-query"];
```

App code consumes template-owned query helpers rather than generated TanStack
Query helpers directly.
