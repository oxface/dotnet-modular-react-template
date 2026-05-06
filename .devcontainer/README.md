# Devcontainer

This devcontainer provides the template's expected tooling: .NET 10, Aspire,
Docker-in-Docker, Node, pnpm, Playwright, PowerShell, Python, uv, Podman, and
ripgrep.

The container is an onboarding accelerator, not the source of truth. Durable
project knowledge should live in repository files under `docs/`, Spec Kit
artifacts, and local README/AGENTS files.

Spec Kit is intentionally not installed by devcontainer lifecycle hooks. The
container provides prerequisites such as Python and `uv`; repository Spec Kit
initialization happens through `scripts/setup-speckit.sh` so generated
`.specify/` and `.agents/` files stay reviewable.

Rebuilding the container can lose tool-local state if that state lives only
inside the container filesystem. Keep AI-agent history, notes, credentials, and
other durable state host-mounted, backed up, or recorded in the repository where
appropriate.
