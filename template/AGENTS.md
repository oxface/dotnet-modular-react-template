# Agent Index

This repository is a domain-neutral .NET + React modular-monolith template.

Start with:

- [docs/README.md](docs/README.md) for stable documentation.
- [docs/governance.md](docs/governance.md) for hard project rules.
- [docs/openspec.md](docs/openspec.md) for the spec-driven workflow.
- [openspec/config.yaml](openspec/config.yaml) for OpenSpec workflow context.
- Current specs under `openspec/specs/` for accepted behavior.
- Active changes under `openspec/changes/` for proposed behavior.

Hard rules summary:

Do not add domain behavior, auth/session plumbing, generated migrations,
frontend apps, orchestration resources, CI workflows, generated clients,
template automation, durable intermodule messaging, or outbox processing unless
accepted OpenSpec artifacts or durable architecture decisions state the scope.
Product authorization must remain application-owned and provider-neutral.
Backend module contracts must not expose EF entities, aggregate internals,
provider SDK types, infrastructure details, `ClaimsPrincipal`, or Host-specific
HTTP concepts.

<!-- OPENSPEC START -->

Use [docs/governance.md](docs/governance.md) for hard project rules. Before
proposing or implementing substantial runtime behavior, read
`docs/governance.md`, `docs/README.md`, `docs/openspec.md`,
`openspec/config.yaml`, relevant stable docs, and existing OpenSpec current
specs. OpenSpec is initialized with Codex skills under `.agents/skills`.
Accepted current behavior is maintained under `openspec/specs/`. Active
changes live under `openspec/changes/`.

Use `openspec/config.yaml` as workflow context, but do not treat it as the only
source of durable rules. Archive accepted changes so `openspec/specs/` remains
the current source of truth.

Use OpenSpec for durable product, domain, runtime, API, persistence, or
architecture behavior, even when the work is mostly technical. Keep accepted
specs focused on observable and testable behavior, not implementation
mechanics. Pure refactors, file moves, class renames, and verification-only
cleanup that do not change accepted behavior should use normal docs or todo
handoffs instead of adding technical requirements to `openspec/specs/`. When a
handoff mixes domain semantics with technical cleanup, put the semantics in
OpenSpec specs and keep the mechanics in `design.md`, `tasks.md`, stable docs,
or the handoff.

<!-- OPENSPEC END -->

Keep stable docs pure: durable rules and intent belong in architecture,
platform, module, testing, and governance docs; transient implementation status
belongs in `docs/current-state/` or feature artifacts.
