## ADDED Requirements

### Requirement: Local Browser Session Smoke Path

The local Aspire platform documentation MUST describe how developers can use
the admin and web frontend resources to smoke-test the Host-owned browser
session path.

#### Scenario: Local smoke path is documented

- **WHEN** a developer reads the local services or orchestration documentation
- **THEN** the docs identify that the admin and web frontend resources expose
  the browser-session smoke surface
- **AND** the docs describe verifying login, current-user loading,
  application-access state, and logout through same-origin frontend routes

#### Scenario: Local smoke path preserves browser auth boundary

- **WHEN** a developer uses either frontend resource to smoke-test the session
  path
- **THEN** browser code calls same-origin `/api/` and `/auth/` routes
- **AND** browser code does not receive identity-provider access tokens or
  refresh tokens from orchestration configuration
