import {
  currentUserQueryOptions,
  resolveCurrentUserAccessState,
  startLogin,
  startLogout,
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
    component: () => <AdminShell fetchCurrentUser={fetchCurrentUser} />,
  });

  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/",
    component: AdminHome,
  });

  return createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
  });
}

function AdminHome() {
  return null;
}

function AdminShell({ fetchCurrentUser }: AppProps) {
  const query = useQuery(currentUserQueryOptions(fetchCurrentUser));
  const accessState = resolveCurrentUserAccessState({
    currentUserResult: query.data,
    error: query.error,
    isLoading: query.isPending,
  });

  return (
    <main className="app-shell">
      <section className="surface" aria-labelledby="admin-title">
        <p className="eyebrow">Administration</p>
        <h1 id="admin-title">Modular Template Admin</h1>
        <AccessStatePanel state={accessState} />
      </section>
    </main>
  );
}

function AccessStatePanel({
  state,
}: {
  state: ReturnType<typeof resolveCurrentUserAccessState>;
}) {
  switch (state.kind) {
    case "loading":
      return <p role="status">Loading session...</p>;
    case "error":
      return (
        <div role="alert">
          <p>Current user could not be loaded.</p>
          <p>
            {state.error instanceof Error
              ? state.error.message
              : "Unknown error"}
          </p>
        </div>
      );
    case "unauthenticated":
      return (
        <div className="stack">
          <p>Sign in to continue.</p>
          <button type="button" onClick={() => startLogin()}>
            Sign in
          </button>
        </div>
      );
    case "no-access":
      return (
        <div className="stack">
          <p>
            Signed in as{" "}
            {state.currentUser.user.displayName ??
              state.currentUser.user.email ??
              "current user"}
            .
          </p>
          <p>Application access has not been granted for this identity.</p>
          <button type="button" onClick={() => startLogout()}>
            Sign out
          </button>
        </div>
      );
    case "has-access":
      return (
        <div className="stack">
          <p>
            Signed in as{" "}
            {state.currentUser.user.displayName ??
              state.currentUser.user.email ??
              "current user"}
            .
          </p>
          <p>Admin foundation is ready.</p>
          <button type="button" onClick={() => startLogout()}>
            Sign out
          </button>
        </div>
      );
  }
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
