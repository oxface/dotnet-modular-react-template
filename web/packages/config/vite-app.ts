import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export interface WebAppConfigOptions {
  port: number;
}

export function defineWebAppConfig(options: WebAppConfigOptions) {
  const hostTarget = process.env.VITE_HOST_ORIGIN ?? "http://localhost:5162";
  const port = Number(process.env.PORT ?? options.port);

  return defineConfig({
    plugins: [react(), tailwindcss()],
    server: {
      port,
      proxy: {
        "/api": {
          target: hostTarget,
          changeOrigin: true,
          secure: false,
        },
        "/auth": {
          target: hostTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
  });
}
