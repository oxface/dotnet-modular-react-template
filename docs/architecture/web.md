# Web Architecture

The frontend is a pnpm workspace with React, Vite, TanStack Query, TanStack
Router, Tailwind, and shared packages for owned UI, auth helpers, configuration,
and generated API clients.

- `web/apps/admin` is the app-owned administration portal.
- `web/apps/web` is the neutral user-facing portal.
- `web/packages/auth` owns browser-safe BFF session helpers, current-user
  loading, and access-state utilities.
- `web/packages/config` owns shared Vite, Vitest, and TypeScript configuration
  used by frontend packages and apps.
- Future shared packages should stay boring and reusable: UI primitives, API
  clients, auth helpers, and test utilities.
- Browser code calls same-origin BFF/API endpoints and does not store identity
  provider access or refresh tokens.
- Local Vite apps proxy `/api/` and `/auth/` to the Host. Set
  `VITE_HOST_ORIGIN` to override the default Host target of
  `http://localhost:5162`.
