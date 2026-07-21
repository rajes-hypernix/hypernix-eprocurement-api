/** Reads the magic-link token from the URL (?token=… or ?t=… for compat). */
export function tokenFromUrl(): string {
  try {
    const params = new URLSearchParams(window.location.search);
    return params.get("token") ?? params.get("t") ?? "";
  } catch {
    return "";
  }
}
