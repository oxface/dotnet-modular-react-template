## ADDED Requirements

### Requirement: Operation Status Query Contract

The system SHALL expose an in-process operations query contract that returns operation lifecycle state by operation identifier.

#### Scenario: Operation exists

- **GIVEN** an operation identifier that exists
- **WHEN** a consumer queries operation status
- **THEN** the contract returns operation details including identifier, type, status, and timestamps.

### Requirement: Operation Status Endpoint

The system SHALL expose an authenticated HTTP endpoint at `/api/operations/{operationId}`.

#### Scenario: Operation found

- **GIVEN** an authenticated caller and an existing operation identifier
- **WHEN** the caller requests `GET /api/operations/{operationId}`
- **THEN** the API returns `200 OK` with operation details.

#### Scenario: Operation missing

- **GIVEN** an authenticated caller and a missing operation identifier
- **WHEN** the caller requests `GET /api/operations/{operationId}`
- **THEN** the API returns `404 Not Found`.

### Requirement: Operation Lifecycle Aggregate

The operations domain SHALL model lifecycle transitions for pending, running, completed, failed, and cancelled states.

#### Scenario: Mark completed

- **GIVEN** a pending or running operation
- **WHEN** it is marked completed
- **THEN** the operation status becomes completed and completion timestamp is recorded.

#### Scenario: Invalid transition

- **GIVEN** a completed operation
- **WHEN** it is marked running
- **THEN** the aggregate rejects the transition with an invalid operation error.
