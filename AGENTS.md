# Agent Index

This repository is a domain-neutral .NET + React modular-monolith template.

Start with:

- [README.md](README.md) for repository status and entrypoints.
- [docs/README.md](docs/README.md) for stable documentation.
- [docs/template/README.md](docs/template/README.md) for template-building
  planning context.

Current implementation gate: review Gate 5 Host foundation before persistence
foundation.

Do not add domain behavior, persistence, auth/session plumbing, generated
migrations, frontend apps, orchestration resources, CI workflows, or template
automation until the relevant gate is reviewed.

For a fresh handoff, treat `docs/template/current-state.md` as the source of
truth for completed gates, verification commands, and next scope.

<!-- SPECKIT START -->

Spec Kit is initialized with Codex skills under `.agents/skills`.
Use `.specify/memory/constitution.md` for hard project rules and
`docs/template/current-state.md` for the current gate before running Spec Kit
skills.

<!-- SPECKIT END -->
