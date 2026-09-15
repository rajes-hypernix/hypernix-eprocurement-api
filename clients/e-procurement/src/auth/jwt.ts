export type JwtClaims = {
  sub?: string;
  email?: string;
  name?: string;
  fullname?: string;
  tenant?: string;
  vendorId?: string;
  permissions?: string[] | string;
  exp?: number;
  [key: string]: unknown;
};

const ROLE_CLAIM_KEYS = [
  "role",
  "roles",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role",
];

function pushRoleValues(out: string[], value: unknown) {
  if (typeof value === "string" && value.trim()) out.push(value.trim());
  else if (Array.isArray(value)) {
    for (const item of value) pushRoleValues(out, item);
  }
}

/** Role names from the access token (Identity writes ClaimTypes.Role). */
export function rolesFromClaims(claims: JwtClaims | null): string[] {
  if (!claims) return [];
  const out: string[] = [];
  for (const key of ROLE_CLAIM_KEYS) {
    pushRoleValues(out, claims[key]);
  }
  return [...new Set(out)];
}

export function decodeJwt(token: string | null | undefined): JwtClaims | null {
  if (!token) return null;
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  try {
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = payload + "=".repeat((4 - (payload.length % 4)) % 4);
    const json = atob(padded);
    return JSON.parse(json) as JwtClaims;
  } catch {
    return null;
  }
}

export function isTokenExpired(claims: JwtClaims | null, skewMs = 10_000): boolean {
  if (!claims || typeof claims.exp !== "number") return false;
  return claims.exp * 1000 <= Date.now() + skewMs;
}
