# Documentation

This folder contains stable product documentation for the generated
modular-monolith repository.

## Read Order

Start with [governance.md](governance.md). It is the hard-rules source for the
repository and supersedes conflicting generated instructions or local agent
memory. Then read the OpenSpec, architecture, platform, module, testing, and
current-state documents that are relevant to the change.

Hard rules summary:

- Substantial runtime behavior MUST start from accepted OpenSpec artifacts or a
  durable architecture decision before code is added.
- Durable project knowledge MUST live in versioned repository files.
- Backend modules MUST preserve explicit dependency direction and keep
  infrastructure/provider details out of module contracts.
- Product authorization MUST be application-owned and provider-neutral.
- Behavior and infrastructure changes MUST leave the repository in a
  verifiable state.

Stable architecture and product docs describe durable intent, rules, and
decision boundaries. Implementation progress and shipped-template inventory
belong under `current-state/` so stable docs do not become stale status
reports.

## Index

- [architecture.md](architecture.md) summarizes the intended system shape.
- [governance.md](governance.md) records hard project rules.
- [openspec.md](openspec.md) describes how this repository uses OpenSpec.
- [architecture/server.md](architecture/server.md) records backend architecture
  guidance.
- [architecture/intermodule-communication.md](architecture/intermodule-communication.md)
  records how modules should use contracts, Mediator, durable commands,
  integration events, outbox/inbox persistence, and Rebus transport.
- [architecture/web.md](architecture/web.md) records frontend architecture
  guidance.
- [architecture/orchestration.md](architecture/orchestration.md) records local
  orchestration guidance.
- [architecture/workflows.md](architecture/workflows.md) records workflow
  architecture guidance.
- [current-state/README.md](current-state/README.md) indexes implementation
  progress notes for the generated template.
- [modules/README.md](modules/README.md) indexes module documentation.
- [platform/README.md](platform/README.md) indexes platform concerns.
- [testing.md](testing.md) summarizes the testing strategy.

Before substantial runtime behavior is proposed or implemented, read
[governance.md](governance.md), this index, [openspec.md](openspec.md),
`../openspec/config.yaml`, relevant stable docs, current specs under
`../openspec/specs/`, and active changes under `../openspec/changes/`. Agent
indexes may summarize hard rules, but durable rules belong in these versioned
docs.
