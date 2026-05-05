# Orchestration Architecture

The local platform target uses Aspire as the development entrypoint.

Expected local resources include:

- Host API
- Migrator
- PostgreSQL
- Redis for BFF session tickets
- Keycloak for local OIDC authentication
- Mailpit
- Vite frontend apps

Orchestration lives under the top-level `orchestration/` folder. Gate 2 includes
an empty project shell only; Aspire resources come in a later gate.
