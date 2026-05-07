#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$repo_root/scripts/generate-openapi.sh"
pnpm --dir "$repo_root" --filter @modular-template/api-client generate
