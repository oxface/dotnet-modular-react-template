# Web Architecture

The frontend target is a pnpm workspace with React, Vite, TanStack Query,
TanStack Router, Tailwind, and shared packages for owned UI, auth helpers, and
generated API clients.

Current direction:

- `web/apps/admin` is the app-owned administration portal.
- `web/apps/web` is the neutral user-facing portal.
- Shared packages should stay boring and reusable: UI primitives, API clients,
  auth helpers, and test utilities.
- Browser code calls same-origin BFF/API endpoints and does not store identity
  provider access or refresh tokens.

Gate 2 includes only root pnpm package baselines. Frontend apps and shared
packages come in later gates.
