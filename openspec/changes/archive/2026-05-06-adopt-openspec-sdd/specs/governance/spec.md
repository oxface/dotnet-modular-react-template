## ADDED Requirements

### Requirement: Domain-Neutral Template

The repository MUST remain a reusable .NET and React modular-monolith template and MUST NOT introduce product-specific domain behavior, sample business workflows, generated migrations, production secrets, or provider-bound authorization decisions without accepted scope.

#### Scenario: Product-specific behavior is proposed

- **WHEN** a change proposes product-specific domain behavior
- **THEN** the change is rejected unless accepted OpenSpec artifacts or durable
  architecture decisions explicitly state that scope

### Requirement: Reviewed Runtime Surface

Substantial runtime behavior MUST start from accepted OpenSpec artifacts or a durable architecture decision before code is added.

#### Scenario: Runtime surface is requested

- **WHEN** a change adds auth/session plumbing, persistence behavior, frontend
  apps, orchestration resources, CI workflows, generated clients, or template
  automation
- **THEN** the repository has accepted OpenSpec artifacts or a durable
  architecture decision that states the scope before implementation

### Requirement: Durable Decisions

Durable project knowledge MUST live in versioned repository files, with stable architecture rules under `docs/` and accepted feature behavior under `openspec/specs/`.

#### Scenario: Agent instruction contains an important rule

- **WHEN** an important project rule appears in agent instructions
- **THEN** the rule is also represented in the durable repository docs or
  OpenSpec current specs

### Requirement: Modular-Monolith Boundaries

Backend modules MUST preserve visible dependency direction and MUST NOT expose EF entities, aggregate internals, provider SDK types, or infrastructure details through module contracts.

#### Scenario: Module integration is designed

- **WHEN** one module needs behavior from another module
- **THEN** the integration uses contracts or Host composition without direct
  access to another module's DbSet surface

### Requirement: Proportionate Verification

Every behavior or infrastructure change MUST leave the repository in a verifiable state, with tests or explicit verification steps proportionate to the change's blast radius.

#### Scenario: Runtime behavior changes

- **WHEN** a change affects runtime behavior, shared abstractions,
  persistence, auth/session flows, frontend workflows, or generated clients
- **THEN** verification includes tests or explicit checks proportionate to that
  risk
