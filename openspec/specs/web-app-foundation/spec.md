# web-app-foundation Specification

## Purpose

Defines the accepted frontend foundation for the template's browser apps,
including app shells, browser-safe session helpers, current-user access states,
local Host proxying, and focused frontend verification.

## Requirements

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
- **AND** login and logout commands are verified through the shared same-origin
  auth helper boundary
