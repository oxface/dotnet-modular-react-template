# Change Proposal: Durable Message Workers

## Why

The template now persists outbox and inbox records but does not process them. Without workers, durable messages cannot move across module boundaries or execute handlers.

## Scope

This change introduces a narrow runtime slice:

- Outbox dispatcher worker that reads pending outbox records and writes inbox records.
- Rebus-backed local transport bridge between outbox dispatch and inbox persistence.
- Inbox processor worker that reads pending inbox records and invokes strongly typed message handlers.
- Basic retry and dead-letter transitions for processing failures.
- Focused tests for dispatch, processing success, and failure-to-dead-letter behavior.

## Non-Goals

- External broker integration.
- Multi-instance distributed locking guarantees.
- Full subscription registry with dynamic discovery.
- Conversion of every module command/event flow to durable messaging.
