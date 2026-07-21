import { apiFetch } from "@/lib/api-client";

export type TokenResponse = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

export function issueToken(input: {
  email: string;
  password: string;
  tenant: string;
}) {
  return apiFetch<TokenResponse>("/api/v1/identity/token/issue", {
    method: "POST",
    body: JSON.stringify({ email: input.email, password: input.password }),
    // Not "dashboard" — root SuperAdmin is blocked from dashboard logins.
    // "eprocurement" is allowed for root admin Phase 0 smoke and later buyer/vendor.
    headers: { tenant: input.tenant, "X-FSH-App": "eprocurement" },
    skipAuth: true,
  });
}
