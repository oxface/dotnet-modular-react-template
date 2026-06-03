#!/usr/bin/env node
import { mkdtemp, readFile, readdir, rm, stat } from "node:fs/promises";
import { spawn } from "node:child_process";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");

const knownPlaceholders = [
  "@modular-template",
  "ModularTemplate",
  "Modular Template",
  "modular-template",
  "modular_template",
  "dotnet-modular-react-template",
];

const ignoredSegments = new Set([
  ".git",
  ".pnpm-store",
  "bin",
  "coverage",
  "dist",
  "node_modules",
  "obj",
  "playwright-report",
  "test-results",
]);

const ignoredFileExtensions = new Set([".lscache"]);

function parseArgs(argv) {
  const args = {
    full: false,
    keep: false,
    productName: "Acme Desk",
  };

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--") {
      continue;
    }

    if (arg === "--full") {
      args.full = true;
      continue;
    }

    if (arg === "--keep") {
      args.keep = true;
      continue;
    }

    if (arg === "--product-name") {
      args.productName = argv[++i] ?? "";
      continue;
    }

    if (arg === "--help" || arg === "-h") {
      console.log(
        'Usage: node scripts/verify-bootstrap.js [--product-name "Acme Desk"] [--full] [--keep]',
      );
      process.exit(0);
    }

    throw new Error(`Unknown argument: ${arg}`);
  }

  return args;
}

function run(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const pathKey = process.platform === "win32" ? "Path" : "PATH";
    const nodeBinDir = path.dirname(process.execPath);
    const child = spawn(command, args, {
      cwd: options.cwd ?? repoRoot,
      env: {
        ...process.env,
        [pathKey]: `${nodeBinDir}${path.delimiter}${process.env[pathKey] ?? ""}`,
        ASPNETCORE_ENVIRONMENT: "Development",
        ...options.env,
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

async function pathExists(candidate) {
  try {
    return await stat(candidate);
  } catch (error) {
    if (error.code === "ENOENT") {
      return null;
    }

    throw error;
  }
}

async function getContainerRuntimeEnv() {
  if (process.env.DOCKER_HOST || process.platform === "win32") {
    return {};
  }

  const candidates = [
    process.env.XDG_RUNTIME_DIR
      ? path.join(process.env.XDG_RUNTIME_DIR, "podman", "podman.sock")
      : "",
    path.join(
      "/run",
      "user",
      String(os.userInfo().uid),
      "podman",
      "podman.sock",
    ),
  ].filter(Boolean);

  for (const candidate of candidates) {
    const candidateStat = await pathExists(candidate);
    if (!candidateStat?.isSocket()) {
      continue;
    }

    console.log(`Using Podman socket for container tests: ${candidate}`);
    return {
      DOCKER_HOST: `unix://${candidate}`,
      TESTCONTAINERS_RYUK_DISABLED:
        process.env.TESTCONTAINERS_RYUK_DISABLED ?? "true",
    };
  }

  return {};
}

async function walk(root) {
  const entries = await readdir(root, { withFileTypes: true });
  const results = [];

  for (const entry of entries) {
    const fullPath = path.join(root, entry.name);
    const relative = path.relative(root, fullPath).split(path.sep);
    if (relative.some((segment) => ignoredSegments.has(segment))) {
      continue;
    }

    if (ignoredFileExtensions.has(path.extname(entry.name))) {
      continue;
    }

    results.push(fullPath);
    if (entry.isDirectory()) {
      results.push(...(await walk(fullPath)));
    }
  }

  return results;
}

async function scanPlaceholders(generatedRoot) {
  const matches = [];
  const entries = await walk(generatedRoot);

  for (const entry of entries) {
    const relative = path
      .relative(generatedRoot, entry)
      .split(path.sep)
      .join("/");
    if (
      knownPlaceholders.some((placeholder) => relative.includes(placeholder))
    ) {
      matches.push(`${relative} (path)`);
    }

    const entryStat = await stat(entry);
    if (!entryStat.isFile()) {
      continue;
    }

    const buffer = await readFile(entry);
    if (buffer.includes(0)) {
      continue;
    }

    const text = buffer.toString("utf8");
    for (const placeholder of knownPlaceholders) {
      if (text.includes(placeholder)) {
        matches.push(`${relative} (${placeholder})`);
      }
    }
  }

  return matches;
}

async function assertProductMigrationsAreTrackable(generatedRoot) {
  const gitignorePath = path.join(generatedRoot, ".gitignore");
  const gitignore = await readFile(gitignorePath, "utf8");

  if (
    /server\/src\/modules\/[^/\r\n]+\.Infrastructure\/Migrations\//.test(
      gitignore,
    )
  ) {
    throw new Error(
      "Bootstrapped product .gitignore must not ignore EF migrations.",
    );
  }
}

async function assertProductIncludesBaselineMigration(generatedRoot) {
  const modulesRoot = path.join(generatedRoot, "server", "src", "modules");
  const requiredMigrationSets = [
    ["Identity.Infrastructure", "IdentityDbContextModelSnapshot.cs"],
    ["Products.Infrastructure", "ProductsDbContextModelSnapshot.cs"],
  ];

  for (const [projectSuffix, snapshotName] of requiredMigrationSets) {
    const entries = await readdir(modulesRoot, { withFileTypes: true });
    const infrastructureProject = entries.find(
      (entry) => entry.isDirectory() && entry.name.endsWith(projectSuffix),
    );

    if (!infrastructureProject) {
      throw new Error(
        `Generated product is missing its ${projectSuffix} project.`,
      );
    }

    const migrationsPath = path.join(
      modulesRoot,
      infrastructureProject.name,
      "Migrations",
    );
    const migrationEntries = await readdir(migrationsPath);
    const hasInitialMigration = migrationEntries.some((entry) =>
      /^\d{14}_InitialCreate\.cs$/.test(entry),
    );
    const hasInitialDesigner = migrationEntries.some((entry) =>
      /^\d{14}_InitialCreate\.Designer\.cs$/.test(entry),
    );

    if (
      !hasInitialMigration ||
      !hasInitialDesigner ||
      !migrationEntries.includes(snapshotName)
    ) {
      throw new Error(
        `Bootstrapped product must include the baseline ${projectSuffix} InitialCreate EF migration.`,
      );
    }
  }
}

async function runFullValidation(generatedRoot) {
  const containerRuntimeEnv = await getContainerRuntimeEnv();
  const commands = [
    ["pnpm", ["install", "--frozen-lockfile"]],
    ["pnpm", ["format:check"]],
    ["dotnet", ["test", "AcmeDesk.slnx"]],
    ["pnpm", ["frontend:typecheck"]],
    ["pnpm", ["frontend:test"]],
    ["pnpm", ["frontend:build"]],
    ["pnpm", ["frontend:lint"]],
    ["pnpm", ["api-client:check"]],
  ];

  for (const [command, args] of commands) {
    await run(command, args, { cwd: generatedRoot, env: containerRuntimeEnv });
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const tempRoot = await mkdtemp(
    path.join(os.tmpdir(), "modular-template-bootstrap-"),
  );
  const outputRoot = path.join(tempRoot, "acme-desk");

  try {
    await run(process.execPath, [
      path.join(repoRoot, "scripts", "bootstrap-template.js"),
      "--product-name",
      args.productName,
      "--output",
      outputRoot,
    ]);

    const matches = await scanPlaceholders(outputRoot);
    if (matches.length > 0) {
      throw new Error(
        `Known template placeholders remain:\n${matches.join("\n")}`,
      );
    }

    await assertProductMigrationsAreTrackable(outputRoot);
    await assertProductIncludesBaselineMigration(outputRoot);

    if (args.full) {
      await runFullValidation(outputRoot);
    } else {
      console.log(
        "Skipped full generated-repository validation. Re-run with --full to execute:",
      );
      console.log("- pnpm install --frozen-lockfile");
      console.log("- pnpm format:check");
      console.log("- dotnet test AcmeDesk.slnx");
      console.log("- pnpm frontend:typecheck");
      console.log("- pnpm frontend:test");
      console.log("- pnpm frontend:build");
      console.log("- pnpm frontend:lint");
      console.log("- pnpm api-client:check");
    }

    console.log(`Bootstrap verification passed: ${outputRoot}`);
  } finally {
    if (!args.keep) {
      await rm(tempRoot, { force: true, recursive: true });
    }
  }
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
