import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { Button } from "../src";

afterEach(() => {
  cleanup();
});

describe("Button", () => {
  it("renders as a button by default", () => {
    render(<Button>Save</Button>);

    expect(screen.getByRole("button", { name: "Save" })).toHaveAttribute(
      "type",
      "button",
    );
  });

  it("passes click events through to consumers", () => {
    const onClick = vi.fn();

    render(<Button onClick={onClick}>Sign in</Button>);

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(onClick).toHaveBeenCalledOnce();
  });
});
