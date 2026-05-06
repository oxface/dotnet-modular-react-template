## ADDED Requirements

### Requirement: Admin And Web App Shells

The template MUST provide separate browser app shells for the app-owned admin
portal and the neutral user-facing portal.

#### Scenario: Frontend apps build

- **WHEN** frontend workspace validation runs for the admin and web apps
- **THEN** both app shells build successfully as React/Vite applications

#### Scenario: App shells stay domain-neutral

- **WHEN** either app shell renders its initial authenticated surface
- **THEN** the visible experience avoids product-specific domain workflows,
  sample business data, and provider-specific authorization concepts

### Requirement: Browser Session Helpers

The frontend MUST use shared browser auth helpers that call the Host through
same-origin BFF routes and MUST NOT store identity-provider tokens in browser
code.

#### Scenario: Current user is loaded

- **WHEN** a browser app needs current-user state
- **THEN** it requests `GET /api/me` through a same-origin relative URL
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

### Requirement: Access State Routing

The frontend MUST distinguish unauthenticated users, authenticated users without
application access, and authenticated users with application access.

#### Scenario: User is unauthenticated

- **WHEN** `GET /api/me` returns `401 Unauthorized`
- **THEN** the browser app presents an unauthenticated state with a command that
  starts Host login

#### Scenario: User lacks application access

- **WHEN** `GET /api/me` succeeds with `applicationAccess.hasAccess` set to
  false
- **THEN** the browser app presents an authenticated-without-access state
- **AND** it does not treat the user as unauthenticated

#### Scenario: User has application access

- **WHEN** `GET /api/me` succeeds with `applicationAccess.hasAccess` set to true
- **THEN** protected app routes can render their authenticated shell

### Requirement: Local Host Proxying

The frontend development server MUST proxy Host-owned API and auth routes to the
local Host service.

#### Scenario: API request is proxied locally

- **WHEN** a developer runs a frontend app through Vite and the app requests an
  `/api/` route
- **THEN** the request is proxied to the configured local Host target

#### Scenario: Auth request is proxied locally

- **WHEN** a developer runs a frontend app through Vite and the browser
  navigates to an `/auth/` route
- **THEN** the request is proxied to the configured local Host target

### Requirement: Frontend Foundation Verification

The frontend foundation MUST include focused tests for shared auth/session
behavior and access-state routing.

#### Scenario: Auth helper tests run

- **WHEN** frontend package tests run
- **THEN** current-user success, unauthenticated failure, login navigation, and
  logout navigation behavior are verified without calling a real identity
  provider

#### Scenario: Access guard tests run

- **WHEN** frontend app or package tests run
- **THEN** unauthenticated, authenticated-without-access, and
  authenticated-with-access states are verified
