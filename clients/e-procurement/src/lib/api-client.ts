import { env } from "@/env";
import { isRolePreviewActive } from "@/auth/role-preview-guard";
import { tokenStore } from "@/auth/token-store";

export type ApiError = {
  status: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]> | string[];
  reason?: string;
  [key: string]: unknown;
};

export class ApiRequestError extends Error {
  readonly status: number;
  readonly problem?: ApiError;

  constructor(status: number, message: string, problem?: ApiError) {
    super(message);
    this.status = status;
    this.problem = problem;
  }
}

type RequestInitEx = RequestInit & {
  skipAuth?: boolean;
  timeoutMs?: number;
  /** Allow a mutating call while role preview is on (unused by default). */
  skipPreviewGuard?: boolean;
};

const PREVIEW_BLOCKED_METHODS = new Set(["POST", "PUT", "PATCH", "DELETE"]);

const DEFAULT_TIMEOUT_MS = 30_000;

let refreshPromise: Promise<void> | null = null;

export async function refreshAccessToken() {
  const refreshToken = tokenStore.getRefreshToken();
  const accessToken = tokenStore.getAccessToken();
  if (!refreshToken || !accessToken) {
    throw new ApiRequestError(401, "No refresh token");
  }

  const tenant = tokenStore.getTenant() ?? env.defaultTenant;
  const response = await fetch(`${env.apiBase}/api/v1/identity/token/refresh`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(tenant ? { tenant } : {}),
    },
    body: JSON.stringify({ token: accessToken, refreshToken }),
    signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
  });

  if (!response.ok) {
    tokenStore.clear();
    throw new ApiRequestError(response.status, "Refresh failed");
  }

  const tokens = (await response.json()) as {
    token: string;
    refreshToken: string;
  };
  tokenStore.setTokens(tokens.token, tokens.refreshToken);
}

async function parseError(response: Response): Promise<ApiError | undefined> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined;
  try {
    return (await response.json()) as ApiError;
  } catch {
    return undefined;
  }
}

function flattenProblemErrors(errs: ApiError["errors"] | undefined): string[] {
  if (!errs) return [];
  if (Array.isArray(errs)) return errs.map(String).map((s) => s.trim()).filter(Boolean);
  if (typeof errs === "object") {
    return Object.values(errs)
      .flat()
      .map(String)
      .map((s) => s.trim())
      .filter(Boolean);
  }
  return [];
}

/** Build a user-facing message from ProblemDetails (detail + Identity/validation errors). */
export function messageFromProblem(problem: ApiError | undefined, fallback: string): string {
  const flat = flattenProblemErrors(problem?.errors);
  const detail = problem?.detail?.trim() ?? "";
  const isGenericDetail =
    !detail ||
    /^an unexpected error occurred/i.test(detail) ||
    /^one or more validation errors occurred\.?$/i.test(detail);

  if (flat.length > 0) {
    if (detail && !isGenericDetail) {
      const joined = flat.join(" ");
      return joined.toLowerCase().includes(detail.toLowerCase()) ? joined : `${detail} ${joined}`.trim();
    }
    return flat.join(" ");
  }

  return detail || problem?.title || fallback;
}

function mergeSignal(userSignal: AbortSignal | null | undefined, timeoutMs: number): AbortSignal {
  const timeoutSignal = AbortSignal.timeout(timeoutMs);
  if (!userSignal) return timeoutSignal;
  return AbortSignal.any([userSignal, timeoutSignal]);
}

export async function apiFetch<T = unknown>(
  path: string,
  init: RequestInitEx = {},
): Promise<T> {
  const { skipAuth, skipPreviewGuard, headers, timeoutMs, signal, ...rest } = init;
  const effectiveTimeout = timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const method = (rest.method ?? "GET").toUpperCase();
  if (!skipPreviewGuard && isRolePreviewActive() && PREVIEW_BLOCKED_METHODS.has(method)) {
    throw new ApiRequestError(403, "Role preview is view-only. Stop preview to make changes.", {
      status: 403,
      title: "Role preview",
      detail: "Role preview is view-only. Stop preview to make changes.",
    });
  }

  const mergedHeaders = new Headers(headers);
  if (!mergedHeaders.has("Content-Type") && rest.body && typeof rest.body === "string") {
    mergedHeaders.set("Content-Type", "application/json");
  }

  if (!skipAuth) {
    const accessToken = tokenStore.getAccessToken();
    if (accessToken) {
      mergedHeaders.set("Authorization", `Bearer ${accessToken}`);
    } else {
      tokenStore.clear();
      throw new ApiRequestError(401, "Not signed in", {
        status: 401,
        title: "Unauthorized",
        detail: "Your session is no longer available. Please sign in again.",
      });
    }
  }

  const tenant = tokenStore.getTenant() ?? env.defaultTenant;
  if (tenant && !mergedHeaders.has("tenant")) {
    mergedHeaders.set("tenant", tenant);
  }

  const url = path.startsWith("http") ? path : `${env.apiBase}${path}`;
  let response = await fetch(url, {
    ...rest,
    headers: mergedHeaders,
    signal: mergeSignal(signal, effectiveTimeout),
  });

  if (response.status === 401 && !skipAuth && tokenStore.getRefreshToken()) {
    refreshPromise ??= refreshAccessToken().finally(() => {
      refreshPromise = null;
    });

    try {
      await refreshPromise;
    } catch (e) {
      throw e instanceof ApiRequestError ? e : new ApiRequestError(401, "Session expired");
    }

    const retryHeaders = new Headers(mergedHeaders);
    retryHeaders.set("Authorization", `Bearer ${tokenStore.getAccessToken() ?? ""}`);
    response = await fetch(url, {
      ...rest,
      headers: retryHeaders,
      signal: mergeSignal(signal, effectiveTimeout),
    });
  }

  if (!response.ok) {
    const problem = await parseError(response);
    throw new ApiRequestError(
      response.status,
      messageFromProblem(problem, response.statusText || "Request failed"),
      problem,
    );
  }

  if (response.status === 204) return undefined as T;

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined as T;

  return (await response.json()) as T;
}
