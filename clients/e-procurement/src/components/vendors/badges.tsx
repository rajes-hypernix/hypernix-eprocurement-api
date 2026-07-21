import { formatVendorType, isSwecType } from "@/lib/format";

const STATUS_TONE: Record<string, string> = {
  Registered: "b-green",
  Provisional: "b-amber",
  Pending: "b-blue",
  Inactive: "b-grey",
  Blacklisted: "b-red",
};

export function StatusBadge({ status }: { status: string | null | undefined }) {
  const tone = STATUS_TONE[status ?? ""] ?? "b-grey";
  return <span className={`badge ${tone}`}>{status}</span>;
}

export function TypeBadge({ type }: { type: string | null | undefined }) {
  const label = formatVendorType(type);
  const tone = isSwecType(type) ? "b-teal" : "b-amber";
  return <span className={`badge ${tone}`}>{label}</span>;
}
