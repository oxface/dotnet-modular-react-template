# OpenSpec Usage

OpenSpec is this repository's default spec-driven development workflow.
Project-specific OpenSpec context and artifact rules live in
[`../openspec/config.yaml`](../openspec/config.yaml). That file routes agents
to the stable docs and carries concise artifact rules, but
[governance.md](governance.md) remains the hard-rules source.

## When To Use It

Use OpenSpec for system behavior that needs durable acceptance criteria,
user-visible semantics, cross-artifact planning, or accepted current-state
capability specs. A change can be mostly technical and still need OpenSpec when
it changes product, domain, runtime, API, persistence, or durable architecture
semantics.

Pure refactors, file moves, class renames, and verification-only cleanup that
do not change accepted behavior should use normal docs or todo handoffs instead
of accepted specs. Specs must describe observable and testable behavior, not
implementation mechanics.

Do not add domain behavior, auth/session plumbing, generated migrations,
frontend apps, orchestration resources, CI workflows, generated clients,
template automation, durable intermodule messaging, or outbox processing
without an accepted OpenSpec change or durable architecture decision that states
the scope.

## Preferred Flow

1. Read [governance.md](governance.md), relevant stable docs, current specs
   under `../openspec/specs/`, and active changes under
   `../openspec/changes/`.
2. Create a change with `openspec new change <name>`.
3. Write `proposal.md`, behavior capability specs under `specs/`, `design.md`
   when the change is cross-cutting, and `tasks.md`.
4. Validate with `openspec validate <change-name> --strict`.
5. Implement and verify the task list.
6. Archive accepted changes with `openspec archive <change-name> --yes` so
   current behavior is merged into `openspec/specs/`.

## Accepted Specs

Accepted behavior lives under `openspec/specs/`. Active proposed behavior lives
under `openspec/changes/`.

The template intentionally starts with empty specs and changes directories.
Technical, governance, and verification-only migration details may remain in
archived changes, but they do not become active capability specs unless they
define observable system behavior.

Changes modify the existing capability spec that owns the behavior instead of
re-specifying an entire historical feature.

## Tooling

OpenSpec is initialized in this repository under `openspec/`, with
project-specific context in `openspec/config.yaml` and Codex skills under
`.agents/skills`.

Run [setup-openspec.sh](../scripts/setup-openspec.sh) to install the pinned
OpenSpec CLI globally when a fresh environment does not already have the
`openspec` command.

OpenSpec is intentionally not installed from devcontainer lifecycle hooks.
