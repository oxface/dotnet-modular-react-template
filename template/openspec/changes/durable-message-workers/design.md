# Design: Durable Message Workers

## Components

- `OutboxDispatcher`: Pulls pending outbox messages and creates inbox records for local delivery.
- `RebusOutboxTransport`: Uses Rebus as the local transport for durable envelope delivery.
- `RebusDurableTransportHandler`: Receives Rebus envelopes and persists inbox records.
- `InboxProcessor`: Pulls pending inbox messages, deserializes payload using the message type registry, and invokes typed handlers.
- `OutboxDispatcherBackgroundService` and `InboxProcessorBackgroundService`: polling wrappers around the dispatch/processing services.

## Aspire Integration

- AppHost provisions `service-bus` via `AddAzureServiceBus(...).RunAsEmulator()`.
- Host receives the emulator connection as `ConnectionStrings:service-bus` through `WithReference(serviceBus)`.
- Persistence config selects Rebus Azure Service Bus transport when this connection string is present.
- Tests and non-Aspire runs continue to use the in-memory Rebus transport fallback.

## Delivery Rules

- Commands require a target module; missing target is treated as processing failure.
- Events with no explicit target are marked processed (no local subscribers).
- Inbox deduplication uses `(MessageId, TargetModule)` uniqueness.

## Retry and Dead-Letter

- Failures increment `AttemptCount` and schedule `NextAttemptAtUtc`.
- Once attempts reach `MaxAttempts`, status transitions to `DeadLettered`.

## Verification

- Integration tests validate outbox -> inbox delivery and inbox handler execution.
- Failure-path tests validate retry/dead-letter transitions.
