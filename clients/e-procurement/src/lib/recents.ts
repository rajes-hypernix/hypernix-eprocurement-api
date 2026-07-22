const RECENTS_KEY = "eproc.recents";
const MAX_RECENTS = 8;

export type RecentEntry = {
  path: string;
  label: string;
  code?: string;
  ts: number;
};

export function loadRecents(): RecentEntry[] {
  try {
    const raw = localStorage.getItem(RECENTS_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as RecentEntry[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function pushRecent(entry: Omit<RecentEntry, "ts">): void {
  const next: RecentEntry = { ...entry, ts: Date.now() };
  const list = loadRecents().filter((r) => r.path !== entry.path);
  list.unshift(next);
  localStorage.setItem(RECENTS_KEY, JSON.stringify(list.slice(0, MAX_RECENTS)));
}

export function clearRecents(): void {
  localStorage.removeItem(RECENTS_KEY);
}
