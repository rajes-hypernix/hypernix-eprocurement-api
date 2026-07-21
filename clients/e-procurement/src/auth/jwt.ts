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
