# Orchestration Architecture

The local platform target uses Aspire as the development entrypoint.

Expected local resources include:

- Host API
- Migrator
- PostgreSQL
- Redis for BFF session tickets
- Keycloak for local OIDC authentication
- Mailpit, deferred until mail workflows exist
- Vite frontend apps for the admin and web portals

Orchestration lives under the top-level `orchestration/` folder.

The current Aspire app host is
`orchestration/ModularTemplate.Orchestration/ModularTemplate.Orchestration.csproj`.
It defines PostgreSQL, Redis, Keycloak, Migrator, Host, admin frontend, and web
frontend resources. The Host waits for the Migrator to complete before starting,
and the frontend resources receive the Host HTTP endpoint through
`VITE_HOST_ORIGIN` for local `/api/` and `/auth/` proxying.

The admin and web frontend resources expose the initial browser-session smoke
surface. It verifies login, current-user loading, application-access state, and
logout through same-origin frontend routes while preserving the Host-owned BFF
session boundary.

Keycloak uses a checked-in realm import JSON for deterministic local OIDC
client configuration. Persistent local service volumes are intentionally not
enabled in the initial topology; reset behavior should stay cheap while the
template is still under construction.
