# Template Current State

Status: Gate checkpoint for the next implementation session.

This file is planning context for building the template itself. It is not
intended to be inherited as stable product documentation.

## Completed Gates

### Gate 1: Repo Skeleton And Documentation Indexes

Completed.

Includes:

- Root README and AGENTS index.
- Stable documentation indexes under `docs/`.
- Top-level skeleton folders for `server`, `web`, `orchestration`, `deploy`,
  `scripts`, and `openspec`.

### Gate 2: Solution And Repository Infrastructure

Completed.

Includes:

- `ModularTemplate.slnx`.
- `global.json`.
- `Directory.Build.props`.
- `Directory.Packages.props`.
- Root pnpm workspace files.
- Empty .NET project shells for Host, Migrator, ServiceDefaults, SharedKernel,
  Identity contracts/module/infrastructure, and Orchestration.

Gate 2 intentionally does not include domain behavior, persistence,
auth/session plumbing, frontend apps, Aspire resources, CI workflows, generated
migrations, tests, or template automation.

## Last Verified Tool Versions

- .NET SDK: `10.0.107`.
- Node: `v18.19.1`.
- pnpm: `10.33.3`.

## Last Verification Commands

These passed after Gate 2 before the Aspire-installed skill artifact was added:

```sh
dotnet restore ModularTemplate.slnx
dotnet build ModularTemplate.slnx --no-restore
pnpm format:check
```

At this checkpoint, `pnpm format:check` reports formatting drift in
`.github/skills/aspire/SKILL.md`. That file is an Aspire-installed skill
artifact, not Gate 1 or Gate 2 scaffold output, and was intentionally left
unchanged in this handoff.

## Next Intended Step

Add devcontainer/tooling support before proceeding with runtime code. This is a
tooling checkpoint, not Gate 3.

The next implementation session should use
[devcontainer-baseline.md](devcontainer-baseline.md) as context and keep local
non-container development supported.

Do not proceed to Gate 3 SharedKernel primitives until the devcontainer/tooling
step is reviewed.

## Notes

- `.github/skills/aspire/SKILL.md` is an Aspire-installed skill artifact. It was
  not part of Gate 1 or Gate 2 template scaffolding.
- Current staged changes include Gate 1 and Gate 2 work.
