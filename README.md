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

Factory planning and release-readiness notes live under `docs/`.
