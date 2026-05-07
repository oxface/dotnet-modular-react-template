import {
  configureSameOriginApiClient,
  getCurrentUser,
  type GetMeResponse,
} from "@modular-template/api-client";

export type CurrentUserResponse = GetMeResponse & {
  isAuthenticated: true;
};

export type CurrentUserResult =
  | {
      status: "authenticated";
      currentUser: CurrentUserResponse;
    }
  | {
      status: "unauthenticated";
    };

export type FetchCurrentUser = typeof fetch;

export class CurrentUserRequestError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = "CurrentUserRequestError";
  }
}

export async function loadCurrentUser(
  fetchCurrentUser: FetchCurrentUser = fetch,
): Promise<CurrentUserResult> {
  const result = await getCurrentUser({
    ...configureSameOriginApiClient(),
    fetch: fetchCurrentUser,
  });

  if (result.response?.status === 401) {
    return { status: "unauthenticated" };
  }

  if (!result.data) {
    throw new CurrentUserRequestError(
      `Current user request failed with status ${result.response?.status ?? 0}.`,
      result.response?.status ?? 0,
    );
  }

  const currentUser = toCurrentUserResponse(result.data);

  return {
    status: "authenticated",
    currentUser,
  };
}

function toCurrentUserResponse(response: GetMeResponse): CurrentUserResponse {
  if (!response.isAuthenticated) {
    throw new CurrentUserRequestError(
      "Current user response was not authenticated.",
      0,
    );
  }

  return {
    ...response,
    isAuthenticated: true,
  };
}
