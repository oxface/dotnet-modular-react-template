import { Button } from "@modular-template/ui";

import type { AccessState } from "./access-state";
import { startLogin, startLogout } from "./navigation";

export interface BrowserSessionSmokeSurfaceProps {
  appName: string;
  appDescription: string;
  state: AccessState;
  onLogin?: () => void;
  onLogout?: () => void;
}

export function BrowserSessionSmokeSurface({
  appName,
  appDescription,
  state,
  onLogin = () => startLogin(),
  onLogout = () => startLogout(),
}: BrowserSessionSmokeSurfaceProps) {
  return (
    <section className="surface" aria-labelledby="session-smoke-title">
      <p className="eyebrow">{appDescription}</p>
      <h1 id="session-smoke-title">{appName}</h1>
      <div className="session-panel">
        <p className="session-label">Browser session</p>
        <AccessStatePanel state={state} onLogin={onLogin} onLogout={onLogout} />
      </div>
    </section>
  );
}

function AccessStatePanel({
  state,
  onLogin,
  onLogout,
}: {
  state: AccessState;
  onLogin: () => void;
  onLogout: () => void;
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
          <p data-testid="session-state">Unauthenticated</p>
          <Button onClick={onLogin}>Sign in</Button>
        </div>
      );
    case "no-access":
      return (
        <AuthenticatedSessionState
          stateLabel="Authenticated without application access"
          displayName={
            state.currentUser.user.displayName ??
            state.currentUser.user.email ??
            "current user"
          }
          onLogout={onLogout}
        />
      );
    case "has-access":
      return (
        <AuthenticatedSessionState
          stateLabel="Authenticated with application access"
          displayName={
            state.currentUser.user.displayName ??
            state.currentUser.user.email ??
            "current user"
          }
          onLogout={onLogout}
        />
      );
  }
}

function AuthenticatedSessionState({
  stateLabel,
  displayName,
  onLogout,
}: {
  stateLabel: string;
  displayName: string;
  onLogout: () => void;
}) {
  return (
    <div className="stack">
      <p data-testid="session-state">{stateLabel}</p>
      <p>Signed in as {displayName}.</p>
      <Button onClick={onLogout}>Sign out</Button>
    </div>
  );
}
