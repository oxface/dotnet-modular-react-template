# Devcontainer Baseline

Status: Planning baseline

This file preserves the current devcontainer shape from the archived implementation before archived source context is removed. The future template may adjust names and versions, but should not lose the intent of this environment.

## Current Shape

```json
{
  "name": "ModularTemplate",
  "image": "mcr.microsoft.com/devcontainers/dotnet:dev-10.0-noble",
  "features": {
    "ghcr.io/devcontainers/features/dotnet:2": {},
    "ghcr.io/microsoft/aspire-devcontainer-feature/aspire:2": {},
    "ghcr.io/devcontainers/features/docker-in-docker:2": {},
    "ghcr.io/devcontainers/features/powershell:2.0.2": {},
    "ghcr.io/devcontainers/features/node:2.0.0": {},
    "ghcr.io/devcontainers/features/python:1": {},
    "ghcr.io/devcontainers-extra/features/uv:1": {},
    "ghcr.io/devcontainers-extra/features/podman-homebrew:1": {},
    "ghcr.io/schlich/devcontainer-features/playwright:0": {},
    "ghcr.io/devcontainers-extra/features/ripgrep:1": {}
  },
  "hostRequirements": {
    "cpus": 8,
    "memory": "16gb",
    "storage": "64gb"
  },
  "postStartCommand": "dotnet dev-certs https --trust",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csdevkit",
        "GitHub.copilot-chat",
        "microsoft-aspire.aspire-vscode",
        "openai.chatgpt",
        "esbenp.prettier-vscode",
        "dbaeumer.vscode-eslint",
        "bradlc.vscode-tailwindcss",
        "HashiCorp.terraform",
        "ms-azuretools.vscode-docker"
      ]
    }
  },
  "runArgs": ["--userns=keep-id"],
  "containerUser": "vscode",
  "remoteUser": "vscode"
}
```

## Intent

- Provide a near-ready .NET 10, Aspire, Node, pnpm/Vite, Playwright, Docker, Python, uv, and ripgrep development environment.
- Support local platform orchestration through Aspire.
- Support browser/e2e testing without ad hoc machine setup.
- Keep VS Code extension suggestions aligned with the stack.

## Known Concern

Rebuilding a devcontainer can lose AI-agent history or tool-local state if that state is stored only inside the container filesystem. The template should document this clearly and prefer host-mounted or repo-backed state for durable project knowledge.

The devcontainer is an onboarding accelerator, not the source of truth. Durable decisions, instructions, and progress must live in repository files.
