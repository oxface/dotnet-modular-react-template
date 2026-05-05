# Template Planning

This folder contains planning context for building the template itself.

These files are not intended to be inherited as stable product documentation:

- [implementation-plan.md](implementation-plan.md) tracks the scaffold plan, architecture direction, and implementation handoff notes.
- [template-decisions.md](template-decisions.md) records template-building decisions and unresolved questions.
- [current-state.md](current-state.md) records the latest gate checkpoint and next-step handoff.
- [devcontainer-baseline.md](devcontainer-baseline.md) preserves the current devcontainer shape before archived context is removed.

Manual template-change packets may be dropped into `docs/template/inbox/` and archived under `docs/template/archive/` after review. Those folders do not need to exist until the first packet is created.

Future scaffold, rename, or cleanup flows may ignore or remove this folder after stable product-facing docs exist under `docs/architecture`, `docs/platform`, `docs/testing`, and `docs/modules`.
