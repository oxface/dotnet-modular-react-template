import { describe, expect, it } from "vitest";

import { configureSameOriginApiClient } from "../src";

describe("configureSameOriginApiClient", () => {
  it("keeps browser API calls on the same origin", () => {
    expect(configureSameOriginApiClient()).toEqual({
      baseUrl: globalThis.location.origin,
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
      },
    });
  });
});
