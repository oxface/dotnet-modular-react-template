import { queryOptions } from "@tanstack/react-query";

import type { FetchCurrentUser } from "./current-user";
import { loadCurrentUser } from "./current-user";

export const currentUserQueryKey = ["current-user"] as const;

export function currentUserQueryOptions(fetchCurrentUser?: FetchCurrentUser) {
  return queryOptions({
    queryKey: currentUserQueryKey,
    queryFn: () => loadCurrentUser(fetchCurrentUser),
  });
}
