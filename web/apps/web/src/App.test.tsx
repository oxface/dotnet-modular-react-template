import { render, screen } from "@testing-library/react";
import { cleanup } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { FetchCurrentUser } from "@modular-template/auth";

import { App } from "./App";

afterEach(() => {
  cleanup();
});

describe("web app shell", () => {
  it("renders unauthenticated portal state", async () => {
    const fetchCurrentUser = vi.fn<FetchCurrentUser>(
      async () => new Response(null, { status: 401 }),
    );

    render(<App fetchCurrentUser={fetchCurrentUser} />);

    expect(await screen.findByText("Sign in to continue.")).toBeInTheDocument();
  });
});
