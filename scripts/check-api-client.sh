#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
client_dir="$repo_root/web/packages/api-client"
snapshot_dir="$(mktemp -d)"

cleanup() {
  rm -rf "$snapshot_dir"
}
trap cleanup EXIT

mkdir -p "$snapshot_dir/openapi" "$snapshot_dir/generated"

if [ -d "$client_dir/openapi" ]; then
  cp -R "$client_dir/openapi/." "$snapshot_dir/openapi/"
fi

if [ -d "$client_dir/src/generated" ]; then
  cp -R "$client_dir/src/generated/." "$snapshot_dir/generated/"
fi

"$repo_root/scripts/generate-api-client.sh"

diff -qr "$snapshot_dir/openapi" "$client_dir/openapi"
diff -qr "$snapshot_dir/generated" "$client_dir/src/generated"
