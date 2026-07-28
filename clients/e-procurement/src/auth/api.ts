import { apiFetch, ApiRequestError } from "@/lib/api-client";
import { env } from "@/env";

export type TokenResponse = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

export type ResolveTenantCandidate = {
  tenantId: string;
  tenantName?: string | null;
};

export type ResolveTenantResponse = {
  status: "Found" | "NotFound" | "Ambiguous" | number;
  tenantId?: string | null;
  tenantName?: string | null;
  candidates?: ResolveTenantCandidate[] | null;
};

export class AmbiguousTenantError extends Error {
  readonly candidates: ResolveTenantCandidate[];

  constructor(candidates: ResolveTenantCandidate[]) {
    super("Multiple organizations match this email. Choose one to continue.");
    this.candidates = candidates;
  }
}

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

/** Resolve which tenant owns an email. Uses root header only to prime IdentityDbContext. */
export async function resolveTenantByEmail(email: string): Promise<ResolveTenantResponse> {
  const response = await fetch(`${env.apiBase}/api/v1/identity/resolve-tenant`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      tenant: env.defaultTenant || "root",
    },
    body: JSON.stringify({ email }),
    signal: AbortSignal.timeout(30_000),
  });

  if (response.status === 409) {
    const body = (await response.json()) as ResolveTenantResponse;
    const candidates = (body.candidates ?? []).map((c) => ({
      tenantId: c.tenantId,
      tenantName: c.tenantName,
    }));
    throw new AmbiguousTenantError(candidates);
  }

  if (!response.ok) {
    let detail = response.statusText;
    try {
      const problem = (await response.json()) as { detail?: string; title?: string };
      detail = problem.detail ?? problem.title ?? detail;
    } catch {
      /* ignore */
    }
    throw new ApiRequestError(response.status, detail, { status: response.status, detail });
  }

  return (await response.json()) as ResolveTenantResponse;
}

/**
 * Step 1 of forgot-password. Resolve tenant first, then call with tenant header.
 * Server returns 200 even when the email is unknown (anti-enumeration).
 */
export async function requestPasswordReset(input: {
  email: string;
  tenant: string;
}): Promise<void> {
  await apiFetch<string>("/api/v1/identity/forgot-password", {
    method: "POST",
    skipAuth: true,
    headers: { tenant: input.tenant, "X-FSH-App": "eprocurement" },
    body: JSON.stringify({ email: input.email }),
  });
}

/**
 * Step 2 — land from emailed link query params (token, email, tenant).
 */
export async function resetPassword(input: {
  email: string;
  password: string;
  token: string;
  tenant: string;
}): Promise<void> {
  await apiFetch<string>("/api/v1/identity/reset-password", {
    method: "POST",
    skipAuth: true,
    headers: { tenant: input.tenant, "X-FSH-App": "eprocurement" },
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      token: input.token,
    }),
  });
}
