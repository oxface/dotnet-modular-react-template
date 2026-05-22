#!/usr/bin/env node
import { spawn } from "node:child_process";
import { cp, mkdir, mkdtemp, rm } from "node:fs/promises";
import { existsSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");
const clientDir = path.join(repoRoot, "web", "packages", "api-client");

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: repoRoot,
      env: process.env,
      shell: process.platform === "win32",
      stdio: "inherit",
    });

    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(
          new Error(
            `${command} ${args.join(" ")} failed with exit code ${code}`,
          ),
        );
      }
    });
  });
}

async function copyIfExists(from, to) {
  if (existsSync(from)) {
    await cp(from, to, { recursive: true });
  }
}

async function main() {
  const snapshotDir = await mkdtemp(
    path.join(os.tmpdir(), "modular-template-api-client-"),
  );

  try {
    const snapshotOpenApi = path.join(snapshotDir, "openapi");
    const snapshotGenerated = path.join(snapshotDir, "generated");

    await mkdir(snapshotOpenApi, { recursive: true });
    await mkdir(snapshotGenerated, { recursive: true });
    await copyIfExists(path.join(clientDir, "openapi"), snapshotOpenApi);
    await copyIfExists(
      path.join(clientDir, "src", "generated"),
      snapshotGenerated,
    );

    await run("pnpm", ["api-client:generate"]);
    await run("diff", [
      "-qr",
      snapshotOpenApi,
      path.join(clientDir, "openapi"),
    ]);
    await run("diff", [
      "-qr",
      snapshotGenerated,
      path.join(clientDir, "src", "generated"),
    ]);
  } finally {
    await rm(snapshotDir, { force: true, recursive: true });
  }
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
