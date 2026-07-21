import type { Page } from "@playwright/test";

const ACCESS_KEY = "fsh.eprocurement.accessToken";
const REFRESH_KEY = "fsh.eprocurement.refreshToken";
const TENANT_KEY = "fsh.eprocurement.tenant";
const PERMS_KEY = "fsh.eprocurement.permissions";

export type SeededUser = {
  sub: string;
  email: string;
  firstName: string;
  lastName: string;
  tenant: string;
  vendorId?: string;
  permissions?: string[];
};

export function fakeJwt(payload: Record<string, unknown>): string {
  const b64url = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  return [b64url({ alg: "HS256", typ: "JWT" }), b64url(payload), "sig"].join(".");
}

export function makeAccessToken(user: SeededUser): string {
  return fakeJwt({
    sub: user.sub,
    email: user.email,
    given_name: user.firstName,
    family_name: user.lastName,
    name: `${user.firstName} ${user.lastName}`.trim(),
    tenant: user.tenant,
    vendorId: user.vendorId,
    permissions: user.permissions ?? [],
    exp: Math.floor(Date.now() / 1000) + 3600,
    iat: Math.floor(Date.now() / 1000),
  });
}

export async function seedAuthedSession(page: Page, user: SeededUser) {
  const accessToken = makeAccessToken(user);

  await page.addInitScript(
    ({ access, refresh, tenant, perms, accessKey, refreshKey, tenantKey, permsKey }) => {
      localStorage.setItem(accessKey, access);
      localStorage.setItem(refreshKey, refresh);
      localStorage.setItem(tenantKey, tenant);
      localStorage.setItem(permsKey, JSON.stringify(perms));
    },
    {
      access: accessToken,
      refresh: "fake-refresh-token",
      tenant: user.tenant,
      perms: user.permissions ?? [],
      accessKey: ACCESS_KEY,
      refreshKey: REFRESH_KEY,
      tenantKey: TENANT_KEY,
      permsKey: PERMS_KEY,
    },
  );
}

export const TEST_USER: SeededUser = {
  sub: "u-eproc-1",
  email: "buyer@root.com",
  firstName: "Root",
  lastName: "Buyer",
  tenant: "root",
  permissions: [],
};
