# Web Current State

The generated template ships a pnpm workspace with React, Vite, TanStack Query,
TanStack Router-ready app structure, Tailwind, and shared frontend packages.

The shipped frontend includes:

- `web/apps/admin` for the app-owned administration portal.
- `web/apps/web` for the neutral user-facing portal.
- `web/packages/auth` for browser-safe BFF session helpers, current-user
  loading, access-state utilities, app-facing TanStack Query composition, and a
  domain-neutral browser-session smoke surface.
- `web/packages/api-client` for generated TypeScript clients for
  template-owned Host API endpoints.
- `web/packages/config` for shared Vite, Vitest, and TypeScript configuration.
- `web/packages/ui` for shared Tailwind-styled UI primitives. The initial
  package includes Button and Textarea components.

The initial admin and web app screens are smoke surfaces, not product
workflows. Future product UI should extend the shared UI component package
before duplicating app-local components or styles.
