# Scripts

Repository helper scripts live here.

## Available Scripts

- `check-api-client.js` verifies that the checked-in OpenAPI document and
  generated frontend API client are current. The root `openapi:generate` and
  `api-client:generate` package scripts refresh those artifacts directly
  through `dotnet build` and the API-client package generator.

Script files are Node ES modules. The root package sets `"type": "module"` so
`.js` scripts use `import`/`export` syntax; `commitlint.config.cjs` remains
CommonJS explicitly because that tool expects it.
