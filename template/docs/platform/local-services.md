# Local Services

The template's current local platform uses Aspire and includes:

- PostgreSQL for the Host-owned database.
- Redis for BFF/session ticket storage.
- Keycloak for local OpenID Connect authentication.
- Migrator for applying Host-owned migrations.
- Host API.
- Admin Vite app.
- Web Vite app.

Mailpit is not part of the current local orchestration topology.

## Startup

Start the local platform from the repository root:

```sh
aspire start --apphost orchestration/ModularTemplate.Orchestration/ModularTemplate.Orchestration.csproj --isolated
```

Use `aspire describe` to inspect resource endpoints and status after startup.
IDE/F5 and direct `dotnet run` launches use the AppHost launch profile under
`orchestration/ModularTemplate.Orchestration/Properties/launchSettings.json`;
that profile provides the dashboard, OTLP, resource-service, and local
HTTP-only development environment variables required by Aspire.

## Podman

Aspire supports Podman as the local container runtime. Set the runtime before
startup:

```sh
export ASPIRE_CONTAINER_RUNTIME=podman
```

Backend tests use Testcontainers and need a Docker-API-compatible socket. For
rootless Podman on Linux, start the user socket and point Testcontainers at it:

```sh
systemctl --user enable --now podman.socket

export DOCKER_HOST="unix://${XDG_RUNTIME_DIR}/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true
```

`TESTCONTAINERS_RYUK_DISABLED=true` is commonly needed with rootless Podman. If
a test run is interrupted, use `podman ps -a` to find and remove leftover test
containers.

## Development Defaults

Keycloak is configured through the checked-in realm import under
`orchestration/ModularTemplate.Orchestration/Realms/`. The local Keycloak
resource is exposed on port `8080`, and the Host uses
`http://localhost:8080/realms/modular-template` as its development OIDC
authority.

The Redis resource is referenced by the Host as
`ConnectionStrings:session-tickets`. The PostgreSQL database resource is
referenced as `ConnectionStrings:modular-template-host`.

The checked-in Keycloak realm import includes local users for browser smoke
testing:

- `admin@example.test` / `Password123!` has initial application access through
  the AppHost-provided bootstrap subject.
- `user@example.test` / `Password123!` can authenticate without application
  access.

The admin and web Vite app resources receive `VITE_HOST_ORIGIN` from the local
Host HTTP endpoint. Their Vite development servers continue to proxy `/api/`
and `/auth/` routes to the Host rather than calling identity-provider endpoints
directly from browser code.

The Migrator runs `ModularTemplateDbContext` migrations during Aspire startup.
This repository intentionally starts without generated EF migrations, so create
and commit the initial migration before relying on the local platform to create
the schema.

## Browser Session Smoke

The admin and web frontend resources render a small domain-neutral browser
session smoke surface on their initial screen. Use it to verify that a local
browser can:

- load current-user state through the same-origin `/api/me` route;
- start login through the same-origin `/auth/login` route;
- distinguish unauthenticated, authenticated-without-access, and
  authenticated-with-access states;
- submit logout through the same-origin `POST /auth/logout` route.

The smoke surface is intentionally not a product workflow. Browser code must
continue to call only same-origin app routes and must not receive
identity-provider access tokens or refresh tokens from Aspire configuration.

## Reset Behavior

PostgreSQL, Redis, and Keycloak use named local data volumes so repeated Aspire
starts preserve useful development state. Delete the named volumes when a clean
local reset is needed. The checked-in Keycloak realm import remains the source
of repeatable local identity-provider client and smoke-user configuration.
