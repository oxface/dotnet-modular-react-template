# Design: Operations Status Slice

## Module Boundaries

- `ModularTemplate.Operations.Contracts`: query DTOs and query interface.
- `ModularTemplate.Operations`: aggregate and endpoint mapping extension.
- `ModularTemplate.Operations.Infrastructure`: EF configuration, repository, query implementation.

Cross-module access is through contracts (`IOperationsQueries`) only.

## Persistence

The Operations module owns `OperationsDbContext`. This slice adds an `Operation` entity mapping under the `operations` schema and surfaces it through `IOperationsDbContext` for operations infrastructure.

## HTTP Behavior

`GET /api/operations/{operationId}` returns:

- `200 OK` with operation details when found.
- `404 Not Found` when no operation exists.
- `401 Unauthorized` when unauthenticated.

## Verification

- Operations unit tests validate state transitions and domain events.
- Host tests validate endpoint success and not-found behavior.
- Existing identity infrastructure integration tests verify no regression in persistence setup.
