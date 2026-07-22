export function initials(name: string | null | undefined): string {
  return (name ?? "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!)
    .join("")
    .toUpperCase();
}

export function fmt(n: number | null | undefined): string {
  if (n == null || Number.isNaN(n)) return "—";
  return n.toLocaleString("en-MY", { maximumFractionDigits: 2 });
}

export function dateMY(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleDateString("en-MY", { day: "2-digit", month: "short", year: "numeric" });
}

export function dateTimeMY(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("en-MY", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

/** Normalize vendor type for display / filters (API may store NonSwec or Non-SWEC). */
export function formatVendorType(type: string | null | undefined): string {
  if (!type) return "—";
  if (type === "SWEC" || type === "Swec") return "SWEC";
  if (type === "NonSwec" || type === "Non-SWEC") return "Non-SWEC";
  return type;
}

export function isSwecType(type: string | null | undefined): boolean {
  return type === "SWEC" || type === "Swec";
}
