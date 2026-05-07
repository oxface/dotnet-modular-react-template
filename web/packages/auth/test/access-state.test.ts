import { describe, expect, it } from "vitest";

import {
  resolveCurrentUserAccessState,
  type CurrentUserResponse,
} from "../src";

const currentUser = {
  isAuthenticated: true,
  user: {
    id: "user-1",
    displayName: "Ada",
    email: "ada@example.test",
  },
  applicationAccess: {
    hasAccess: true,
  },
} satisfies CurrentUserResponse;

describe("resolveCurrentUserAccessState", () => {
  it("returns loading while current-user state is loading", () => {
    expect(resolveCurrentUserAccessState({ isLoading: true })).toEqual({
      kind: "loading",
    });
  });

  it("returns unauthenticated for missing or unauthenticated current-user state", () => {
    expect(resolveCurrentUserAccessState({})).toEqual({
      kind: "unauthenticated",
    });
    expect(
      resolveCurrentUserAccessState({
        currentUserResult: { status: "unauthenticated" },
      }),
    ).toEqual({
      kind: "unauthenticated",
    });
  });

  it("returns no-access for authenticated users without application access", () => {
    const state = resolveCurrentUserAccessState({
      currentUserResult: {
        status: "authenticated",
        currentUser: {
          ...currentUser,
          applicationAccess: {
            hasAccess: false,
          },
        },
      },
    });

    expect(state.kind).toBe("no-access");
  });

  it("returns has-access for authenticated users with application access", () => {
    const state = resolveCurrentUserAccessState({
      currentUserResult: {
        status: "authenticated",
        currentUser,
      },
    });

    expect(state.kind).toBe("has-access");
  });
});
