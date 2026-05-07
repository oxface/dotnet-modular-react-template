## Context

The Host exposes the first accepted API endpoint, `GET /api/me`, and the React
apps already use shared browser-safe helpers for current-user state. Those
helpers currently duplicate the response shape manually in TypeScript. As the
template grows, hand-maintained browser contracts will make backend/frontend
drift easy to miss.

The generated client boundary must preserve the existing BFF/session model:
browser code calls same-origin `/api/` routes with the application cookie and
must not receive, store, or forward identity-provider tokens.

## Goals / Non-Goals

**Goals:**

- Generate an OpenAPI document from the Host-owned API surface.
- Generate a browser-safe TypeScript client package from that document.
- Make the generation workflow repeatable and verifiable from repo scripts.
- Replace hand-maintained current-user client types with generated API types and
  calls.
- Keep the generated client package reusable by both frontend apps and shared
  auth helpers.

**Non-Goals:**

- Do not add new business/domain endpoints.
- Do not add a public API documentation UI.
- Do not expose auth routes as generated API operations.
- Do not generate clients for identity-provider APIs.
- Do not add OIDC end-to-end browser smoke UI in this change; that is the
  follow-up gate.
- Do not introduce provider tokens or provider authorization payloads into
  browser code.

## Decisions

### Use ASP.NET Core built-in OpenAPI as the source

The Host should use the built-in ASP.NET Core OpenAPI support through
`Microsoft.AspNetCore.OpenApi`, with build-time document generation enabled by
`Microsoft.Extensions.ApiDescription.Server`.

Rationale:

- It keeps the API document owned by the Host API metadata instead of a parallel
  handwritten contract.
- It avoids adding Swagger UI or other runtime documentation surfaces before the
  template has a real need for them.
- Build-time document output gives frontend generation and drift checks a
  deterministic source.

Alternative considered: Swashbuckle. It is familiar, but the built-in OpenAPI
stack is a better fit for the current .NET template baseline and keeps the
surface smaller.

### Generate a dedicated frontend package

Create `web/packages/api-client` as the generated-client package. Generated code
should live under a clearly marked subfolder such as `src/generated/`, with any
small hand-written same-origin configuration or exports kept outside the
generated folder.

Rationale:

- Both apps and shared packages can consume one client boundary.
- Generated files stay isolated from hand-written package code.
- The eventual template bootstrap/rename script has one obvious package to
  rename and verify.

### Use a fetch-based TypeScript OpenAPI generator

Use `@hey-api/openapi-ts` with its default Fetch client as the initial
generator.

Rationale:

- It produces TypeScript SDKs and types from OpenAPI and supports pluggable
  clients while defaulting SDK generation to Fetch API.
- A fetch client matches browser same-origin cookie behavior without adding an
  Axios dependency or a separate generated-client runtime package.
- Hey API has first-class TanStack Query generation. This gate intentionally
  keeps query integration in the existing auth package; a follow-up gate should
  enable generated query helpers once the OIDC/browser-session smoke UI proves
  whether apps should use generated helpers directly or template-owned wrappers.

Alternative considered: `openapi-typescript` plus `openapi-fetch`. That would
also be reasonable and smaller, but the generated SDK shape from Hey API better
matches the template's goal of a reusable client package for future endpoints.

### Keep same-origin behavior in package configuration

The generated client package must default to relative same-origin API calls.
Browser consumers should not configure an identity-provider origin, bearer
token, refresh token, or provider-specific header.

Rationale:

- The accepted auth/session behavior relies on opaque application cookies and
  Host-owned OIDC mechanics.
- Frontend Vite proxying already maps same-origin `/api/` and `/auth/` routes to
  the Host in local development.

### Verify generation drift explicitly

Add scripts that generate the Host OpenAPI document, regenerate the frontend
client, and check whether generated output is up to date. The validation path
for this gate should include backend build/test plus frontend typecheck, tests,
build, lint, formatting, generated-client freshness checks, and OpenSpec
validation. The same freshness check should run from the local pre-commit hook
and future CI workflows.

Rationale:

- Generated code is only useful if drift is visible.
- The template should teach downstream products how to update clients
  intentionally rather than relying on ad hoc local commands.

## Risks / Trade-offs

- Generator output may be noisy or change across package upgrades. Mitigation:
  pin package versions centrally in `package.json` and keep generated files
  isolated.
- Minimal API metadata may need explicit names, response types, and tags to
  generate useful operation names. Mitigation: update endpoint metadata in this
  gate and test the generated call from `web/packages/auth`.
- Build-time OpenAPI generation can accidentally include non-API or auth routes.
  Mitigation: define the document boundary around `/api/` endpoints and verify
  the generated document does not expose provider token material.
- Generated clients can tempt browser code to call absolute service URLs.
  Mitigation: package configuration defaults to same-origin relative paths and
  tests assert current-user calls use relative `/api/me` behavior.
