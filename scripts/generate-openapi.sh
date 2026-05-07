#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
host_project="$repo_root/server/src/ModularTemplate.Host/ModularTemplate.Host.csproj"
openapi_dir="$repo_root/web/packages/api-client/openapi"

mkdir -p "$openapi_dir"

export ASPNETCORE_ENVIRONMENT=Development

dotnet build "$host_project" \
  -p:OpenApiGenerateDocuments=true \
  -p:OpenApiDocumentsDirectory="$openapi_dir" \
  -p:OpenApiGenerateDocumentsOptions="--file-name host"
