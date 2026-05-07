## MODIFIED Requirements

### Requirement: Browser Session Helpers

The frontend MUST use shared browser auth helpers that call the Host through
same-origin BFF routes, MUST use the generated API client package for
Host-owned API calls where generated operations exist, and MUST NOT store
identity-provider tokens in browser code.

#### Scenario: Current user is loaded

- **WHEN** a browser app needs current-user state
- **THEN** it requests `GET /api/me` through the generated API client package
- **AND** the generated client uses the same browser origin for the API route
- **AND** it treats the response as the source of authenticated user and
  application-access state

#### Scenario: Login starts

- **WHEN** a browser app needs to start authentication
- **THEN** it navigates to the Host login route through a same-origin relative
  URL

#### Scenario: Logout starts

- **WHEN** a browser app needs to end the session
- **THEN** it navigates to the Host logout route through a same-origin relative
  URL

#### Scenario: Browser auth code is inspected

- **WHEN** frontend auth/session helper code is inspected
- **THEN** it does not read, store, or send identity-provider access tokens or
  refresh tokens
