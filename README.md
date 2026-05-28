# dotnet-modular-react-template

Template factory for a domain-neutral .NET + React modular-monolith product
repository.

The generated-product payload lives under `template/`. Root files maintain the
factory: bootstrap automation, factory verification, release notes, and
maintainer documentation.

## Use The Template

After publication, install and run the bootstrap CLI like any other npm tool:

```sh
pnpm add --global dotnet-modular-react-template

dotnet-modular-react-template --product-name "Acme Desk" --output ../acme-desk
```

For one-off use without a global install:

```sh
pnpm dlx dotnet-modular-react-template -- --product-name "Acme Desk" --output ../acme-desk
```

When developing the factory locally, use the root script:

```sh
pnpm template:bootstrap -- --product-name "Acme Desk" --output ../acme-desk
```

The output path may be missing or may already be an existing repository. The
template payload does not include `README.md` or `LICENSE`, so repository-host
defaults can remain in place. Bootstrap stops if any generated path would
replace an existing file or directory.

To test the installable package before publication, pack it and run the tarball:

```sh
mkdir -p /tmp/dotnet-modular-react-template-pack
pnpm pack --pack-destination /tmp/dotnet-modular-react-template-pack
pnpm dlx /tmp/dotnet-modular-react-template-pack/dotnet-modular-react-template-0.0.0.tgz -- --product-name "Acme Desk" --output /tmp/acme-desk
```

The bootstrap command accepts one display-oriented product name and derives the
.NET namespace/project prefix, npm scope, local service slugs, database names,
and visible display text.

## Maintain The Factory

Use Node 24 and pnpm 10.33.x for factory maintenance.

Useful maintenance commands:

- `pnpm verify`
- `pnpm pack`
- `pnpm scripts:lint`
- `pnpm template:verify`
- `pnpm template:verify:full`

Factory maintenance documentation lives under `docs/`:

- `docs/README.md` indexes factory-maintainer docs.
- `docs/template-decisions.md` records durable template-factory decisions.
- `docs/testing.md` records release and bootstrap validation steps.

Generated-product documentation lives under `template/docs/`; generated-product
agent context starts at `template/AGENTS.md` and
`template/docs/governance.md`. Generated-product implementation progress is
indexed under `template/docs/current-state/`.
