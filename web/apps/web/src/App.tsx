import {
  BrowserSessionSmokeSurface,
  currentUserQueryOptions,
  resolveCurrentUserAccessState,
  type FetchCurrentUser,
} from "@modular-template/auth";
import {
  QueryClient,
  QueryClientProvider,
  useQuery,
} from "@tanstack/react-query";
import {
  createRootRoute,
  createRoute,
  createRouter,
  RouterProvider,
} from "@tanstack/react-router";
import { useState } from "react";

interface AppProps {
  fetchCurrentUser?: FetchCurrentUser;
}

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
}

function createAppRouter(fetchCurrentUser?: FetchCurrentUser) {
  const rootRoute = createRootRoute({
    component: () => <WebShell fetchCurrentUser={fetchCurrentUser} />,
  });

  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/",
    component: WebHome,
  });

  return createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
  });
}

function WebHome() {
  return null;
}

function WebShell({ fetchCurrentUser }: AppProps) {
  const query = useQuery(currentUserQueryOptions(fetchCurrentUser));
  const accessState = resolveCurrentUserAccessState({
    currentUserResult: query.data,
    error: query.error,
    isLoading: query.isPending,
  });

  return (
    <main className="app-shell">
      <BrowserSessionSmokeSurface
        appName="Modular Template Web"
        appDescription="Portal"
        state={accessState}
      />
    </main>
  );
}

export function App({ fetchCurrentUser }: AppProps) {
  const [queryClient] = useState(createQueryClient);
  const [router] = useState(() => createAppRouter(fetchCurrentUser));

  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
