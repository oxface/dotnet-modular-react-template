# Identity Module

Identity is the first planned module because authentication and application
authorization are template-level concerns.

Identity owns:

- Local user identity records mapped to OIDC subjects.
- Lazy local user creation after successful authentication.
- Application-owned staff/admin authorization records.
- Current-user context contracts.
- Initial admin bootstrap records.

The Host owns OIDC challenge/callback/logout mechanics, cookie configuration,
Redis-backed session ticket storage, and API authentication behavior.

Identity-provider roles, groups, and organization membership are not
authoritative product authorization sources.
