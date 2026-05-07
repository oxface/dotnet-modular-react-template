# generated-api-clients Specification

## Purpose

Defines accepted Host OpenAPI document generation, generated frontend API client behavior, same-origin browser API constraints, and generated-client verification for the template.

## Requirements

### Requirement: Host OpenAPI Document

The Host MUST generate an OpenAPI document for the template-owned API surface
from Host endpoint metadata.

#### Scenario: OpenAPI document is generated

- **WHEN** the documented client generation workflow runs
- **THEN** an OpenAPI document is produced from the Host API endpoints
- **AND** the document includes `GET /api/me`

#### Scenario: Provider token material is absent

- **WHEN** the generated OpenAPI document is inspected
- **THEN** it does not include identity-provider access tokens, refresh tokens,
  provider SDK payloads, or provider authorization claims in API response
  schemas

#### Scenario: Auth routes are excluded

- **WHEN** the generated OpenAPI document is inspected
- **THEN** Host-owned browser auth routes such as login, logout, callback, and
  signed-out callback routes are not generated as frontend API client operations

### Requirement: Frontend API Client Package

The frontend workspace MUST include a generated API client package that is
usable by shared frontend packages and both browser apps.

#### Scenario: Package exports generated current-user client

- **WHEN** frontend TypeScript code imports from the API client package
- **THEN** it can call the generated current-user API client for `GET /api/me`
- **AND** it can use generated current-user response types

#### Scenario: Generated files are isolated

- **WHEN** the API client package is inspected
- **THEN** generated files are isolated under a clearly marked generated source
  folder
- **AND** hand-written package configuration or exports are kept outside the
  generated source folder

### Requirement: Same-Origin Browser Calls

Generated browser API client usage MUST preserve the accepted same-origin BFF
session boundary.

#### Scenario: Current-user client uses same-origin API route

- **WHEN** browser code uses the generated current-user client
- **THEN** the request targets the same-origin `/api/me` route
- **AND** browser code does not configure an identity-provider origin for API
  calls

#### Scenario: Browser code does not handle provider tokens

- **WHEN** generated client package code and browser consumers are inspected
- **THEN** browser code does not read, store, or send identity-provider access
  tokens or refresh tokens

### Requirement: Repeatable Generation Workflow

The repository MUST provide repeatable commands for generating and validating
the Host OpenAPI document and frontend API client output.

#### Scenario: Client generation command runs

- **WHEN** a developer runs the documented client generation command
- **THEN** the Host OpenAPI document is refreshed
- **AND** the frontend API client package generated output is refreshed from
  that document

#### Scenario: Client drift check runs

- **WHEN** repository validation checks generated client freshness
- **THEN** stale OpenAPI or generated client output is reported as a validation
  failure

### Requirement: Generated Client Verification

The generated API client change MUST include focused verification for backend
contract generation and frontend consumption.

#### Scenario: Backend and frontend validation pass

- **WHEN** implementation verification runs for this change
- **THEN** backend restore, build, and tests pass
- **AND** frontend typecheck, tests, build, lint, and formatting checks pass

#### Scenario: Current-user helper consumes generated client

- **WHEN** frontend auth package tests run
- **THEN** current-user success and unauthenticated behavior are verified
  through the generated current-user client boundary

### Requirement: MVP Query Helper Boundary

For MVP 1, the template MUST keep app-facing TanStack Query composition in
template-owned shared packages and MUST NOT require frontend apps to import Hey
API generated TanStack Query helpers directly.

#### Scenario: Current-user query composition is consumed

- **WHEN** a frontend app needs current-user query behavior
- **THEN** it consumes the template-owned auth query helper
- **AND** the helper loads current-user state through the generated API client
  operation

#### Scenario: Generated query helpers are deferred

- **WHEN** the API client generation configuration is inspected for MVP 1
- **THEN** it generates SDK/types needed by current browser consumers
- **AND** it does not expose generated TanStack Query helpers as the required
  app-facing query API
