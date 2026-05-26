# Change Proposal: Operations Status Slice

## Why

The modular monolith refactor requires a first-class Operations surface for durable workflows. The current template has no operations module, no operation status contract, and no operation status endpoint.

## Scope

This change introduces a narrow vertical slice:

- `Operations` contracts and domain aggregate for operation lifecycle state.
- Operations infrastructure query implementation backed by persistence.
- `GET /api/operations/{operationId}` endpoint returning operation status.
- Tests for aggregate transitions and endpoint behavior.

## Non-Goals

- Durable outbox dispatch/inbox processing workers.
- Cross-module message routing and handler execution.
- Full multi-module workflow orchestration.
- Generated migrations for operations schema in this slice.

## Governance Notes

This change touches runtime and persistence behavior and therefore is tracked under OpenSpec as required by repository governance.
