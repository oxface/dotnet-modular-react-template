# Operations Module

Operations owns template-level operation status tracking for durable or
long-running work.

Operations owns:

- Operation lifecycle state and timestamps.
- Provider-neutral operation status contracts.
- Module-owned persistence for operation records.
- The `GET /api/operations/{operationId}` status endpoint.

Operations does not own cross-module business workflows by default. Product
repositories should add workflow-specific state, steps, commands, events, and
process managers only when accepted product behavior defines them.

Implementation progress for the shipped template lives in
[../current-state/server.md](../current-state/server.md).
