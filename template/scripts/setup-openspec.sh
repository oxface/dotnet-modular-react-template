#!/usr/bin/env bash
set -euo pipefail

OPEN_SPEC_VERSION="1.3.1"
MIN_NODE_VERSION="20.19.0"
FORCE=0

usage() {
  cat <<USAGE
Usage: scripts/setup-openspec.sh [--force]

Installs @fission-ai/openspec@${OPEN_SPEC_VERSION} globally and initializes
OpenSpec with Codex support.

Options:
  --force   Allow openspec init to reuse or clean up an existing openspec/
            setup. Without this flag, the script refuses to run when
            openspec/ already exists.
USAGE
}

while (($#)); do
  case "$1" in
    --force)
      FORCE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if ! command -v node >/dev/null 2>&1; then
  echo "Node.js is required. Install Node.js >= ${MIN_NODE_VERSION}." >&2
  exit 1
fi

node - "${MIN_NODE_VERSION}" <<'NODE'
const required = process.argv[2].split('.').map(Number);
const actual = process.versions.node.split('.').map(Number);
for (let i = 0; i < required.length; i += 1) {
  if (actual[i] > required[i]) process.exit(0);
  if (actual[i] < required[i]) {
    console.error(`Node.js >= ${process.argv[2]} is required; found ${process.versions.node}.`);
    process.exit(1);
  }
}
NODE

if [ -d openspec ] && [ "${FORCE}" -ne 1 ]; then
  echo "Refusing to overwrite existing openspec/. Re-run with --force if intentional." >&2
  exit 1
fi

npm install --global "@fission-ai/openspec@${OPEN_SPEC_VERSION}"

INIT_ARGS=(init --tools codex .)
if [ "${FORCE}" -eq 1 ]; then
  INIT_ARGS+=(--force)
fi

openspec "${INIT_ARGS[@]}"
openspec --version

