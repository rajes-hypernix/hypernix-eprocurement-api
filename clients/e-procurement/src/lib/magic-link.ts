/** Rewrite API magic links onto the current Vite origin so demo links always hit this app. */
export function portalMagicLink(apiLink: string | null | undefined): string {
  if (!apiLink) return "";
  try {
    const u = new URL(apiLink, window.location.origin);
    const token = u.searchParams.get("token") ?? u.searchParams.get("t");
    if (!token) return apiLink;
    return `${window.location.origin}/onboard?token=${encodeURIComponent(token)}`;
  } catch {
    return apiLink;
  }
}
