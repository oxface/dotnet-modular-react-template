## 1. Host Authentication Foundation

- [x] 1.1 Add central package versions and Host package references for OpenID Connect, Redis/distributed cache support, and any required authentication abstractions.
- [x] 1.2 Replace baseline Host authentication registration with explicit cookie and OpenID Connect scheme configuration.
- [x] 1.3 Configure cookie events so API requests return `401` or `403` instead of redirecting to browser login or access-denied routes.
- [x] 1.4 Implement a minimal Host-owned Redis-backed authentication ticket store for the application cookie session.
- [x] 1.5 Keep custom request-header authentication available only from test projects/test configuration.

## 2. Browser Auth Routes

- [x] 2.1 Add Host login route mapping that starts an OpenID Connect challenge with a configurable return URL.
- [x] 2.2 Add Host logout route mapping that clears the local cookie session and initiates provider sign-out when configured.
- [x] 2.3 Ensure login/logout routes remain Host/platform behavior and do not introduce Identity module dependencies on HTTP or provider SDK types.

## 3. Local Aspire Platform

- [x] 3.1 Convert the orchestration project shell into an Aspire app host.
- [x] 3.2 Add PostgreSQL, Redis, Keycloak, Migrator, and Host resources to the Aspire app model.
- [x] 3.3 Wire Host configuration for database, Redis ticket storage, OIDC authority, client identifier, callback, and logout values from the app model.
- [x] 3.4 Add deterministic local Keycloak realm/client setup through checked-in import JSON wired by the Aspire Keycloak integration.

## 4. Documentation

- [x] 4.1 Update `docs/platform/auth-and-authorization.md` with the implemented OIDC/cookie/Redis session behavior and test-only status of header authentication.
- [x] 4.2 Update `docs/platform/local-services.md` and `docs/architecture/orchestration.md` with local startup, resources, reset notes, and expected development configuration.
- [x] 4.3 Update repository handoff documentation after implementation to record the completed gate and verification commands.

## 5. Tests And Verification

- [x] 5.1 Add Host tests for API `401` behavior with missing/expired/invalid cookie-session authentication and no login redirects.
- [x] 5.2 Add Host tests for API `403` behavior for authenticated users without application access and no access-denied redirects.
- [x] 5.3 Add focused tests for login/logout route behavior and authentication scheme composition.
- [x] 5.4 Add focused tests for Redis ticket-store behavior or option wiring without requiring browser code to handle provider tokens.
- [x] 5.5 Run `dotnet restore ModularTemplate.slnx`, `dotnet build ModularTemplate.slnx --no-restore`, relevant `dotnet test` commands, and `pnpm format:check`.
