# E2E Testing

End-to-end tests should cover controlled local-platform workflows once Aspire,
the Host, identity provider, Redis, PostgreSQL, Mailpit, and frontend apps are
runnable together.

E2E tests should not run by default until the full platform startup path is
stable enough for CI.
