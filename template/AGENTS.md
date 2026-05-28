# Agent Index

This repository is a domain-neutral .NET + React modular-monolith template.

Start with:

- [docs/README.md](docs/README.md) for stable documentation.
- [docs/governance.md](docs/governance.md) for hard project rules.
- Relevant architecture, platform, module, and testing docs under `docs/`.

Hard rules summary:

Do not add domain behavior, auth/session plumbing, generated migrations,
frontend apps, orchestration resources, CI workflows, generated clients,
template automation, durable intermodule messaging, or outbox processing unless
durable architecture decisions state the scope.
Product authorization must remain application-owned and provider-neutral.
Backend module contracts must not expose EF entities, aggregate internals,
provider SDK types, infrastructure details, `ClaimsPrincipal`, or Host-specific
HTTP concepts.

Use [docs/governance.md](docs/governance.md) for hard project rules. Before
proposing or implementing substantial runtime behavior, read the governance,
architecture, platform, module, and testing docs relevant to the change. Record
durable product or architecture decisions in stable docs before implementing
broad runtime behavior.

Keep stable docs pure: durable rules and intent belong in architecture,
platform, module, testing, and governance docs; transient implementation status
belongs in `docs/current-state/` or feature artifacts.
