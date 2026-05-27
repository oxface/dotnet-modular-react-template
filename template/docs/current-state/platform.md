# Platform Current State

The generated template ships local platform support through Aspire.

The shipped local platform includes:

- PostgreSQL for the generated product database.
- Redis for BFF/session ticket storage.
- Keycloak for local OpenID Connect authentication.
- Migrator for applying module-owned migrations and explicit initial-admin
  setup.
- Host API.
- Admin and web Vite app resources.
- Named local data volumes for PostgreSQL, Redis, and Keycloak.

Mailpit, local AI resources, eval runners, durable workflow orchestration, and
external workflow resources are not part of the shipped template surface.
Durable outbox/inbox persistence and local workers are shipped as backend
platform libraries rather than orchestration resources.

The shipped auth slice uses Host-owned OIDC and cookie authentication for
browser sessions. The Host stores authentication ticket state server-side in
Redis, maps the request principal to a provider-neutral authenticated identity
value, and exposes application-access authorization through Identity contracts.

The default CI workflow runs backend restore, build, filtered tests with
coverage collection, frontend validation, and generated API client drift
checks. Aspire/browser automation remains outside the default CI surface.
