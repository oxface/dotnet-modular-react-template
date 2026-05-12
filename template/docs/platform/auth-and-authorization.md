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

- Apply Host-owned database migrations.
- Run explicit initial-admin setup when configured or invoked by command.

The identity provider proves identity. The application decides product access.

## Current Implementation Slice

The template includes Host-owned OIDC and cookie authentication for browser
sessions. The Host uses the application cookie as the local session scheme and
OpenID Connect as the login/sign-out challenge scheme. Authentication ticket
state is stored server-side in Redis through a minimal Host-owned ticket store,
so browser code receives only an opaque application session cookie.

The Host resolves a request principal, maps it to a provider-neutral
authenticated identity value, requires authentication for the current-user
endpoint, and exposes application-access authorization as a Host policy backed
by Identity contracts.

During OIDC token validation, the Host normalizes the authenticated provider to
the configured `Authentication:Oidc:Authority` and stores it in the local
session as a `provider` claim. Current-user resolution uses that provider plus
the stable subject claim so local user records match Migrator initial-admin
configuration even when provider token claim sets differ.

API authentication failures return `401` without browser redirects. API
authorization failures for authenticated users without active application-owned
access return `403`.

Initial admin setup is not a Host startup side effect. The Migrator can grant
one configured `(provider, subject)` pair app-owned admin/application access
after migrations. The operation is idempotent while access is active, but it
does not reactivate revoked access unless explicitly forced.

Custom request-header authentication is not production authentication. It exists
only in backend tests as temporary verification scaffolding. It must not be
wired into production Host composition, emitted as response state, or used as a
replacement for calling `GET /api/me`.

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
