## 1. Frontend Smoke Surface

- [x] 1.1 Add a domain-neutral browser-session smoke surface to the admin app
      shell.
- [x] 1.2 Add the same browser-session smoke surface behavior to the web app
      shell.
- [x] 1.3 Keep smoke commands on the existing shared login/logout helper
      boundary.

## 2. Verification

- [x] 2.1 Add frontend app tests for unauthenticated,
      authenticated-without-access, and authenticated-with-access smoke states
      across both apps.
- [x] 2.2 Verify login/logout commands through the shared same-origin auth
      helper boundary.

## 3. Documentation And Query Decision

- [x] 3.1 Document the local browser-session smoke path in stable local
      services/orchestration docs.
- [x] 3.2 Record the MVP 1 decision to keep template-owned TanStack Query
      composition and defer Hey API generated query helpers.
- [x] 3.3 Update template planning docs so steps 14 and 15 are marked complete.

## 4. Validation

- [x] 4.1 Run frontend and OpenSpec validation for the completed change.
