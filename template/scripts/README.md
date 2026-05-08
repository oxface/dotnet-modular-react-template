# Scripts

Repository helper scripts live here.

## Available Scripts

- `setup-openspec.sh` installs the pinned OpenSpec CLI and initializes Codex
  support. It refuses to reuse an existing `openspec/` directory unless
  `--force` is passed.
- `generate-openapi.js` generates the Host OpenAPI document used by the
  frontend API client package.
- `generate-api-client.js` refreshes the Host OpenAPI document and generated
  frontend API client.
- `check-api-client.js` verifies that the checked-in OpenAPI document and
  generated frontend API client are current.

Script files are Node ES modules. The root package sets `"type": "module"` so
`.js` scripts use `import`/`export` syntax; `commitlint.config.cjs` remains
CommonJS explicitly because that tool expects it.
