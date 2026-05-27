# Auth And Authorization

Authentication mechanics are a Host/platform responsibility. Identity and
application authorization data are module responsibilities.

Host responsibilities:

- Configure ASP.NET Core authentication.
- Handle OIDC challenge, callback, and logout routes.
- Store server-side session tickets in Redis.
- Ensure unauthenticated API requests return `401`.
- Ensure forbidden API requests return `403`.
- Suppress browser redirects for API endpoints.

Identity module responsibilities:

- Translate authenticated OIDC principals into local user identities.
- Store app-owned authorization records.
- Provide current-user context.
- Provide command-side behavior for granting one initial application admin.

Migrator responsibilities:

- Apply module-owned database migrations.
- Run explicit initial-admin setup when configured or invoked by command.

The identity provider proves identity. The application decides product access.

Custom request-header authentication is not production authentication. It exists
only in backend tests as temporary verification scaffolding. It must not be
wired into production Host composition, emitted as response state, or used as a
replacement for calling `GET /api/me`.

Implementation progress for the shipped template lives in
[../current-state/platform.md](../current-state/platform.md).

## Local OIDC Defaults

Local development uses Keycloak through Aspire. The checked-in realm import
configures the `modular-template` realm and `modular-template-host` public OIDC
client with PKCE and the Host callback/logout routes.

The development defaults are:

- OIDC authority: `http://localhost:8080/realms/modular-template`
- OIDC client id: `modular-template-host`
- Login route: `/auth/login`
- Logout route: `POST /auth/logout`
- Callback route: `/auth/callback`
- Signed-out callback route: `/auth/signed-out`
- Redis connection string key: `ConnectionStrings:session-tickets`
- Initial admin config: `Identity:InitialAdmin:Provider` and
  `Identity:InitialAdmin:Subject`

Identity-provider roles, groups, organizations, and provider-specific
authorization claims are not authoritative for product authorization.
