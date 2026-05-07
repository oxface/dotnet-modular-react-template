import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "./openapi/host.json",
  output: {
    path: "./src/generated",
    clean: true,
  },
});
