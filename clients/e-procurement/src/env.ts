// Runtime config — loaded once at boot from config.json (same pattern as FSH admin/dashboard).
type RuntimeConfig = {
  apiBase: string;
  defaultTenant: string;
  /** IIS / subfolder path, e.g. "/UI-REAL" — empty when hosted at site root. */
  basePath: string;
};

let cached: RuntimeConfig | null = null;

function normalizeBasePath(raw: string | undefined): string {
  const t = (raw ?? "").trim();
  if (!t || t === "/") return "";
  return t.startsWith("/") ? t.replace(/\/$/, "") : `/${t.replace(/\/$/, "")}`;
}

/** Resolve config.json next to index.html (works under IIS virtual dirs like /UI-REAL). */
function configUrl(): string {
  const base = import.meta.env.BASE_URL || "/";
  if (base.startsWith(".")) {
    const script = document.querySelector<HTMLScriptElement>('script[type="module"][src*="assets/"]');
    if (script?.src) {
      return new URL("../config.json", script.src).href;
    }
    return new URL("config.json", window.location.href).href;
  }
  return new URL("config.json", `${window.location.origin}${base.endsWith("/") ? base : `${base}/`}`).href;
}

export async function loadRuntimeConfig(): Promise<void> {
  if (cached !== null) return;
  const res = await fetch(configUrl(), { cache: "no-store" });
  if (!res.ok) {
    throw new Error(`Failed to load config.json: ${res.status} ${res.statusText}`);
  }
  const cfg = (await res.json()) as Partial<RuntimeConfig>;
  // Prefer config.json basePath; fall back to Vite build base (e.g. /UI-REAL/).
  const fromConfig = normalizeBasePath(cfg.basePath);
  const fromVite = normalizeBasePath(import.meta.env.BASE_URL?.startsWith(".") ? "" : import.meta.env.BASE_URL);
  cached = {
    apiBase: (cfg.apiBase ?? "").replace(/\/$/, ""),
    defaultTenant: cfg.defaultTenant ?? "root",
    basePath: fromConfig || fromVite,
  };
}

function get(): RuntimeConfig {
  if (cached === null) {
    throw new Error(
      "Runtime config not loaded. main.tsx must await loadRuntimeConfig() before mounting React.",
    );
  }
  return cached;
}

export const env = {
  get apiBase(): string {
    return get().apiBase;
  },
  get defaultTenant(): string {
    return get().defaultTenant;
  },
  get basePath(): string {
    return get().basePath;
  },
  /** Static file under the deploy folder (logos, etc.). Always rooted at the app base. */
  asset(path: string): string {
    const clean = path.replace(/^\//, "");
    const appBase = get().basePath;
    if (appBase) {
      return `${appBase}/${clean}`;
    }
    const viteBase = import.meta.env.BASE_URL || "/";
    if (viteBase.startsWith(".")) {
      const script = document.querySelector<HTMLScriptElement>('script[type="module"][src*="assets/"]');
      if (script?.src) {
        return new URL(`../${clean}`, script.src).href;
      }
      return `/${clean}`;
    }
    return `${viteBase.endsWith("/") ? viteBase : `${viteBase}/`}${clean}`;
  },
};
