/**
 * Detects a newly deployed SPA build (Surfactor version-checker pattern) by comparing
 * the injected app-version meta / VITE_BUILD_TIME and polling index.html ETag /
 * Last-Modified. Production only — skipped in Vite DEV.
 */

export type VersionInfo = {
  version: string;
  buildTime: number;
};

const VERSION_KEY = "eprocure_app_version";
const CHECK_INTERVAL_MS = 60_000;
const VERSION_META_SELECTOR = 'meta[name="app-version"]';

export function clearVersionState(): void {
  sessionStorage.removeItem(VERSION_KEY);
  sessionStorage.removeItem(`${VERSION_KEY}_etag`);
  sessionStorage.removeItem(`${VERSION_KEY}_lastModified`);
  sessionStorage.removeItem(`${VERSION_KEY}_dismissed`);
}

function getCurrentVersion(): VersionInfo {
  const metaTag = document.querySelector(VERSION_META_SELECTOR) as HTMLMetaElement | null;
  if (metaTag?.content) {
    try {
      const versionData = JSON.parse(metaTag.content) as { version?: string; buildTime?: number };
      return {
        version: versionData.version || String(versionData.buildTime ?? 0),
        buildTime: versionData.buildTime || 0,
      };
    } catch {
      return { version: metaTag.content, buildTime: 0 };
    }
  }

  const buildTime = import.meta.env.VITE_BUILD_TIME
    ? Number.parseInt(String(import.meta.env.VITE_BUILD_TIME), 10)
    : 0;

  return { version: String(buildTime), buildTime };
}

function indexHtmlUrl(): string {
  // Works with Vite base "./" (IIS / subfolder) and absolute bases.
  return new URL("index.html", window.location.href).href;
}

async function checkForNewVersion(): Promise<boolean> {
  try {
    const currentVersion = getCurrentVersion();
    const storedVersion = sessionStorage.getItem(VERSION_KEY);

    if (!storedVersion) {
      sessionStorage.setItem(VERSION_KEY, JSON.stringify(currentVersion));
      return false;
    }

    const stored = JSON.parse(storedVersion) as VersionInfo;
    if (currentVersion.buildTime !== 0 && currentVersion.buildTime !== stored.buildTime) {
      return true;
    }

    const response = await fetch(indexHtmlUrl(), {
      method: "HEAD",
      cache: "no-cache",
    });

    const etag = response.headers.get("ETag");
    const lastModified = response.headers.get("Last-Modified");
    const storedEtag = sessionStorage.getItem(`${VERSION_KEY}_etag`);
    const storedLastModified = sessionStorage.getItem(`${VERSION_KEY}_lastModified`);

    if (etag) {
      if (storedEtag !== null && etag !== storedEtag) {
        sessionStorage.setItem(`${VERSION_KEY}_etag`, etag);
        return true;
      }
      if (storedEtag === null) sessionStorage.setItem(`${VERSION_KEY}_etag`, etag);
    }

    if (lastModified) {
      if (storedLastModified !== null && lastModified !== storedLastModified) {
        sessionStorage.setItem(`${VERSION_KEY}_lastModified`, lastModified);
        return true;
      }
      if (storedLastModified === null) {
        sessionStorage.setItem(`${VERSION_KEY}_lastModified`, lastModified);
      }
    }

    return false;
  } catch (error) {
    console.debug("Version check failed:", error);
    return false;
  }
}

function isDismissedForCurrentSignal(): boolean {
  const dismissed = sessionStorage.getItem(`${VERSION_KEY}_dismissed`);
  if (!dismissed) return false;
  const etag = sessionStorage.getItem(`${VERSION_KEY}_etag`) ?? "";
  const lastModified = sessionStorage.getItem(`${VERSION_KEY}_lastModified`) ?? "";
  const build = String(getCurrentVersion().buildTime);
  return dismissed === `${build}|${etag}|${lastModified}`;
}

export function dismissCurrentVersionPrompt(): void {
  const etag = sessionStorage.getItem(`${VERSION_KEY}_etag`) ?? "";
  const lastModified = sessionStorage.getItem(`${VERSION_KEY}_lastModified`) ?? "";
  const build = String(getCurrentVersion().buildTime);
  sessionStorage.setItem(`${VERSION_KEY}_dismissed`, `${build}|${etag}|${lastModified}`);
}

export function refreshToNewVersion(): void {
  clearVersionState();
  window.location.reload();
}

/**
 * @returns cleanup that stops the interval
 */
export function startVersionChecker(onNewVersionDetected?: () => void): () => void {
  if (import.meta.env.DEV) {
    return () => {};
  }

  let stopped = false;
  let prompting = false;

  const notify = () => {
    if (stopped || prompting || isDismissedForCurrentSignal()) return;
    prompting = true;
    if (onNewVersionDetected) {
      onNewVersionDetected();
    } else if (
      window.confirm("A new version of the application is available. Refresh now?")
    ) {
      refreshToNewVersion();
    } else {
      dismissCurrentVersionPrompt();
    }
    prompting = false;
  };

  void checkForNewVersion().then((hasNew) => {
    if (hasNew) notify();
  });

  const intervalId = window.setInterval(() => {
    void checkForNewVersion().then((hasNew) => {
      if (hasNew) notify();
    });
  }, CHECK_INTERVAL_MS);

  return () => {
    stopped = true;
    window.clearInterval(intervalId);
  };
}

export function getAppVersion(): VersionInfo {
  return getCurrentVersion();
}
