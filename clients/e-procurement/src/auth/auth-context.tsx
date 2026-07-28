import {
  createContext,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { tokenStore } from "@/auth/token-store";
import { decodeJwt, isTokenExpired, type JwtClaims } from "@/auth/jwt";
import { AmbiguousTenantError, issueToken, resolveTenantByEmail } from "@/auth/api";
import { refreshAccessToken } from "@/lib/api-client";
import { getMyPermissions } from "@/api/identity";

export type AuthUser = {
  id: string;
  email?: string;
  name?: string;
  tenant?: string;
  vendorId?: string;
  permissions: string[];
};

export type AuthContextValue = {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isInitializing: boolean;
  permissionsHydrated: boolean;
  isVendor: boolean;
  login: (input: {
    email: string;
    password: string;
    /** When set (e.g. after an ambiguous resolve picker), skips resolve-tenant. */
    tenant?: string;
    /** Persist refresh session across browser restarts (never stores the password). */
    rememberMe?: boolean;
  }) => Promise<void>;
  logout: () => void;
  refreshPermissions: () => Promise<void>;
  /** Update display fields in-session after profile save (JWT claims stay until next login). */
  applyLocalProfile: (patch: { name?: string }) => void;
};

export const AuthContext = createContext<AuthContextValue | null>(null);

function claimsToUser(claims: JwtClaims | null, permissions: string[]): AuthUser | null {
  if (!claims?.sub) return null;
  const name =
    (typeof claims.fullname === "string" && claims.fullname) ||
    (typeof claims.name === "string" && claims.name) ||
    claims.email;
  const vendorId =
    typeof claims.vendorId === "string"
      ? claims.vendorId
      : typeof claims.vendorid === "string"
        ? claims.vendorid
        : undefined;
  return {
    id: claims.sub,
    email: claims.email,
    name,
    tenant: claims.tenant,
    vendorId,
    permissions,
  };
}

function readStoredSession(): { claims: JwtClaims | null; usable: boolean } {
  const claims = decodeJwt(tokenStore.getAccessToken());
  return { claims, usable: claims !== null && !isTokenExpired(claims) };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<AuthUser | null>(() => {
    const { claims, usable } = readStoredSession();
    return usable ? claimsToUser(claims, tokenStore.getPermissions()) : null;
  });
  const [isInitializing, setIsInitializing] = useState<boolean>(
    () => !readStoredSession().usable && tokenStore.getRefreshToken() !== null,
  );
  const [permissionsHydrated, setPermissionsHydrated] = useState<boolean>(() => {
    if (!tokenStore.getAccessToken()) return true;
    return tokenStore.getPermissions().length > 0;
  });
  const lastHydratedSubject = useRef<string | null>(user?.id ?? null);

  useEffect(() => {
    if (!isInitializing) return;
    let cancelled = false;
    void (async () => {
      try {
        await refreshAccessToken();
      } catch {
        tokenStore.clear();
      } finally {
        if (!cancelled) setIsInitializing(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isInitializing]);

  useEffect(() => {
    if (!user) {
      lastHydratedSubject.current = null;
      setPermissionsHydrated(true);
      return;
    }
    if (lastHydratedSubject.current === user.id && permissionsHydrated) {
      return;
    }
    lastHydratedSubject.current = user.id;
    let cancelled = false;
    void (async () => {
      try {
        const perms = await getMyPermissions();
        if (cancelled) return;
        tokenStore.setPermissions(perms);
        setPermissionsHydrated(true);
      } catch {
        if (!cancelled) setPermissionsHydrated(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user, permissionsHydrated]);

  useEffect(() => {
    return tokenStore.subscribe(() => {
      const claims = decodeJwt(tokenStore.getAccessToken());
      const next =
        claims && !isTokenExpired(claims)
          ? claimsToUser(claims, tokenStore.getPermissions())
          : null;
      setUser(next);
    });
  }, []);

  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (
        e.key !== null &&
        e.key !== "fsh.eprocurement.accessToken" &&
        e.key !== "fsh.eprocurement.refreshToken"
      ) {
        return;
      }
      const claims = decodeJwt(tokenStore.getAccessToken());
      setUser(
        claims && !isTokenExpired(claims)
          ? claimsToUser(claims, tokenStore.getPermissions())
          : null,
      );
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const login = useCallback(async (input: {
    email: string;
    password: string;
    tenant?: string;
    rememberMe?: boolean;
  }) => {
    const remember = Boolean(input.rememberMe);
    // Wipe any prior session first so hydration/refresh cannot race-clear the new login.
    tokenStore.setRememberMe(remember);
    if (remember) {
      tokenStore.setRememberedEmail(input.email);
    } else {
      tokenStore.setRememberedEmail(null);
    }

    let tenant = input.tenant?.trim();
    if (!tenant) {
      const resolved = await resolveTenantByEmail(input.email);
      tenant = resolved.tenantId?.trim() ?? "";
      if (!tenant) {
        throw new Error("No active organization could be resolved for that email.");
      }
    }

    tokenStore.setTenant(tenant);

    try {
      const tokens = await issueToken({
        email: input.email,
        password: input.password,
        tenant,
      });
      if (!tokens.accessToken || !tokens.refreshToken) {
        throw new Error("Login succeeded but tokens were missing from the response.");
      }
      tokenStore.setPermissions([]);
      setPermissionsHydrated(false);
      tokenStore.setTokens(tokens.accessToken, tokens.refreshToken);
    } catch (err) {
      if (err instanceof AmbiguousTenantError) throw err;
      throw err;
    }
  }, []);

  const logout = useCallback(() => {
    tokenStore.clear();
    queryClient.clear();
  }, [queryClient]);

  const refreshPermissions = useCallback(async () => {
    try {
      const perms = await getMyPermissions();
      tokenStore.setPermissions(perms);
    } catch {
      /* swallow */
    }
  }, []);

  const applyLocalProfile = useCallback((patch: { name?: string }) => {
    setUser((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        name: patch.name?.trim() || prev.name,
      };
    });
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isInitializing,
      permissionsHydrated,
      isVendor: Boolean(user?.vendorId),
      login,
      logout,
      refreshPermissions,
      applyLocalProfile,
    }),
    [user, isInitializing, permissionsHydrated, login, logout, refreshPermissions, applyLocalProfile],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
