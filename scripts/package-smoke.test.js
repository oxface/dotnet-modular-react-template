import assert from "node:assert/strict";
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

const execFileAsync = promisify(execFile);
const repoRoot = path.resolve(import.meta.dirname, "..");

function commandEnv() {
  const pathKey = process.platform === "win32" ? "Path" : "PATH";
  return {
    ...process.env,
    [pathKey]: `${path.dirname(process.execPath)}${path.delimiter}${
      process.env[pathKey] ?? ""
    }`,
  };
}

async function withTempDir(callback) {
  const tempDir = await mkdtemp(path.join(os.tmpdir(), "package-smoke-test-"));

  try {
    return await callback(tempDir);
  } finally {
    await rm(tempDir, { force: true, recursive: true });
  }
}

async function packPublishedPayload(tempDir) {
  const { stdout } = await execFileAsync(
    "pnpm",
    ["pack", "--pack-destination", tempDir],
    {
      cwd: repoRoot,
      env: commandEnv(),
      maxBuffer: 1024 * 1024 * 8,
    },
  );
  const tarballLine = stdout
    .trim()
    .split(/\r?\n/)
    .findLast((line) => line.endsWith(".tgz"));

  assert.ok(tarballLine, `pnpm pack did not report a tarball:\n${stdout}`);

  return path.isAbsolute(tarballLine)
    ? tarballLine
    : path.join(tempDir, tarballLine);
}

async function assertPackExcludesGeneratedArtifacts(tarballPath) {
  const { stdout } = await execFileAsync("tar", ["-tf", tarballPath], {
    cwd: repoRoot,
    env: commandEnv(),
    maxBuffer: 1024 * 1024 * 8,
  });
  const entries = stdout.trim().split(/\r?\n/);
  const forbiddenEntries = entries.filter(
    (entry) =>
      /package\/template\/(?:.*\/)?(?:\.pnpm-store|bin|coverage|dist|node_modules|obj|playwright-report|test-results)\//.test(
        entry,
      ) ||
      entry.startsWith("package/template/.husky/_/") ||
      entry.startsWith(
        "package/template/server/src/ModularTemplate.Persistence/Migrations/",
      ),
  );

  assert.deepEqual(forbiddenEntries, []);
}

test("packed CLI bootstraps a product from the published payload", async () => {
  await withTempDir(async (tempDir) => {
    const generatedArtifactDir = path.join(
      repoRoot,
      "template",
      "web",
      "apps",
      "web",
      "dist",
    );
    await mkdir(generatedArtifactDir, { recursive: true });
    await writeFile(
      path.join(generatedArtifactDir, "package-smoke-artifact.txt"),
      "generated during package smoke test\n",
      "utf8",
    );

    const tarballPath = await packPublishedPayload(tempDir);
    await assertPackExcludesGeneratedArtifacts(tarballPath);
    const outputRoot = path.join(tempDir, "package-desk");

    await execFileAsync(
      "pnpm",
      [
        "dlx",
        tarballPath,
        "--",
        "--product-name",
        "Package Desk",
        "--output",
        outputRoot,
      ],
      {
        cwd: tempDir,
        env: commandEnv(),
        maxBuffer: 1024 * 1024 * 8,
      },
    );

    const packageJson = JSON.parse(
      await readFile(path.join(outputRoot, "package.json"), "utf8"),
    );
    assert.equal(packageJson.name, "package-desk");
    await assert.rejects(stat(path.join(outputRoot, ".npmignore")), {
      code: "ENOENT",
    });
    await stat(path.join(outputRoot, "PackageDesk.slnx"));
    await stat(path.join(outputRoot, ".github", "workflows", "verify.yml"));
    await assert.rejects(
      stat(
        path.join(
          outputRoot,
          "server",
          "src",
          "PackageDesk.Persistence",
          "Migrations",
        ),
      ),
      { code: "ENOENT" },
    );
  });
});
