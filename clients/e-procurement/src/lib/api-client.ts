import { env } from "@/env";
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

type RequestInitEx = RequestInit & { skipAuth?: boolean; timeoutMs?: number };

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

function mergeSignal(userSignal: AbortSignal | null | undefined, timeoutMs: number): AbortSignal {
  const timeoutSignal = AbortSignal.timeout(timeoutMs);
  if (!userSignal) return timeoutSignal;
  return AbortSignal.any([userSignal, timeoutSignal]);
}

export async function apiFetch<T = unknown>(
  path: string,
  init: RequestInitEx = {},
): Promise<T> {
  const { skipAuth, headers, timeoutMs, signal, ...rest } = init;
  const effectiveTimeout = timeoutMs ?? DEFAULT_TIMEOUT_MS;

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
      problem?.title ?? problem?.detail ?? response.statusText,
      problem,
    );
  }

  if (response.status === 204) return undefined as T;

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined as T;

  return (await response.json()) as T;
}
