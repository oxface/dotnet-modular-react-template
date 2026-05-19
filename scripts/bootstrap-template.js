#!/usr/bin/env node
import {
  cp,
  mkdir,
  mkdtemp,
  readFile,
  readdir,
  rename,
  rm,
  stat,
  writeFile,
} from "node:fs/promises";
import os from "node:os";
import { existsSync } from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");
const templateRoot = path.join(repoRoot, "template");

const manifest = {
  source: templateRoot,
  textFileExtensions: [
    ".cs",
    ".csproj",
    ".css",
    ".editorconfig",
    ".html",
    ".json",
    ".js",
    ".md",
    ".props",
    ".sh",
    ".slnx",
    ".ts",
    ".tsx",
    ".xml",
    ".yaml",
    ".yml",
  ],
  textFileNames: [".gitattributes", ".gitignore", ".prettierignore"],
  ignoredSegments: [
    ".git",
    ".pnpm-store",
    ".vs",
    "bin",
    "coverage",
    "dist",
    "node_modules",
    "obj",
    "playwright-report",
    "test-results",
  ],
  ignoredRelativePaths: [".husky/_", ".npmignore"],
  ignoredFileExtensions: [".lscache"],
  placeholders(names) {
    return [
      ["@modular-template", names.npmScope],
      ["ModularTemplate", names.pascal],
      ["Modular Template", names.display],
      ["modular-template", names.slug],
      ["modular_template", names.snake],
      ["dotnet-modular-react-template", names.slug],
    ];
  },
};

const textFileExtensions = new Set(manifest.textFileExtensions);
const textFileNames = new Set(manifest.textFileNames);
const ignoredSegments = new Set(manifest.ignoredSegments);
const ignoredRelativePaths = new Set(manifest.ignoredRelativePaths);
const ignoredFileExtensions = new Set(manifest.ignoredFileExtensions);
function usage() {
  console.log(
    `Usage: node scripts/bootstrap-template.js --product-name "Acme Desk" --output ../acme-desk [--dry-run]`,
  );
}

function parseArgs(argv) {
  const args = {
    dryRun: false,
    productName: "",
    output: "",
  };

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--") {
      continue;
    }

    if (arg === "--product-name") {
      args.productName = argv[++i] ?? "";
      continue;
    }

    if (arg === "--output") {
      args.output = argv[++i] ?? "";
      continue;
    }

    if (arg === "--dry-run") {
      args.dryRun = true;
      continue;
    }

    if (arg === "--help" || arg === "-h") {
      usage();
      process.exit(0);
    }

    throw new Error(`Unknown argument: ${arg}`);
  }

  return args;
}

function splitWords(productName) {
  const matches = productName
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .match(/[A-Za-z0-9]+/g);

  if (!matches?.length) {
    throw new Error(
      "Product name must contain at least one ASCII letter or digit.",
    );
  }

  return matches;
}

function toPascalCase(words) {
  return words
    .map((word) => {
      const hasInteriorCamelCase = /[a-z][A-Z]/.test(word);
      if (hasInteriorCamelCase) {
        return `${word[0].toUpperCase()}${word.slice(1)}`;
      }

      return `${word[0].toUpperCase()}${word.slice(1).toLowerCase()}`;
    })
    .join("");
}

function deriveNames(productName) {
  const words = splitWords(productName);
  const pascal = toPascalCase(words);
  const slug = words.map((word) => word.toLowerCase()).join("-");
  const snake = words.map((word) => word.toLowerCase()).join("_");
  const display = words
    .map((word) => `${word[0].toUpperCase()}${word.slice(1)}`)
    .join(" ");

  if (!/^[A-Za-z][A-Za-z0-9]*$/.test(pascal)) {
    throw new Error(
      "Product name must derive a namespace that starts with a letter.",
    );
  }

  return {
    display,
    npmScope: `@${slug}`,
    pascal,
    slug,
    snake,
  };
}

function getMappings(names) {
  return manifest.placeholders(names);
}

function normalizeRelative(relativePath) {
  return relativePath.split(path.sep).join("/");
}

function shouldExclude(src) {
  const relative = normalizeRelative(path.relative(manifest.source, src));
  if (!relative) {
    return false;
  }

  if (ignoredFileExtensions.has(path.extname(src))) {
    return true;
  }

  if (
    ignoredRelativePaths.has(relative) ||
    [...ignoredRelativePaths].some((ignored) =>
      relative.startsWith(`${ignored}/`),
    )
  ) {
    return true;
  }

  return relative.split("/").some((segment) => ignoredSegments.has(segment));
}

function isTextFile(filePath) {
  const name = path.basename(filePath);
  if (textFileNames.has(name)) {
    return true;
  }

  return textFileExtensions.has(path.extname(filePath));
}

async function walk(root) {
  const entries = await readdir(root, { withFileTypes: true });
  const results = [];

  for (const entry of entries) {
    const fullPath = path.join(root, entry.name);
    results.push(fullPath);
    if (entry.isDirectory()) {
      results.push(...(await walk(fullPath)));
    }
  }

  return results;
}

function applyMappings(value, mappings) {
  return mappings.reduce(
    (current, [from, to]) => current.split(from).join(to),
    value,
  );
}

async function rewriteFile(filePath, mappings) {
  if (!isTextFile(filePath)) {
    return;
  }

  const buffer = await readFile(filePath);
  if (buffer.includes(0)) {
    return;
  }

  const original = buffer.toString("utf8");
  const updated = applyMappings(original, mappings);
  if (updated !== original) {
    await writeFile(filePath, updated, "utf8");
  }
}

async function rewritePaths(outputRoot, mappings) {
  const entries = (await walk(outputRoot)).sort((a, b) => b.length - a.length);

  for (const currentPath of entries) {
    const parent = path.dirname(currentPath);
    const name = path.basename(currentPath);
    const rewrittenName = applyMappings(name, mappings);
    if (rewrittenName !== name) {
      await rename(currentPath, path.join(parent, rewrittenName));
    }
  }
}

async function removeTemplateOnlyGitignoreEntries(outputRoot) {
  const gitignorePath = path.join(outputRoot, ".gitignore");
  if (!existsSync(gitignorePath)) {
    return;
  }

  const gitignore = await readFile(gitignorePath, "utf8");
  const updated = gitignore.replace(
    /\n# Template-local generated EF migrations\.\n# Bootstrapped product repositories remove this block so they can commit their\n# own migration history\.\nserver\/src\/[^/\r\n]+\.Persistence\/Migrations\/\n/g,
    "\n",
  );

  if (updated !== gitignore) {
    await writeFile(gitignorePath, updated, "utf8");
  }
}

async function validateExistingOutputRoot(outputRoot) {
  const outputStat = await stat(outputRoot);
  if (!outputStat.isDirectory()) {
    throw new Error(`Existing output path must be a directory: ${outputRoot}`);
  }
}

async function rewriteFiles(outputRoot, mappings) {
  const entries = await walk(outputRoot);
  for (const entry of entries) {
    const entryStat = await stat(entry);
    if (entryStat.isFile()) {
      await rewriteFile(entry, mappings);
    }
  }
}

async function createGeneratedOutput(outputRoot, mappings) {
  await mkdir(path.dirname(outputRoot), { recursive: true });
  await cp(manifest.source, outputRoot, {
    dereference: false,
    errorOnExist: true,
    filter: (src) => !shouldExclude(src),
    force: false,
    recursive: true,
  });

  await rewriteFiles(outputRoot, mappings);
  await rewritePaths(outputRoot, mappings);
  await removeTemplateOnlyGitignoreEntries(outputRoot);
}

async function copyGeneratedIntoExisting(stagedRoot, outputRoot) {
  const entries = await readdir(stagedRoot);

  for (const entry of entries) {
    const sourcePath = path.join(stagedRoot, entry);
    const destinationPath = path.join(outputRoot, entry);

    await cp(sourcePath, destinationPath, {
      dereference: false,
      errorOnExist: true,
      force: false,
      recursive: true,
    });
  }
}

async function createGeneratedOutputInExisting(outputRoot, mappings, names) {
  await validateExistingOutputRoot(outputRoot);

  const tempDir = await mkdtemp(path.join(os.tmpdir(), "template-bootstrap-"));
  const stagedRoot = path.join(tempDir, names.slug);

  try {
    await createGeneratedOutput(stagedRoot, mappings);
    await copyGeneratedIntoExisting(stagedRoot, outputRoot);
  } finally {
    await rm(tempDir, { force: true, recursive: true });
  }
}

async function bootstrap() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.productName.trim()) {
    throw new Error("--product-name is required.");
  }

  if (!args.output.trim()) {
    throw new Error("--output is required.");
  }

  const outputRoot = path.resolve(args.output);
  const outputExists = existsSync(outputRoot);

  const names = deriveNames(args.productName);
  const mappings = getMappings(names);

  if (args.dryRun) {
    const action = outputExists ? "bootstrap into" : "create";
    console.log(`Would ${action} ${names.display} at ${outputRoot}`);
    console.log(`Source: ${manifest.source}`);
    console.log(`Namespace: ${names.pascal}`);
    console.log(`Slug: ${names.slug}`);
    console.log(`Database slug: ${names.snake}`);
    console.log(`NPM scope: ${names.npmScope}`);
    if (outputExists) {
      console.log("Stop on generated path conflicts");
    }
    return;
  }

  if (outputExists) {
    await createGeneratedOutputInExisting(outputRoot, mappings, names);
  } else {
    await createGeneratedOutput(outputRoot, mappings);
  }

  console.log(
    `${outputExists ? "Bootstrapped" : "Created"} ${names.display} at ${outputRoot}`,
  );
  console.log(`Namespace: ${names.pascal}`);
  console.log(`Slug: ${names.slug}`);
  console.log(`Database slug: ${names.snake}`);
  console.log(`NPM scope: ${names.npmScope}`);
}

if (
  process.argv[1] &&
  fileURLToPath(import.meta.url) === path.resolve(process.argv[1])
) {
  bootstrap().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}

export {
  applyMappings,
  bootstrap,
  deriveNames,
  getMappings,
  isTextFile,
  manifest,
  removeTemplateOnlyGitignoreEntries,
  rewriteFile,
  rewritePaths,
  shouldExclude,
  validateExistingOutputRoot,
};
