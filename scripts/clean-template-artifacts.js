#!/usr/bin/env node
import { lstat, readdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");
const templateRoot = path.join(repoRoot, "template");

const generatedDirectoryNames = new Set([
  ".pnpm-store",
  "bin",
  "coverage",
  "dist",
  "node_modules",
  "obj",
  "playwright-report",
  "test-results",
]);

async function removeGeneratedDirectories(root) {
  const entries = await readdir(root, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = path.join(root, entry.name);
    if (generatedDirectoryNames.has(entry.name)) {
      await rm(fullPath, { force: true, recursive: true });
      continue;
    }

    if (!entry.isDirectory()) {
      continue;
    }

    const entryStat = await lstat(fullPath);
    if (!entryStat.isSymbolicLink()) {
      await removeGeneratedDirectories(fullPath);
    }
  }
}

await removeGeneratedDirectories(templateRoot);
