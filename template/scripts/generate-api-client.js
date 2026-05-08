#!/usr/bin/env node
import { spawn } from "node:child_process";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");

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

async function main() {
  await run("node", [path.join(repoRoot, "scripts", "generate-openapi.js")]);
  await run("pnpm", [
    "--dir",
    repoRoot,
    "--filter",
    "@modular-template/api-client",
    "generate",
  ]);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
