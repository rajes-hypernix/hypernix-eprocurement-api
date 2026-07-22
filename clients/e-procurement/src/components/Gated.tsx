import type { ReactNode } from "react";
import { useAuth } from "@/auth/use-auth";
import { hasOldAction } from "@/lib/permissions-map";

/**
 * Hide children when the caller lacks the mapped FSH permission for an old
 * AUTHORIZATION-MATRIX `action`, or a raw FSH `permission` string.
 */
export function Gated({
  action,
  permission,
  children,
  fallback = null,
}: {
  action?: string;
  permission?: string;
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { user, permissionsHydrated } = useAuth();
  if (!permissionsHydrated) return null;

  const granted = user?.permissions ?? [];
  let allowed = true;
  if (permission) {
    allowed = granted.includes(permission);
  } else if (action) {
    allowed = hasOldAction(granted, action);
  }

  return allowed ? <>{children}</> : <>{fallback}</>;
}
