import { client } from "./generated/client.gen";

export { client } from "./generated/client.gen";
export { getCurrentUser } from "./generated/sdk.gen";
export type { GetMeResponse } from "./generated/types.gen";

export function configureSameOriginApiClient() {
  return {
    baseUrl: globalThis.location?.origin ?? "",
    credentials: "same-origin" as RequestCredentials,
    headers: {
      Accept: "application/json",
    },
  };
}

client.setConfig(configureSameOriginApiClient());
