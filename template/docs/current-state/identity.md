# Identity Current State

Identity owns template-level local identity and application authorization
behavior.

The shipped Identity module includes:

- Provider-neutral current-user contracts.
- Local user aggregates mapped to OIDC subjects.
- Application access aggregates for application-owned access state.
- Bondstone command handlers.
- Module-owned repository contracts.
- Infrastructure repository implementations through a narrow Identity DbContext
  interface.

A valid authenticated principal with a stable provider subject lazily creates
or updates one local user by `(provider, subject)`. Missing or inactive
application access means the user is authenticated without product access; the
user does not become unauthenticated.

The setup command can grant one configured provider/subject pair active initial
admin/application access. It is idempotent while access is active and refuses
to silently reactivate revoked access unless the caller explicitly forces the
operation.

Identity records these stable domain event types:

- `identity.local-user-created`
- `identity.local-user-seen`
- `identity.application-access-granted`
- `identity.application-access-revoked`
