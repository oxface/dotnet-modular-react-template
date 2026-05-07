import type { CurrentUserResult } from "./current-user";

export type AuthenticatedAccessState =
  | {
      kind: "has-access";
      currentUser: Extract<
        CurrentUserResult,
        { status: "authenticated" }
      >["currentUser"];
    }
  | {
      kind: "no-access";
      currentUser: Extract<
        CurrentUserResult,
        { status: "authenticated" }
      >["currentUser"];
    };

export type AccessState =
  | {
      kind: "loading";
    }
  | {
      kind: "unauthenticated";
    }
  | {
      kind: "error";
      error: unknown;
    }
  | AuthenticatedAccessState;

export function resolveCurrentUserAccessState(options: {
  currentUserResult?: CurrentUserResult;
  error?: unknown;
  isLoading?: boolean;
}): AccessState {
  if (options.isLoading) {
    return { kind: "loading" };
  }

  if (options.error !== undefined && options.error !== null) {
    return {
      kind: "error",
      error: options.error,
    };
  }

  if (
    options.currentUserResult === undefined ||
    options.currentUserResult.status === "unauthenticated"
  ) {
    return { kind: "unauthenticated" };
  }

  if (options.currentUserResult.currentUser.applicationAccess.hasAccess) {
    return {
      kind: "has-access",
      currentUser: options.currentUserResult.currentUser,
    };
  }

  return {
    kind: "no-access",
    currentUser: options.currentUserResult.currentUser,
  };
}
