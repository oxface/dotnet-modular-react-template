import { render, screen } from "@testing-library/react";
import { cleanup } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { FetchCurrentUser } from "@modular-template/auth";

import { App } from "./App";

afterEach(() => {
  cleanup();
});

describe("admin app shell", () => {
  it("renders unauthenticated browser-session smoke state", async () => {
    const fetchCurrentUser = vi.fn<FetchCurrentUser>(
      async () => new Response(null, { status: 401 }),
    );

    render(<App fetchCurrentUser={fetchCurrentUser} />);

    expect(await screen.findByText("Unauthenticated")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign in" })).toBeInTheDocument();
  });

  it("renders authenticated-without-access browser-session smoke state", async () => {
    const fetchCurrentUser = vi.fn<FetchCurrentUser>(
      async () =>
        new Response(
          JSON.stringify({
            isAuthenticated: true,
            user: {
              id: "user-1",
              displayName: "Ada",
              email: "ada@example.test",
            },
            applicationAccess: {
              hasAccess: false,
            },
          }),
          {
            status: 200,
            headers: {
              "Content-Type": "application/json",
            },
          },
        ),
    );

    render(<App fetchCurrentUser={fetchCurrentUser} />);

    expect(
      await screen.findByText("Authenticated without application access"),
    ).toBeInTheDocument();
    expect(screen.getByText("Signed in as Ada.")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
  });

  it("renders authenticated-with-access browser-session smoke state", async () => {
    const fetchCurrentUser = vi.fn<FetchCurrentUser>(
      async () =>
        new Response(
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
        ),
    );

    render(<App fetchCurrentUser={fetchCurrentUser} />);

    expect(
      await screen.findByText("Authenticated with application access"),
    ).toBeInTheDocument();
    expect(screen.getByText("Signed in as Ada.")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
  });
});
