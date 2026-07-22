/**
 * Backend Communication handlers emit FSH-admin-oriented paths.
 * Rewrite them to e-procurement SPA routes.
 */
export function toClientNotificationLink(link: string | null | undefined): string | null {
  if (!link) return null;
  if (/^https?:\/\//i.test(link)) return link;

  let path = link.startsWith("/") ? link : `/${link}`;

  if (path === "/vendor" || path.startsWith("/vendor/")) {
    return "/dashboard";
  }

  path = path.replace(/^\/suppliers\/onboarding\//, "/onboarding/");
  path = path.replace(/^\/sourcing\/rfqs\/([^/]+)\/award\/?$/, "/awards/$1");
  path = path.replace(/^\/sourcing\/rfqs\//, "/rfqs/");

  return path;
}

export function formatRelativeShort(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  const diff = Date.now() - d.getTime();
  const sec = Math.round(diff / 1000);
  if (sec < 60) return `${sec}s`;
  const min = Math.round(sec / 60);
  if (min < 60) return `${min}m`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `${hr}h`;
  const day = Math.round(hr / 24);
  if (day < 14) return `${day}d`;
  return d.toLocaleDateString("en-MY");
}
