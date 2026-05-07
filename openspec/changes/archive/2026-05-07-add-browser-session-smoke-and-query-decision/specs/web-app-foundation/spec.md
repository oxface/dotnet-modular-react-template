## ADDED Requirements

### Requirement: Browser Session Smoke Surface

The frontend apps MUST expose a small domain-neutral browser-session smoke
surface that verifies the accepted Host session path without introducing
product-specific workflows.

#### Scenario: Unauthenticated smoke state

- **WHEN** `GET /api/me` returns `401 Unauthorized` in either frontend app
- **THEN** the smoke surface presents an unauthenticated session state
- **AND** it includes a command that starts Host login through a same-origin
  relative `/auth/` route

#### Scenario: Authenticated without access smoke state

- **WHEN** `GET /api/me` succeeds with `applicationAccess.hasAccess` set to
  false in either frontend app
- **THEN** the smoke surface presents an authenticated-without-access state
- **AND** it includes a command that starts Host logout through a same-origin
  relative `/auth/` route

#### Scenario: Authenticated with access smoke state

- **WHEN** `GET /api/me` succeeds with `applicationAccess.hasAccess` set to
  true in either frontend app
- **THEN** the smoke surface presents an authenticated-with-access state
- **AND** it includes a command that starts Host logout through a same-origin
  relative `/auth/` route

#### Scenario: Smoke surface stays domain-neutral

- **WHEN** either frontend app renders the browser-session smoke surface
- **THEN** the visible experience avoids product workflows, sample business
  data, provider roles, provider groups, provider organizations, and provider
  token material

### Requirement: Browser Session Smoke Verification

The browser-session smoke surface MUST include focused frontend verification
for both app shells.

#### Scenario: App shell smoke tests run

- **WHEN** frontend app tests run
- **THEN** unauthenticated, authenticated-without-access, and
  authenticated-with-access smoke states are verified across the admin and web
  app shells
- **AND** login and logout commands are verified through the shared
  same-origin auth helper boundary
