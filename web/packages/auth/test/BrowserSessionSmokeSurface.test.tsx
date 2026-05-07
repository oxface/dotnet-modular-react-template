import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { BrowserSessionSmokeSurface, type AccessState } from "../src";

afterEach(() => {
  cleanup();
});

describe("BrowserSessionSmokeSurface", () => {
  it("starts login through the provided session command", () => {
    const onLogin = vi.fn();

    render(
      <BrowserSessionSmokeSurface
        appName="Modular Template"
        appDescription="Portal"
        state={{ kind: "unauthenticated" }}
        onLogin={onLogin}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(onLogin).toHaveBeenCalledOnce();
  });

  it("starts logout through the provided session command", () => {
    const onLogout = vi.fn();
    const state: AccessState = {
      kind: "has-access",
      currentUser: {
        isAuthenticated: true,
        user: {
          id: "user-1",
          displayName: "Ada",
          email: "ada@example.test",
        },
        applicationAccess: {
          hasAccess: true,
        },
      },
    };

    render(
      <BrowserSessionSmokeSurface
        appName="Modular Template"
        appDescription="Portal"
        state={state}
        onLogout={onLogout}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));

    expect(onLogout).toHaveBeenCalledOnce();
  });
});
