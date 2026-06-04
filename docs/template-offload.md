# Template Offload Pause

This note records the intended direction for this template factory after
Bondstone is ready to act as the shared modular-monolith foundation library.
No implementation changes are planned here until that upstream split is ready.

## Intended Role

Bondstone is the domain-neutral library that enables modular-monolith product
repositories. It should provide reusable runtime and framework behavior without
product-specific modules or business workflows.

This repository should remain the product-repository initializer for new
Bondstone-backed products. Its job is to create the first commit shape for a
new repository, not to carry a second copy of Bondstone implementation code.

## Future Boundary

Bondstone should own reusable foundation behavior, such as hosting, module
composition, authentication/session support, persistence conventions,
observability, testing helpers, and other runtime mechanics that should be
implemented once and reused by generated products.

This template factory should own generated repository shape and composition,
including:

- solution, project, folder, and workspace layout;
- bootstrap naming and rename behavior;
- basic product-owned projects and easily renamable module skeletons;
- Aspire local platform setup for common development needs;
- working local authentication setup against Keycloak;
- CI, Dependabot, release, and repository-maintenance defaults;
- generated-product docs, governance, and agent instructions;
- references to Bondstone packages.

## Relevance Decision

The template remains relevant because Bondstone alone does not initialize a
complete product repository. A GitHub template can copy static files, and AI
skills can adapt a setup interactively, but this factory should provide a
deterministic, tested bootstrap path that wires a new product repo to the same
foundation Bondstone uses.

Public reuse is acceptable but not the goal. The primary goal is to make future
personal product repositories start from the preferred Bondstone-backed
foundation quickly and consistently.

## Pause State

Stop expanding this template's runtime surface until Bondstone is ready. When
work resumes, remove copied Bondstone implementation code from the template and
replace it with package references and product-owned composition.
