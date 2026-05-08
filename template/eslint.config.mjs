import js from "@eslint/js";
import path from "node:path";
import { fileURLToPath } from "node:url";
import tseslint from "typescript-eslint";

const configDir = path.dirname(fileURLToPath(import.meta.url));

export default tseslint.config(
  {
    ignores: [
      "node_modules/**",
      "server/**/bin/**",
      "server/**/obj/**",
      "orchestration/**/bin/**",
      "orchestration/**/obj/**",
      "web/**/dist/**",
      "web/packages/api-client/src/generated/**",
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ["scripts/**/*.js"],
    languageOptions: {
      globals: {
        console: "readonly",
      },
    },
  },
  {
    files: ["web/**/*.ts", "web/**/*.tsx"],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: configDir,
      },
    },
    rules: {
      "@typescript-eslint/consistent-type-imports": "error",
      "@typescript-eslint/no-floating-promises": "error",
    },
  },
);
