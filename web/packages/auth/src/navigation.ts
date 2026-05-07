export const loginPath = "/auth/login";
export const logoutPath = "/auth/logout";

export interface BrowserLocation {
  assign(url: string): void;
}

export function startLogin(location: BrowserLocation = window.location): void {
  location.assign(loginPath);
}

export function startLogout(location: BrowserLocation = window.location): void {
  location.assign(logoutPath);
}
