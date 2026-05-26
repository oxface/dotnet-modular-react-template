## ADDED Requirements

### Requirement: Rebus Transport Bridge

The system SHALL use Rebus as the local transport abstraction between outbox dispatch and inbox persistence.

#### Scenario: Aspire local environment provides Service Bus transport

- **GIVEN** the local AppHost topology is started
- **WHEN** host service dependencies are composed
- **THEN** Aspire provides `ConnectionStrings:service-bus` from a Service Bus emulator resource
- **AND** durable transport uses Azure Service Bus through Rebus.

#### Scenario: Azure Service Bus transport is selected without connection string

- **GIVEN** `Messaging:Transport` is `AzureServiceBus`
- **WHEN** the host persistence transport is configured without `ConnectionStrings:service-bus`
- **THEN** startup fails with a configuration error.

#### Scenario: Azure Service Bus transport probe fails

- **GIVEN** `Messaging:Transport` is `AzureServiceBus` with a configured connection string
- **WHEN** startup transport probe cannot reach the Service Bus namespace
- **THEN** startup fails before processing workers begin.

#### Scenario: Outbox command dispatch through transport

- **GIVEN** a pending outbox command message with a target module
- **WHEN** dispatch runs
- **THEN** the message is sent through Rebus as a durable transport envelope
- **AND** a Rebus handler persists the corresponding inbox record for the target module.

### Requirement: Outbox Dispatch

The system SHALL dispatch pending outbox records to local inbox records for durable processing.

#### Scenario: Command message with target module

- **GIVEN** a pending outbox command message with a target module
- **WHEN** the outbox dispatcher runs
- **THEN** a corresponding inbox message is persisted for that target module
- **AND** the outbox message is marked processed.

#### Scenario: Event message without local target

- **GIVEN** a pending outbox event message without a target module
- **WHEN** the outbox dispatcher runs
- **THEN** no inbox message is created
- **AND** the outbox message is marked processed.

### Requirement: Inbox Processing

The system SHALL process pending inbox records using strongly typed handlers.

#### Scenario: Typed command handler succeeds

- **GIVEN** a pending inbox command message with a registered message type and handler
- **WHEN** the inbox processor runs
- **THEN** the handler executes with deserialized payload
- **AND** the inbox message is marked processed.

### Requirement: Failure Retry And Dead-Letter

The system SHALL retry failed durable message processing and dead-letter after max attempts.

#### Scenario: Processing repeatedly fails

- **GIVEN** a pending message whose processing throws on each attempt
- **WHEN** processing runs until attempts reach max attempts
- **THEN** message status becomes dead-lettered
- **AND** failure metadata is recorded.
