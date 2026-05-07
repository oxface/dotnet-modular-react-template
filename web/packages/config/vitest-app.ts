import { mergeConfig } from "vite";
import { defineConfig } from "vitest/config";

import { defineWebAppConfig } from "./vite-app.ts";

export function defineWebAppTestConfig(port: number) {
  return mergeConfig(
    defineWebAppConfig({ port }),
    defineConfig({
      test: {
        environment: "jsdom",
        globals: true,
        setupFiles: ["../../packages/config/vitest.setup.ts"],
      },
    }),
  );
}
