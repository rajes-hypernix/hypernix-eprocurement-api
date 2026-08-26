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
import { setRolePreviewActive } from "@/auth/role-preview-guard";
import { refreshAccessToken } from "@/lib/api-client";
import { getMyPermissions, getRoleWithPermissions } from "@/api/identity";
import { FshPermissions } from "@/lib/fsh-permissions";

export type AuthUser = {
  id: string;
  email?: string;
  name?: string;
  tenant?: string;
  vendorId?: string;
  permissions: string[];
};

export type RolePreview = {
  roleId: string;
  roleName: string;
};

export type AuthContextValue = {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isInitializing: boolean;
  permissionsHydrated: boolean;
  isVendor: boolean;
  /** True when the real user (not the overlay) may open Act as role. */
  canPreviewRoles: boolean;
  rolePreview: RolePreview | null;
  startRolePreview: (roleId: string, roleName: string) => Promise<void>;
  stopRolePreview: () => void;
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
  const [actualUser, setActualUser] = useState<AuthUser | null>(() => {
    const { claims, usable } = readStoredSession();
    return usable ? claimsToUser(claims, tokenStore.getPermissions()) : null;
  });
  const [rolePreviewState, setRolePreviewState] = useState<{
    roleId: string;
    roleName: string;
    permissions: string[];
  } | null>(null);
  const [isInitializing, setIsInitializing] = useState<boolean>(
    () => !readStoredSession().usable && tokenStore.getRefreshToken() !== null,
  );
  const [permissionsHydrated, setPermissionsHydrated] = useState<boolean>(() => {
    if (!tokenStore.getAccessToken()) return true;
    return tokenStore.getPermissions().length > 0;
  });
  const lastHydratedSubject = useRef<string | null>(actualUser?.id ?? null);

  const user = useMemo<AuthUser | null>(() => {
    if (!actualUser) return null;
    if (!rolePreviewState) return actualUser;
    return { ...actualUser, permissions: rolePreviewState.permissions };
  }, [actualUser, rolePreviewState]);

  /** Vendor portal is JWT vendorId — never inferred from a previewed role name. */
  const isVendor = Boolean(actualUser?.vendorId);
  const canPreviewRoles =
    Boolean(actualUser) &&
    !actualUser?.vendorId &&
    (actualUser?.permissions ?? []).includes(FshPermissions.roles.view);
  const rolePreview: RolePreview | null = rolePreviewState
    ? { roleId: rolePreviewState.roleId, roleName: rolePreviewState.roleName }
    : null;

  useEffect(() => {
    setRolePreviewActive(rolePreviewState !== null);
    return () => setRolePreviewActive(false);
  }, [rolePreviewState]);

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
    if (!actualUser) {
      lastHydratedSubject.current = null;
      setPermissionsHydrated(true);
      return;
    }
    if (lastHydratedSubject.current === actualUser.id && permissionsHydrated) {
      return;
    }
    lastHydratedSubject.current = actualUser.id;
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
  }, [actualUser, permissionsHydrated]);

  useEffect(() => {
    return tokenStore.subscribe(() => {
      const claims = decodeJwt(tokenStore.getAccessToken());
      const next =
        claims && !isTokenExpired(claims)
          ? claimsToUser(claims, tokenStore.getPermissions())
          : null;
      setActualUser(next);
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
      setActualUser(
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
    setRolePreviewState(null);
    setRolePreviewActive(false);
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
    setRolePreviewState(null);
    setRolePreviewActive(false);
    tokenStore.clear();
    queryClient.clear();
  }, [queryClient]);

  const startRolePreview = useCallback(async (roleId: string, roleName: string) => {
    const role = await getRoleWithPermissions(roleId);
    setRolePreviewState({
      roleId: role.id,
      roleName: role.name || roleName,
      permissions: role.permissions ?? [],
    });
  }, []);

  const stopRolePreview = useCallback(() => {
    setRolePreviewState(null);
    setRolePreviewActive(false);
  }, []);

  const refreshPermissions = useCallback(async () => {
    try {
      const perms = await getMyPermissions();
      tokenStore.setPermissions(perms);
    } catch {
      /* swallow */
    }
  }, []);

  const applyLocalProfile = useCallback((patch: { name?: string }) => {
    setActualUser((prev) => {
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
      isVendor,
      canPreviewRoles,
      rolePreview,
      startRolePreview,
      stopRolePreview,
      login,
      logout,
      refreshPermissions,
      applyLocalProfile,
    }),
    [
      user,
      isInitializing,
      permissionsHydrated,
      isVendor,
      canPreviewRoles,
      rolePreview,
      startRolePreview,
      stopRolePreview,
      login,
      logout,
      refreshPermissions,
      applyLocalProfile,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
