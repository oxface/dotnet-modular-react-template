#!/usr/bin/env bash
set -euo pipefail

SPEC_KIT_VERSION="${SPEC_KIT_VERSION:-v0.8.5}"
ARCHIVE_VERSION="${ARCHIVE_VERSION:-v1.0.0}"
REFINE_VERSION="${REFINE_VERSION:-v1.0.0}"

FORCE=0
UPGRADE=0

usage() {
  cat <<'EOF'
Usage: scripts/setup-speckit.sh [--force] [--upgrade]

Initializes the approved Spec Kit setup for this template:

  - Spec Kit core, pinned by SPEC_KIT_VERSION
  - Codex integration files
  - Archive extension, pinned by ARCHIVE_VERSION
  - Refine extension, pinned by REFINE_VERSION

Options:
  --force    Allow setup to run when .specify already exists.
  --upgrade  Reinstall the pinned specify-cli version even if specify exists.

Environment overrides:
  SPEC_KIT_VERSION  Default: v0.8.5
  ARCHIVE_VERSION   Default: v1.0.0
  REFINE_VERSION    Default: v1.0.0
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --force)
      FORCE=1
      ;;
    --upgrade)
      UPGRADE=1
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [ -d ".specify" ] && [ "$FORCE" -ne 1 ]; then
  cat >&2 <<'EOF'
.specify already exists.

Refusing to overwrite the current Spec Kit setup. Re-run with --force only
after reviewing the existing .specify/ and .agents/ files.
EOF
  exit 1
fi

if ! command -v uv >/dev/null 2>&1; then
  echo "uv is required to install specify-cli. Install uv or use the devcontainer." >&2
  exit 1
fi

export PATH="$HOME/.local/bin:$PATH"

if ! command -v specify >/dev/null 2>&1 || [ "$UPGRADE" -eq 1 ]; then
  echo "Installing specify-cli ${SPEC_KIT_VERSION}..."
  uv tool install --force specify-cli \
    --from "git+https://github.com/github/spec-kit.git@${SPEC_KIT_VERSION}"
else
  echo "Using existing specify executable: $(command -v specify)"
fi

echo "Spec Kit version:"
specify --version

echo "Initializing Spec Kit with Codex integration..."
specify init --here --force --integration codex --script sh --no-git \
  --ignore-agent-tools

echo "Installing approved extensions..."
specify extension add archive \
  --from "https://github.com/stn1slv/spec-kit-archive/archive/refs/tags/${ARCHIVE_VERSION}.zip"
specify extension add refine \
  --from "https://github.com/Quratulain-bilal/spec-kit-refine/archive/refs/tags/${REFINE_VERSION}.zip"

echo "Installed Spec Kit integrations:"
specify integration list || true

echo "Installed Spec Kit extensions:"
specify extension list

cat <<'EOF'

Spec Kit setup complete.

Review generated .specify/ and .agents/ files before committing. Codex invokes
Spec Kit commands as $speckit-<command> skills.
EOF
