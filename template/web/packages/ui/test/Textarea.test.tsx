import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import { Textarea } from "../src";

afterEach(() => {
  cleanup();
});

describe("Textarea", () => {
  it("renders an accessible multiline input", () => {
    render(<Textarea aria-label="Message" placeholder="Describe the work" />);

    const textarea = screen.getByRole("textbox", { name: "Message" });

    expect(textarea).toHaveAttribute("placeholder", "Describe the work");
  });
});
