const ACCESS_KEY = "fsh.eprocurement.accessToken";
const REFRESH_KEY = "fsh.eprocurement.refreshToken";
const TENANT_KEY = "fsh.eprocurement.tenant";
const PERMS_KEY = "fsh.eprocurement.permissions";
/** "1" = persist session in localStorage across browser restarts. Never stores passwords. */
const REMEMBER_KEY = "fsh.eprocurement.rememberMe";
/** Email only — convenience prefill, not a credential. */
const EMAIL_KEY = "fsh.eprocurement.rememberedEmail";

type Listener = () => void;

const listeners = new Set<Listener>();

function emit() {
  for (const listener of listeners) listener();
}

function wantsRemember(): boolean {
  return localStorage.getItem(REMEMBER_KEY) === "1";
}

function sessionStore(): Storage {
  return wantsRemember() ? localStorage : sessionStorage;
}

function readKey(key: string): string | null {
  // Prefer the active persistence mode's store so a stale copy in the other
  // store cannot win (that caused post-login session clears / login loops).
  const primary = sessionStore().getItem(key);
  if (primary) return primary;
  const secondary = (sessionStore() === localStorage ? sessionStorage : localStorage).getItem(key);
  return secondary;
}

function clearSessionKeys(store: Storage) {
  store.removeItem(ACCESS_KEY);
  store.removeItem(REFRESH_KEY);
  store.removeItem(TENANT_KEY);
  store.removeItem(PERMS_KEY);
}

export const tokenStore = {
  getAccessToken: () => readKey(ACCESS_KEY),
  getRefreshToken: () => readKey(REFRESH_KEY),
  getTenant: () => readKey(TENANT_KEY),

  getRememberMe: () => wantsRemember(),

  getRememberedEmail: () => localStorage.getItem(EMAIL_KEY),

  /**
   * Choose where tokens live. Always clears both session buckets first so an
   * in-flight permissions refresh cannot wipe a brand-new login.
   */
  setRememberMe(remember: boolean) {
    localStorage.setItem(REMEMBER_KEY, remember ? "1" : "0");
    clearSessionKeys(sessionStorage);
    clearSessionKeys(localStorage);
  },

  setRememberedEmail(email: string | null) {
    const trimmed = email?.trim() ?? "";
    if (trimmed) localStorage.setItem(EMAIL_KEY, trimmed);
    else localStorage.removeItem(EMAIL_KEY);
  },

  getPermissions(): string[] {
    try {
      const raw = readKey(PERMS_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as unknown;
      return Array.isArray(parsed) ? parsed.filter((p): p is string => typeof p === "string") : [];
    } catch {
      return [];
    }
  },

  setPermissions(permissions: string[]) {
    const store = sessionStore();
    const other = store === localStorage ? sessionStorage : localStorage;
    other.removeItem(PERMS_KEY);
    store.setItem(PERMS_KEY, JSON.stringify(permissions));
    emit();
  },

  setTokens(accessToken: string, refreshToken: string) {
    const store = sessionStore();
    const other = store === localStorage ? sessionStorage : localStorage;
    other.removeItem(ACCESS_KEY);
    other.removeItem(REFRESH_KEY);
    store.setItem(ACCESS_KEY, accessToken);
    store.setItem(REFRESH_KEY, refreshToken);
    emit();
  },

  setTenant(tenant: string) {
    const store = sessionStore();
    const other = store === localStorage ? sessionStorage : localStorage;
    other.removeItem(TENANT_KEY);
    store.setItem(TENANT_KEY, tenant);
    emit();
  },

  clear() {
    clearSessionKeys(localStorage);
    clearSessionKeys(sessionStorage);
    emit();
  },

  subscribe(listener: Listener) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
