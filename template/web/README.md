# Web

Frontend apps and packages live here.

Workspace shape:

- `apps/admin`
- `apps/web`
- `packages/api-client`
- `packages/auth`
- `packages/config`
- `packages/ui`

Product UI should extend `packages/ui` before duplicating reusable components
or one-off app-local style systems.

Local Vite apps proxy `/api/` and `/auth/` to the Host. Set `VITE_HOST_ORIGIN`
to override the default target of `http://localhost:5162`.
