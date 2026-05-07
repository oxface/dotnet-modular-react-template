import { describe, expect, it } from "vitest";

import {
  CurrentUserRequestError,
  loadCurrentUser,
  type FetchCurrentUser,
} from "../src";

describe("loadCurrentUser", () => {
  it("loads current user state from the same-origin API route", async () => {
    const calls: Array<Parameters<FetchCurrentUser>> = [];
    const fetchCurrentUser: FetchCurrentUser = async (...args) => {
      calls.push(args);

      return new Response(
        JSON.stringify({
          isAuthenticated: true,
          user: {
            id: "user-1",
            displayName: "Ada",
            email: "ada@example.test",
          },
          applicationAccess: {
            hasAccess: true,
          },
        }),
        {
          status: 200,
          headers: {
            "Content-Type": "application/json",
          },
        },
      );
    };

    const result = await loadCurrentUser(fetchCurrentUser);

    expect(result.status).toBe("authenticated");
    expect(
      result.status === "authenticated"
        ? result.currentUser.user.id
        : undefined,
    ).toBe("user-1");
    expect(calls).toHaveLength(1);

    const request = calls[0]?.[0];
    expect(request).toBeInstanceOf(Request);
    expect(
      request instanceof Request ? new URL(request.url).pathname : undefined,
    ).toBe("/api/me");
    expect(request instanceof Request ? request.credentials : undefined).toBe(
      "same-origin",
    );
    expect(
      request instanceof Request ? request.headers.get("Accept") : undefined,
    ).toBe("application/json");
  });

  it("returns unauthenticated state for 401 responses", async () => {
    const fetchCurrentUser: FetchCurrentUser = async () =>
      new Response(null, { status: 401 });

    await expect(loadCurrentUser(fetchCurrentUser)).resolves.toEqual({
      status: "unauthenticated",
    });
  });

  it("throws for unexpected API failures", async () => {
    const fetchCurrentUser: FetchCurrentUser = async () =>
      new Response(null, { status: 503 });

    await expect(loadCurrentUser(fetchCurrentUser)).rejects.toBeInstanceOf(
      CurrentUserRequestError,
    );
  });
});
