# Web Architecture

The frontend is a pnpm workspace with React, Vite, TanStack Query, TanStack
Router, Tailwind, and shared packages for owned UI, auth helpers, configuration,
and generated API clients.

- `web/apps/admin` is the app-owned administration portal.
- `web/apps/web` is the neutral user-facing portal.
- `web/packages/auth` owns browser-safe BFF session helpers, current-user
  loading, and access-state utilities. Host API calls from this package should
  use generated client operations when they exist.
- `web/packages/api-client` owns the generated TypeScript client for
  template-owned Host API endpoints. Generated files live under
  `src/generated/`; hand-written same-origin configuration and exports stay
  outside that folder.
- `web/packages/config` owns shared Vite, Vitest, and TypeScript configuration
  used by frontend packages and apps.
- Future shared packages should stay boring and reusable: UI primitives, API
  clients, auth helpers, and test utilities.
- Browser code calls same-origin BFF/API endpoints and does not store identity
  provider access or refresh tokens.
- Local Vite apps proxy `/api/` and `/auth/` to the Host. Set
  `VITE_HOST_ORIGIN` to override the default Host target of
  `http://localhost:5162`.

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

The local pre-commit hook runs `pnpm api-client:check`. Future CI workflows
should run the same command so OpenAPI/client drift is caught before merge.

Hey API also supports TanStack Query generation. The template currently
generates SDK/types only and keeps query composition in `web/packages/auth`.
Generated query helpers are a good follow-up once the next UI smoke gate proves
the desired app-facing shape. A future change can enable the plugin in
`web/packages/api-client/openapi-ts.config.ts` with a shape like:

```ts
plugins: ["@tanstack/react-query"];
```

That follow-up should decide whether app code imports generated query helpers
directly or whether shared packages wrap them behind template-owned helpers.
