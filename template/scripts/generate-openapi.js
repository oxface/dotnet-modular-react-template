#!/usr/bin/env node
import { spawn } from "node:child_process";
import { mkdir } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");
const hostProject = path.join(
  repoRoot,
  "server",
  "src",
  "ModularTemplate.Host",
  "ModularTemplate.Host.csproj",
);
const openApiDir = path.join(
  repoRoot,
  "web",
  "packages",
  "api-client",
  "openapi",
);

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
      },
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
  await mkdir(openApiDir, { recursive: true });
  await run("dotnet", [
    "build",
    hostProject,
    "-p:OpenApiGenerateDocuments=true",
    `-p:OpenApiDocumentsDirectory=${openApiDir}`,
    "-p:OpenApiGenerateDocumentsOptions=--file-name host",
  ]);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
