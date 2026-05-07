## ADDED Requirements

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
