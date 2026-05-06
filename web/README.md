# Web

Frontend apps and packages live here.

Current shape:

- `apps/admin`
- `apps/web`
- `packages/auth`
- `packages/config`

Future shared packages may include `packages/api-client` and `packages/ui` once
their scope is accepted.

Local Vite apps proxy `/api/` and `/auth/` to the Host. Set `VITE_HOST_ORIGIN`
to override the default target of `http://localhost:5162`.
