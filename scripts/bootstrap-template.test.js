import assert from "node:assert/strict";
import { Buffer } from "node:buffer";
import { execFile } from "node:child_process";
import {
  mkdir,
  mkdtemp,
  readFile,
  rm,
  stat,
  writeFile,
} from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { test } from "node:test";
import { promisify } from "node:util";

import {
  deriveNames,
  getMappings,
  isTextFile,
  manifest,
  removeTemplateOnlyGitignoreEntries,
  rewriteFile,
  rewritePaths,
  shouldExclude,
} from "./bootstrap-template.js";

const execFileAsync = promisify(execFile);
const repoRoot = path.resolve(manifest.source, "..");
const bootstrapScript = path.join(repoRoot, "scripts", "bootstrap-template.js");

async function withTempDir(callback) {
  const tempDir = await mkdtemp(
    path.join(os.tmpdir(), "bootstrap-template-test-"),
  );

  try {
    return await callback(tempDir);
  } finally {
    await rm(tempDir, { force: true, recursive: true });
  }
}

test("derives product naming forms from a single display name", () => {
  assert.deepEqual(deriveNames("Café Desk 42!"), {
    display: "Cafe Desk 42",
    npmScope: "@cafe-desk-42",
    pascal: "CafeDesk42",
    slug: "cafe-desk-42",
    snake: "cafe_desk_42",
  });

  assert.deepEqual(deriveNames("CommandDeck"), {
    display: "CommandDeck",
    npmScope: "@commanddeck",
    pascal: "CommandDeck",
    slug: "commanddeck",
    snake: "commanddeck",
  });

  assert.throws(
    () => deriveNames("42 Desk"),
    /namespace that starts with a letter/,
  );
});

test("keeps manifest text and ignore rules focused on bootstrap inputs", () => {
  assert.equal(isTextFile(path.join(manifest.source, ".gitignore")), true);
  assert.equal(isTextFile(path.join(manifest.source, "package.json")), true);
  assert.equal(isTextFile(path.join(manifest.source, "logo.png")), false);

  assert.equal(
    shouldExclude(
      path.join(manifest.source, "node_modules", "pkg", "index.js"),
    ),
    true,
  );
  assert.equal(
    shouldExclude(path.join(manifest.source, ".husky", "_", "husky.sh")),
    true,
  );
  assert.equal(
    shouldExclude(path.join(manifest.source, ".husky", "pre-commit")),
    false,
  );
  assert.equal(
    shouldExclude(
      path.join(
        manifest.source,
        "server",
        "src",
        "ModularTemplate.Persistence",
        "Migrations",
        "20260507204301_InitialCreate.cs",
      ),
    ),
    true,
  );
});

test("rewrites placeholders in text files and skips binary files", async () => {
  await withTempDir(async (tempDir) => {
    const names = deriveNames("North Star");
    const mappings = getMappings(names);
    const textPath = path.join(tempDir, "sample.md");
    const binaryPath = path.join(tempDir, "sample.bin");

    await writeFile(
      textPath,
      "ModularTemplate uses @modular-template and modular_template.",
      "utf8",
    );
    await writeFile(binaryPath, new Uint8Array([77, 0, 84]));

    await rewriteFile(textPath, mappings);
    await rewriteFile(binaryPath, mappings);

    assert.equal(
      await readFile(textPath, "utf8"),
      "NorthStar uses @north-star and north_star.",
    );
    assert.deepEqual(await readFile(binaryPath), Buffer.from([77, 0, 84]));
  });
});

test("renames generated paths from deepest entries first", async () => {
  await withTempDir(async (tempDir) => {
    const names = deriveNames("North Star");
    const mappings = getMappings(names);
    const projectDir = path.join(
      tempDir,
      "server",
      "src",
      "ModularTemplate.Host",
    );
    const projectPath = path.join(projectDir, "ModularTemplate.Host.csproj");

    await mkdir(projectDir, { recursive: true });
    await writeFile(projectPath, "<Project />", "utf8");

    await rewritePaths(tempDir, mappings);

    assert.equal(
      await readFile(
        path.join(
          tempDir,
          "server",
          "src",
          "NorthStar.Host",
          "NorthStar.Host.csproj",
        ),
        "utf8",
      ),
      "<Project />",
    );
  });
});

test("removes template-only EF migration ignore block for products", async () => {
  await withTempDir(async (tempDir) => {
    const gitignorePath = path.join(tempDir, ".gitignore");

    await writeFile(
      gitignorePath,
      `node_modules/

# Template-local generated EF migrations.
# Bootstrapped product repositories remove this block so they can commit their
# own migration history.
server/src/NorthStar.Persistence/Migrations/
`,
      "utf8",
    );

    await removeTemplateOnlyGitignoreEntries(tempDir);

    assert.equal(await readFile(gitignorePath, "utf8"), "node_modules/\n\n");
  });
});

test("bootstraps a generated sample with renamed manifests and product CI files", async () => {
  await withTempDir(async (tempDir) => {
    const outputRoot = path.join(tempDir, "north-star");

    await execFileAsync(process.execPath, [
      bootstrapScript,
      "--product-name",
      "North Star",
      "--output",
      outputRoot,
    ]);

    const packageJson = JSON.parse(
      await readFile(path.join(outputRoot, "package.json"), "utf8"),
    );
    const workflow = await readFile(
      path.join(outputRoot, ".github", "workflows", "verify.yml"),
      "utf8",
    );
    const gitignore = await readFile(
      path.join(outputRoot, ".gitignore"),
      "utf8",
    );

    assert.equal(packageJson.name, "north-star");
    assert.match(workflow, /dotnet restore NorthStar\.slnx/);
    assert.doesNotMatch(gitignore, /Persistence\/Migrations\//);
    await assert.rejects(
      stat(
        path.join(
          outputRoot,
          "server",
          "src",
          "NorthStar.Persistence",
          "Migrations",
        ),
      ),
      { code: "ENOENT" },
    );
    assert.equal(
      await readFile(path.join(outputRoot, "NorthStar.slnx"), "utf8").then(
        () => true,
      ),
      true,
    );
  });
});
