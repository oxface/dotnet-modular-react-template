import { render, screen } from "@testing-library/react";
import { cleanup } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { FetchCurrentUser } from "@modular-template/auth";

import { App } from "./App";

afterEach(() => {
  cleanup();
});

describe("admin app shell", () => {
  it("renders authenticated admin foundation state", async () => {
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
      await screen.findByText("Admin foundation is ready."),
    ).toBeInTheDocument();
  });
});
