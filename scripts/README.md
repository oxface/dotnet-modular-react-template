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

The output path may be missing or may already be an existing repository. The
template payload does not include `README.md` or `LICENSE`, so repository-host
defaults can remain in place. Bootstrap stops if any generated path would
replace an existing file or directory.

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

`pnpm verify` also runs `pnpm framework:test`, which covers template-framework
behavior that should not be copied as generated-product test examples.

Run full generated-repository verification:

```sh
pnpm template:verify:full
```
