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
- Bootstrap one initial application admin.

The identity provider proves identity. The application decides product access.
