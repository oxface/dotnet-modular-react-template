# Modular Monolith Durable Messaging & Module Persistence Refactor Plan

## Status

Large parts of this direction now exist in the template payload: module-owned
`DbContext` types and schemas, module-owned migrations, shared Infrastructure
library folders for Persistence/Outbox/Transport, local durable workers, and
an Operations status slice.
Treat the rest of this document as background direction and backlog, not as a
fresh inventory of missing work.

Current staleness to watch:

- Completed OpenSpec changes under `template/openspec/changes/` should be
  archived when the team is ready for accepted specs to become the source of
  truth.
- Product-facing docs under `template/docs/` are the durable reference once
  they conflict with this todo handoff.

## Goal

Refactor ModularTemplate toward a production-realistic modular monolith architecture with:

- DbContext per module
- PostgreSQL schema per module
- module-owned migrations
- persistent domain events
- durable outbox/inbox messaging
- message-driven cross-module communication
- in-process module query contracts
- durable cross-module command submissions
- first-class operation/process tracking
- explicit module boundaries suitable for future extraction

This is intended to become the reference implementation before backporting the same architecture into the reusable template.

---

## Architectural Direction

ModularTemplate should use a modular monolith architecture.

The app remains:

- single deployable unit
- single runtime process
- single physical database
- single observability surface
- simple local debugging model

But each module should own:

- its own domain model
- its own application layer
- its own infrastructure
- its own DbContext
- its own PostgreSQL schema
- its own migrations
- its own domain events
- its own inbox/outbox tables, or at minimum logically module-scoped inbox/outbox records
- its own endpoint registration

Cross-module communication should be explicit and consumer-driven.

Modules should be closed by default.

The only default public surface of a module is its HTTP endpoints.

Additional intermodule contracts should be added only when a real consumer needs them.

---

## Communication Model

ModularTemplate should support two general intermodule interaction families:

1. In-process interaction
2. Durable interaction

### In-process interaction

Use in-process contracts when the consumer needs data or a result immediately.

Default use cases:

- queries
- lookups
- validation
- small synchronous capabilities
- immediate command results when truly required

Rules:

- Return DTOs/read models.
- Do not expose EF entities.
- Do not expose IQueryable.
- Do not expose module internals.
- Do not let consumers reference another module's Application, Domain, or Infrastructure projects.
- Consumers may reference another module's Contracts project only.

Example:

```csharp
public interface IIncidentsQueries
{
    Task<IncidentDetails?> GetIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken);
}
Durable interaction

Use durable messaging when the consumer wants to request work but does not need the business result immediately.

Default use cases:

cross-module commands
side effects
long-running work
retryable work
external integrations
notifications
workflow steps
AI processing
background analysis

Rules:

Durable commands return acceptance/submission metadata, not final business result.
Results are observed later via operation status, query, or event.
Durable command handlers must be idempotent.
Durable messages must be persisted before processing.
Durable message processing must support retry and failure state.

Example:

public interface IIncidentsSubmissions
{
    Task<CommandSubmission> SubmitRegisterIncidentAsync(
        RegisterIncidentSubmission request,
        CancellationToken cancellationToken);
}

Return model:

public sealed record CommandSubmission(
    Guid SubmissionId,
    Guid? OperationId,
    CommandSubmissionStatus Status);
Default Policy

Use this policy throughout ModularTemplate:

Interaction	Default
External client to module	HTTP endpoint
Endpoint to own module application logic	Immediate in-process
Module to another module query	In-process query contract
Module to another module command	Durable submission
Module to another module command needing immediate result	Explicit in-process command contract exception
Module publishes fact to consumers	Integration event through outbox
Multi-module long-running process	Operation/process manager module
Important Terminology
Domain event

A domain event is an internal module fact.

It is raised by domain model behavior and persisted as part of the module transaction.

Domain events are not automatically public contracts.

Example:

public sealed record IncidentRegisteredDomainEvent(
    IncidentId IncidentId,
    WorkspaceId WorkspaceId,
    DateTimeOffset RegisteredAt);
Integration event

An integration event is a public, stable fact exposed by a module for other modules to consume.

Integration events are consumer-driven. Do not create integration events unless there is a real consumer.

Example:

public sealed record IncidentRegisteredIntegrationEvent(
    Guid IncidentId,
    Guid WorkspaceId,
    DateTimeOffset RegisteredAt);
Outbox message

An outbox message is a persisted transport envelope owned by the producing module.

It guarantees that a message that leaves the current transaction boundary is not lost after data is saved.

Inbox message

An inbox message is a persisted transport envelope owned by the consuming module.

It guarantees durable processing, deduplication, retries, and idempotency.

Event Publishing Rule

Any event or command that leaves the current transaction boundary must go through durable messaging.

Use the outbox for:

integration events
cross-module events
durable commands
retryable background work
externally observable events
events consumed asynchronously
events that must survive process crash

Local same-module reactions may run in-process if they are part of the same transaction.

Cross-module reactions must not run as direct domain event handlers.

Target Flow: Choreography

Producer module transaction:

1. Load aggregate.
2. Execute domain behavior.
3. Aggregate records domain event.
4. DbContext persists aggregate changes.
5. DbContext persists domain event.
6. Application/infrastructure maps selected domain events to integration events.
7. Integration events are written to producer outbox.
8. Transaction commits.

Dispatcher:

1. Poll pending producer outbox messages.
2. Resolve local subscriptions.
3. For each subscriber, write message to consumer inbox.
4. Mark outbox message as dispatched when all local inbox deliveries succeed.

Consumer inbox processor:

1. Poll pending inbox messages for module.
2. Deserialize message.
3. Resolve strongly typed handler.
4. Execute handler inside consumer module transaction.
5. Persist consumer module changes.
6. Persist any new domain events/outbox messages.
7. Mark inbox message processed.
8. On failure, increment retry metadata and schedule next attempt.
9. After retry limit, mark as failed/dead-letter.
Target Flow: Durable Command Submission

Consumer/orchestrator submits durable command:

1. Caller creates durable command.
2. Caller writes command to its own outbox, or uses messaging infrastructure to create an outbox message.
3. Dispatcher delivers command to target module inbox.
4. Target module inbox processor executes command handler.
5. Handler uses target module application layer internally.
6. Target module commits state changes.
7. Target module publishes resulting integration events through its own outbox.

Durable command submission returns:

Accepted / Rejected / Duplicate

It does not return final business result.

Final result should be obtained via:

operation status
later query
completion event
process manager state
Module Persistence Model

Move to:

One physical PostgreSQL database
One DbContext per module
One schema per module
One migration history per module

Example schemas:

operations.*
workflows.*
incidents.*
integrations.*
notifications.*
audit.*

Each module DbContext should configure its own default schema:

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("incidents");
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidentsDbContext).Assembly);
}

Each module should own its own migrations.

Recommended EF migrations history table per module:

options.UseNpgsql(
    connectionString,
    npgsql =>
    {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "incidents");
    });

No module should directly access another module's DbContext.

No module should directly mutate another module's tables.

Avoid cross-module foreign keys by default.

Use IDs across module boundaries, not EF navigation properties.

Module Project Structure

Adapt to existing ModularTemplate structure, but aim for this conceptual shape:

src/
  ModularTemplate.Host/

  Modules/
    Operations/
      ModularTemplate.Operations.Contracts/
      ModularTemplate.Operations.Domain/
      ModularTemplate.Operations.Application/
      ModularTemplate.Operations.Infrastructure/
      ModularTemplate.Operations.Endpoints/

    Incidents/
      ModularTemplate.Incidents.Contracts/
      ModularTemplate.Incidents.Domain/
      ModularTemplate.Incidents.Application/
      ModularTemplate.Incidents.Infrastructure/
      ModularTemplate.Incidents.Endpoints/

    Notifications/
      ModularTemplate.Notifications.Contracts/
      ModularTemplate.Notifications.Domain/
      ModularTemplate.Notifications.Application/
      ModularTemplate.Notifications.Infrastructure/
      ModularTemplate.Notifications.Endpoints/

If current project structure is flatter, preserve existing conventions where practical, but keep the dependency rules.

Dependency Rules

Enforce these rules:

Host may reference module Contracts, Infrastructure, and Endpoints for composition.

Module.Endpoints may reference own module Application and Contracts.

Module.Infrastructure may reference own module Application, Domain, and Contracts.

Module.Application may reference own module Domain.

Module.Application may reference another module's Contracts only if explicitly needed.

Module.Domain must not reference other modules.

Module.Contracts must not reference Infrastructure, EF Core, ASP.NET Core, or module Application.

No module may reference another module's Domain, Application, Infrastructure, or Endpoints.

Cross-module access must happen through Contracts only.

Add architecture tests later if not already present.

Prefer to add the rules now in code organization even if tests come in a later step.

Contracts Policy

Modules are closed by default.

Do not create public contracts speculatively.

Add contracts only when a real consumer needs them.

Allowed contract types:

Queries
Immediate command contracts
Durable command submissions
Integration events
DTOs/read models
IDs/value objects safe across boundaries

Avoid exposing:

EF entities
IQueryable
repositories
DbContext
application commands
internal domain events
domain aggregates
infrastructure services

Recommended naming:

I{Module}Queries
I{Module}Commands
I{Module}Submissions

Example:

public interface IIncidentsQueries
{
    Task<IncidentDetails?> GetIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken);
}

public interface IIncidentsSubmissions
{
    Task<CommandSubmission> SubmitRegisterIncidentAsync(
        RegisterIncidentSubmission request,
        CancellationToken cancellationToken);
}

Use I{Module}Commands only for explicit immediate cross-module command exceptions.

Operations Module

Add a first-class Operations module.

Purpose:

track long-running work
track durable command results
expose operation status
hold correlation between submitted commands and business results
coordinate process/saga state where appropriate

Operations module should own:

operations.operations
operations.operation_steps
operations.operation_events

Minimal operation state:

OperationId
OperationType
Status
CorrelationId
CausationId
CreatedAtUtc
UpdatedAtUtc
CompletedAtUtc
FailedAtUtc
FailureReason
ResultJson
MetadataJson

Statuses:

Pending
Running
Completed
Failed
Cancelled

Operation step state:

OperationStepId
OperationId
StepName
Status
AttemptCount
StartedAtUtc
CompletedAtUtc
FailedAtUtc
FailureReason
InputJson
OutputJson

Operations module should expose endpoint:

GET /api/operations/{operationId}

Durable HTTP commands may return:

202 Accepted
Location: /api/operations/{operationId}
Messaging Infrastructure

Create reusable messaging infrastructure that can be reused by all modules.

Keep it explicit and dependency-light.

Use:

BackgroundService
EF Core polling
PostgreSQL tables
strongly typed handlers
System.Text.Json serialization

Do not introduce Quartz/Hangfire/Wolverine at this stage.

The infrastructure should be designed so a broker adapter can be added later.

Message Envelope

Create a common message envelope for persisted transport messages.

Suggested fields:

Id
MessageId
MessageKind
MessageType
SourceModule
TargetModule
CorrelationId
CausationId
OperationId
IdempotencyKey
PayloadJson
MetadataJson
Status
AttemptCount
MaxAttempts
NextAttemptAtUtc
CreatedAtUtc
LockedAtUtc
LockedBy
ProcessedAtUtc
FailedAtUtc
Error

Message kind:

Command
Event

Message status:

Pending
Processing
Processed
Failed
DeadLettered
Cancelled

Important uniqueness:

MessageId should be globally unique.
Inbox should deduplicate by MessageId.
Optional idempotency key uniqueness should be supported.
Outbox Table

Each module should have an outbox table in its schema.

Example:

incidents.outbox_messages
notifications.outbox_messages
operations.outbox_messages

Outbox message should include:

Id
MessageId
MessageKind
MessageType
SourceModule
TargetModule nullable
CorrelationId
CausationId
OperationId nullable
PayloadJson
MetadataJson
Status
AttemptCount
NextAttemptAtUtc
CreatedAtUtc
DispatchedAtUtc
FailedAtUtc
Error

Outbox dispatch behavior:

Pending messages are selected.
Messages are locked.
Dispatcher resolves target subscribers.
Dispatcher writes messages to consumer inboxes.
Dispatcher marks outbox message Processed/Dispatched.
Failures increment attempt count.
Messages exceeding retry policy go to DeadLettered/Failed.
Inbox Table

Each module should have an inbox table in its schema.

Example:

incidents.inbox_messages
notifications.inbox_messages
operations.inbox_messages

Inbox message should include:

Id
MessageId
MessageKind
MessageType
SourceModule
TargetModule
CorrelationId
CausationId
OperationId nullable
IdempotencyKey nullable
PayloadJson
MetadataJson
Status
AttemptCount
NextAttemptAtUtc
ReceivedAtUtc
LockedAtUtc
LockedBy
ProcessedAtUtc
FailedAtUtc
Error

Inbox processing behavior:

Pending messages are selected.
Messages are locked.
Handler is resolved by MessageType and TargetModule.
Handler executes inside target module transaction.
Handler marks message processed only after transaction succeeds.
Handler failures are retried.
Poison messages are marked DeadLettered after max attempts.
Persistent Domain Events

Each module should persist domain events.

Example table:

incidents.domain_events

Suggested fields:

Id
EventId
AggregateId
AggregateType
EventType
PayloadJson
MetadataJson
OccurredAtUtc
RecordedAtUtc
CorrelationId
CausationId
UserId nullable
OperationId nullable
PublishedAtUtc nullable

Domain events should be recorded during aggregate mutation.

Domain events should be persisted in the same transaction as aggregate changes.

Domain events are internal to the module by default.

Do not publish every domain event as an integration event.

Add a mapping from domain event to integration event only when needed.

Example:

IncidentRegisteredDomainEvent
  -> IncidentRegisteredIntegrationEvent
Domain Event Capture

Implement a mechanism for aggregates to record domain events.

Possible model:

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

During SaveChangesAsync, the module DbContext or UnitOfWork should:

1. Collect domain events from tracked aggregate roots.
2. Persist domain event records.
3. Map selected domain events to integration events.
4. Persist corresponding outbox messages.
5. Clear domain events after successful persistence.

Be careful not to clear events before transaction success.

Integration Event Mapping

Create explicit mappers per module.

Example:

public interface IIntegrationEventMapper
{
    IReadOnlyCollection<IIntegrationEvent> Map(IDomainEvent domainEvent);
}

Or strongly typed mappers:

public interface IIntegrationEventMapper<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    IReadOnlyCollection<IIntegrationEvent> Map(TDomainEvent domainEvent);
}

Rules:

Mapping is explicit.
No automatic publication of all domain events.
No generic Created/Updated/Deleted integration events by default.
Integration event payloads should be stable and minimal.
Consumers should query for details if needed.
Message Handlers

Use strongly typed handlers.

Command handler:

public interface IDurableCommandHandler<in TCommand>
    where TCommand : IDurableCommand
{
    Task HandleAsync(TCommand command, MessageContext context, CancellationToken cancellationToken);
}

Event handler:

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, MessageContext context, CancellationToken cancellationToken);
}

Message context:

public sealed record MessageContext(
    Guid MessageId,
    string SourceModule,
    string TargetModule,
    Guid CorrelationId,
    Guid? CausationId,
    Guid? OperationId,
    string? IdempotencyKey,
    DateTimeOffset CreatedAtUtc);

The persisted message should deserialize into the strongly typed command/event before handler invocation.

Message Routing

Create an explicit local subscription registry.

The registry should know:

MessageType
MessageKind
SourceModule optional
TargetModule
HandlerType

For commands:

There should be one target module.
There should be one logical command handler.

For events:

There may be zero, one, or many subscribers.

Do not require every event to have subscribers.

Dispatcher behavior for an event with no subscribers:

Mark outbox message as dispatched.
No inbox messages are created.

Dispatcher behavior for command with no target handler:

Mark failed/dead-lettered.
This is configuration error.
Transaction Boundaries

Inside a module:

Application command handler executes in one module transaction.
Aggregate changes, persistent domain events, and outbox messages commit together.

Across modules:

Do not use distributed transactions.
Do not use cross-module EF changes inside one implicit transaction by default.
Use outbox/inbox messaging.

Allowed exception:

Explicit in-process command contract may be used when immediate result/consistency is required.
This should be rare and obvious in code.
Background Services

Implement at least two background workers:

OutboxDispatcherBackgroundService
InboxProcessorBackgroundService

Depending on implementation simplicity, these can either:

A. Process all module outboxes/inboxes using registered module stores

or:

B. Be registered per module

Start with the simpler model that fits existing DI.

Polling configuration:

Enabled
PollingInterval
BatchSize
MaxAttempts
LockTimeout

Use conservative defaults:

PollingInterval: 1-5 seconds
BatchSize: 20-100
MaxAttempts: 5
LockTimeout: 1-5 minutes

Add jitter/backoff later if needed.

Locking

Avoid double-processing messages when multiple app instances run.

Even if ModularTemplate currently runs as one instance, implement safe locking now because this is template infrastructure.

Use database-level locking or optimistic locking.

Possible approaches:

SELECT ... FOR UPDATE SKIP LOCKED

or update-based claiming:

UPDATE outbox_messages
SET status = 'Processing',
    locked_at_utc = now(),
    locked_by = instance_id
WHERE id IN (...)
  AND status = 'Pending'
  AND next_attempt_at_utc <= now()
RETURNING *

Prefer an approach that works cleanly with PostgreSQL.

Retry Policy

Failed processing should not immediately lose the message.

Retry fields:

AttemptCount
MaxAttempts
NextAttemptAtUtc
Error

Suggested retry delay:

Attempt 1: immediate or short delay
Attempt 2: 10 seconds
Attempt 3: 1 minute
Attempt 4: 5 minutes
Attempt 5: 15 minutes
Then DeadLettered

Keep retry policy simple initially.

Make retry calculation centralized.

Serialization

Use System.Text.Json.

Persist:

PayloadJson
MetadataJson
MessageType

MessageType should be stable.

Avoid using raw CLR full names as the only persisted contract if possible.

Prefer explicit message type names:

incidents.incident-registered.v1
notifications.send-notification.v1
operations.operation-completed.v1

Create a message type registry mapping:

message type name -> CLR type
CLR type -> message type name

This is important for future compatibility and refactoring.

Versioning

Support versioned message names from the beginning.

Example:

ModularTemplate.incidents.incident-registered.v1
ModularTemplate.notifications.send-message.v1

Rules:

Do not break existing message payloads casually.
Add v2 when payload contract changes incompatibly.
Keep old handlers if unprocessed old messages may exist.

This matters because messages are persisted.

Observability

Add structured logging around:

domain event persisted
outbox message created
outbox dispatch started
outbox dispatch succeeded
outbox dispatch failed
inbox message received
inbox processing started
inbox processing succeeded
inbox processing failed
message dead-lettered
operation created
operation completed
operation failed

Always include:

MessageId
CorrelationId
CausationId
OperationId
SourceModule
TargetModule
MessageType
AttemptCount

Do not overdo metrics initially, but keep the logging fields consistent.

HTTP Behavior for Durable Work

For endpoints that initiate durable workflows:

Return:

202 Accepted
Location: /api/operations/{operationId}

Response body:

{
  "operationId": "...",
  "status": "Pending"
}

For immediate commands:

Return normal synchronous result:

201 Created

or:

200 OK

Be explicit in endpoint naming and behavior.

Do not pretend durable commands completed synchronously.

Initial Implementation Scope

Do not try to migrate the entire app at once.

Implement the infrastructure with one or two real module examples.

Recommended first modules:

Operations
Notifications

or:

Operations
One existing ModularTemplate domain module

Notifications is a good candidate because it naturally fits durable side effects.

Operations is needed because durable command results need a place to live.

Step-by-Step Implementation Plan
Step 1: Inspect current ModularTemplate structure

Review:

solution layout
existing modules
existing DbContext usage
current migrations
current endpoint registration
current Mediator usage
current domain event implementation if any
current background services
current project references

Produce a short internal note in code comments or a temporary markdown file describing what currently exists.

Do not make assumptions.

Step 2: Introduce module DbContext/schema convention

For each selected module:

Create module DbContext.
Set default schema.
Configure migrations history table for module schema.
Move module EF configurations under module infrastructure.
Ensure module entities are only mapped in owning module DbContext.

Acceptance criteria:

App starts.
Migrations can be generated per module.
Database schema is module-specific.
No selected module depends on a global app DbContext.
Step 3: Add common messaging abstractions

Create shared abstractions in an appropriate shared kernel or building-blocks project.

Suggested abstractions:

IDomainEvent
IIntegrationEvent
IDurableCommand
IMessage
IMessageHandler
IDurableCommandHandler<T>
IIntegrationEventHandler<T>
MessageContext
CommandSubmission
CommandSubmissionStatus

Keep shared abstractions minimal.

Do not add business concepts here.

Step 4: Add persistent domain event model

For selected module:

Add domain_events table.
Add aggregate domain event collection.
Capture domain events during SaveChanges/UnitOfWork.
Persist domain event records in same transaction.

Acceptance criteria:

When aggregate behavior raises a domain event, it is persisted.
Domain event persists with aggregate state.
If transaction fails, neither aggregate changes nor domain event persist.
Step 5: Add outbox model

For selected module:

Add outbox_messages table.
Add outbox entity/configuration.
Add service for appending outbox messages.
Add integration event mapper from selected domain event.
Persist outbox messages in same transaction as aggregate changes.

Acceptance criteria:

When selected domain event occurs, mapped integration event is written to outbox.
If aggregate transaction fails, outbox message is not persisted.
If transaction succeeds, outbox message is pending for dispatch.
Step 6: Add inbox model

For selected consumer module:

Add inbox_messages table.
Add inbox entity/configuration.
Add deduplication by MessageId.
Add status/attempt fields.

Acceptance criteria:

Dispatcher can insert pending inbox message.
Duplicate MessageId is ignored or handled idempotently.
Inbox message can transition Pending -> Processing -> Processed.
Step 7: Add message type registry

Create registry that maps:

CLR type -> stable message type name
stable message type name -> CLR type

Example:

registry.Register<IncidentRegisteredIntegrationEvent>(
    "ModularTemplate.incidents.incident-registered.v1");

Acceptance criteria:

Outbox messages persist stable message type names.
Inbox processor can deserialize based on message type.
Renaming CLR type does not automatically break persisted messages.
Step 8: Add local subscription registry

Create registry for local delivery.

It should support:

event subscriptions: one event -> many target modules/handlers
command routing: one command -> one target module/handler

Acceptance criteria:

Dispatcher can resolve subscribers for integration event.
Dispatcher can resolve target for durable command.
Missing command target is treated as configuration error.
Event with no subscribers is allowed.
Step 9: Implement outbox dispatcher

Create BackgroundService.

Behavior:

Poll pending outbox messages.
Claim batch safely.
Resolve subscribers/target.
Write messages to consumer inboxes.
Mark outbox message dispatched.
Handle failures and retries.

Acceptance criteria:

Pending outbox messages are delivered to inbox.
Already dispatched messages are not delivered again.
Failures are retried.
Messages eventually dead-letter after max attempts.
Step 10: Implement inbox processor

Create BackgroundService.

Behavior:

Poll pending inbox messages.
Claim batch safely.
Resolve strongly typed handler.
Deserialize payload.
Execute handler in target module transaction.
Mark processed on success.
Retry on failure.
Dead-letter after max attempts.

Acceptance criteria:

Pending inbox messages invoke handler.
Handler can mutate target module state.
Handler can create new domain events/outbox messages.
Message is not marked processed if handler transaction fails.
Step 11: Add Operations module

Add Operations module with:

OperationsDbContext
operations schema
Operation aggregate/entity
OperationStep entity if needed
endpoint to query operation status
contracts for operation status

Minimum API:

GET /api/operations/{operationId}

Acceptance criteria:

Durable workflow can create operation.
Operation status can be queried.
Operation can be marked Completed or Failed by message handlers.
Step 12: Implement one end-to-end durable workflow

Pick a simple real ModularTemplate scenario.

Example shape:

HTTP endpoint receives command.
Endpoint creates operation.
Application stores operation.
Application writes durable command/event to outbox.
Outbox dispatcher delivers to target inbox.
Target inbox handler processes message.
Target publishes completion event.
Operations module receives completion event.
Operation becomes Completed.
GET /api/operations/{id} returns Completed.

Acceptance criteria:

End-to-end flow works after app restart.
If app crashes after outbox write but before processing, message is eventually processed after restart.
If handler fails, message retries.
If handler keeps failing, message is dead-lettered and operation can reflect failure.
Step 13: Add in-process query contract example

Pick one module that another module needs to query.

Create:

{Module}.Contracts/I{Module}Queries.cs
Implementation in module infrastructure/application
Registration in module DI
Consumer uses interface only

Acceptance criteria:

Consumer can query module read model in-process.
Consumer does not reference target module Application/Infrastructure/Domain.
Query returns DTO/read model only.
Step 14: Add architecture tests

Add tests to enforce dependencies.

Rules:

Domain projects do not reference Application, Infrastructure, Endpoints, other modules.
Application projects do not reference Infrastructure.
Contracts projects do not reference EF Core or ASP.NET Core.
Modules do not reference other modules except Contracts.
No module references another module's Infrastructure.
No module references another module's Application.
No module references another module's Domain.

Acceptance criteria:

Architecture tests fail on forbidden references.
Current solution passes.
Step 15: Add integration tests

Add tests for:

domain event persistence
outbox creation
outbox dispatch to inbox
inbox processing success
inbox retry on failure
inbox idempotency
operation status completion
message type registry resolution

Use real PostgreSQL if the project already supports test containers.

Avoid mocking the entire persistence flow.

Durability should be tested against real database behavior.

Step 16: Document conventions

Add or update project documentation:

docs/architecture/modular-monolith.md
docs/architecture/messaging.md
docs/architecture/module-contracts.md

Document:

when to use in-process query
when to use immediate command
when to use durable command
when to create integration event
how domain events become integration events
how operation status works
how to add a new module
how to add a new durable message
how to add a new subscriber

Keep docs concise but actionable.

Non-Goals for First Pass

Do not implement external broker yet.

Do not introduce Wolverine yet.

Do not introduce Quartz/Hangfire yet.

Do not convert every module at once.

Do not create integration events for every domain event.

Do not create generic CRUD integration events.

Do not expose every module operation as public contract.

Do not build distributed transactions.

Do not build full event sourcing.

Do not build advanced replay UI.

Do not build complex saga DSL.

Do not over-generalize before one end-to-end flow works.

Design Constraints

The implementation should be:

explicit
boring
debuggable
testable
dependency-light
production-realistic
suitable for template extraction

Prefer clarity over clever abstractions.

Prefer a small working vertical slice over broad incomplete infrastructure.

Prefer strongly typed handlers over dynamic runtime magic.

Prefer stable message names over raw CLR type names.

Prefer module ownership over global shared services.

Expected Final Shape

After this refactor, ModularTemplate should support:

- module-owned DbContexts and schemas
- persistent domain events
- explicit domain-event-to-integration-event mapping
- durable outbox publishing
- durable inbox consumption
- local message dispatcher
- local inbox processors
- operation tracking
- in-process query contracts
- durable command submissions
- retry/dead-letter behavior
- architecture boundaries
- end-to-end durable workflow test

This should become the reference implementation for updating the reusable modular monolith template.

Suggested Work Strategy for Agent

Use high reasoning for architecture and boundary changes.

Work in small vertical slices.

Do not attempt a full rewrite.

Preferred sequence:

1. Inspect existing structure.
2. Introduce abstractions.
3. Convert one module to module DbContext/schema.
4. Add persistent domain events.
5. Add outbox.
6. Add inbox.
7. Add dispatcher/processor.
8. Add Operations module.
9. Build one end-to-end durable flow.
10. Add tests.
11. Document conventions.
12. Only then generalize.

At every step, keep the app compiling and runnable.

If a design decision is ambiguous, prefer the simpler implementation that preserves module boundaries and can be generalized later.
```
