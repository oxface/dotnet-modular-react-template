import { describe, expect, it } from "vitest";

import {
  loginPath,
  logoutPath,
  startLogin,
  startLogout,
  type BrowserLocation,
} from "../src";

describe("session navigation helpers", () => {
  it("starts login through the Host login route", () => {
    const assignedUrls: string[] = [];
    const location: BrowserLocation = {
      assign(url) {
        assignedUrls.push(url);
      },
    };

    startLogin(location);

    expect(assignedUrls).toEqual([loginPath]);
  });

  it("starts logout through the Host logout route", () => {
    const assignedUrls: string[] = [];
    const location: BrowserLocation = {
      assign(url) {
        assignedUrls.push(url);
      },
    };

    startLogout(location);

    expect(assignedUrls).toEqual([logoutPath]);
  });
});
