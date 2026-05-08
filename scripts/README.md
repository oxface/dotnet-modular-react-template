# Scripts

Template-factory helper scripts live here.

## Available Scripts

- `bootstrap-template.js` creates a product-named repository from
  `template/`.
- `verify-bootstrap.js` creates a temporary sample product repository and
  checks the rename/bootstrap path.

Script files are Node ES modules. The root package sets `"type": "module"` so
`.js` scripts use `import`/`export` syntax.

## Template Bootstrap

Create a product repository copy:

```sh
pnpm template:bootstrap -- --product-name "Acme Desk" --output ../acme-desk
```

Preview the derived names and target path without writing files:

```sh
pnpm template:bootstrap -- --product-name "Acme Desk" --output ../acme-desk --dry-run
```

The first bootstrap version accepts one product name and derives:

- namespace/project prefix: `AcmeDesk`
- slug: `acme-desk`
- database slug: `acme_desk`
- npm scope: `@acme-desk`
- display name: `Acme Desk`

Run focused bootstrap verification:

```sh
pnpm verify
```

Run full generated-repository verification:

```sh
pnpm template:verify:full
```
