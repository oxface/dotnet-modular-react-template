import assert from "node:assert/strict";
import { Buffer } from "node:buffer";
import { execFile } from "node:child_process";
import {
  mkdir,
  mkdtemp,
  readdir,
  readFile,
  rm,
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
      path.join(manifest.source, "server", "src", "Project.csproj.lscache"),
    ),
    true,
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
    false,
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
    const agentIndex = await readFile(
      path.join(outputRoot, "AGENTS.md"),
      "utf8",
    );
    const governance = await readFile(
      path.join(outputRoot, "docs", "governance.md"),
      "utf8",
    );
    const openspecConfig = await readFile(
      path.join(outputRoot, "openspec", "config.yaml"),
      "utf8",
    );

    assert.equal(packageJson.name, "north-star");
    assert.match(workflow, /dotnet restore NorthStar\.slnx/);
    assert.doesNotMatch(gitignore, /Persistence\/Migrations\//);
    assert.match(agentIndex, /openspec\/specs/);
    assert.match(agentIndex, /openspec\/changes/);
    assert.match(governance, /Product authorization MUST be application-owned/);
    assert.match(openspecConfig, /schema: spec-driven/);
    assert.match(openspecConfig, /domain-neutral \.NET \+ React/);
    assert.doesNotMatch(agentIndex, /CommandDeck|MAF|Ollama|issue-intake/);
    assert.doesNotMatch(governance, /CommandDeck|MAF|Ollama|issue-intake/);
    assert.doesNotMatch(openspecConfig, /CommandDeck|MAF|Ollama|issue-intake/);
    assert.equal(
      (
        await readFile(
          path.join(outputRoot, "openspec", "specs", ".gitkeep"),
          "utf8",
        )
      ).trim(),
      "",
    );
    assert.equal(
      (
        await readFile(
          path.join(outputRoot, "openspec", "changes", ".gitkeep"),
          "utf8",
        )
      ).trim(),
      "",
    );
    assert.equal(
      await readFile(
        path.join(
          outputRoot,
          ".agents",
          "skills",
          "openspec-propose",
          "SKILL.md",
        ),
        "utf8",
      ).then((content) => /openspec/i.test(content)),
      true,
    );
    assert.equal(
      await readFile(
        path.join(
          outputRoot,
          "server",
          "src",
          "NorthStar.Persistence",
          "Migrations",
          "NorthStarDbContextModelSnapshot.cs",
        ),
        "utf8",
      ).then((content) => content.includes("NorthStar.Persistence.Migrations")),
      true,
    );
    assert.equal(
      await readdir(
        path.join(
          outputRoot,
          "server",
          "src",
          "NorthStar.Persistence",
          "Migrations",
        ),
      ).then((entries) =>
        entries.some((entry) => /^\d{14}_InitialCreate\.cs$/.test(entry)),
      ),
      true,
    );
    await assert.rejects(readFile(path.join(outputRoot, "README.md")), {
      code: "ENOENT",
    });
    assert.equal(
      await readFile(path.join(outputRoot, "NorthStar.slnx"), "utf8").then(
        () => true,
      ),
      true,
    );
  });
});

test("bootstraps into an existing repository with README and LICENSE", async () => {
  await withTempDir(async (tempDir) => {
    const outputRoot = path.join(tempDir, "north-star");
    await mkdir(outputRoot);
    await writeFile(path.join(outputRoot, "README.md"), "# Existing\n", "utf8");
    await writeFile(path.join(outputRoot, "LICENSE"), "Existing\n", "utf8");

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
    const gitignore = await readFile(
      path.join(outputRoot, ".gitignore"),
      "utf8",
    );

    assert.equal(packageJson.name, "north-star");
    assert.equal(
      await readFile(path.join(outputRoot, "README.md"), "utf8"),
      "# Existing\n",
    );
    assert.equal(
      await readFile(path.join(outputRoot, "LICENSE"), "utf8"),
      "Existing\n",
    );
    assert.match(
      await readFile(
        path.join(outputRoot, ".github", "workflows", "verify.yml"),
        "utf8",
      ),
      /dotnet restore NorthStar\.slnx/,
    );
    assert.match(gitignore, /node_modules/);
    assert.doesNotMatch(gitignore, /Persistence\/Migrations\//);
    assert.equal(
      await readFile(path.join(outputRoot, "NorthStar.slnx"), "utf8").then(
        () => true,
      ),
      true,
    );
  });
});

test("rejects existing repository bootstrap when generated paths would conflict", async () => {
  await withTempDir(async (tempDir) => {
    const outputRoot = path.join(tempDir, "north-star");
    await mkdir(path.join(outputRoot, ".git"), { recursive: true });
    await writeFile(path.join(outputRoot, ".gitignore"), "existing\n", "utf8");

    await assert.rejects(
      execFileAsync(process.execPath, [
        bootstrapScript,
        "--product-name",
        "North Star",
        "--output",
        outputRoot,
      ]),
      (error) =>
        error.stderr.includes(".gitignore") &&
        error.stderr.includes("already exists"),
    );
  });
});
