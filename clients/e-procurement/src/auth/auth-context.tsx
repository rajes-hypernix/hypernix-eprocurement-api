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
import { issueToken } from "@/auth/api";
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
  login: (input: { email: string; password: string; tenant: string }) => Promise<void>;
  logout: () => void;
  refreshPermissions: () => Promise<void>;
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
      const next = claimsToUser(decodeJwt(tokenStore.getAccessToken()), tokenStore.getPermissions());
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
      setUser(claimsToUser(decodeJwt(tokenStore.getAccessToken()), tokenStore.getPermissions()));
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const login = useCallback(async (input: { email: string; password: string; tenant: string }) => {
    tokenStore.setTenant(input.tenant);
    tokenStore.setPermissions([]);
    setPermissionsHydrated(false);
    const tokens = await issueToken(input);
    tokenStore.setTokens(tokens.accessToken, tokens.refreshToken);
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
    }),
    [user, isInitializing, permissionsHydrated, login, logout, refreshPermissions],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
