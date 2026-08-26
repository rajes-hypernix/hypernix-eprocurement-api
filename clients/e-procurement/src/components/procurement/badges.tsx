const PO_TONE: Record<string, string> = {
  Draft: "b-grey",
  Verified: "b-amber",
  Issued: "b-blue",
  Acknowledged: "b-blue",
  PartiallyReceived: "b-amber",
  Received: "b-blue",
  Matched: "b-green",
  Discrepancy: "b-red",
  Cancelled: "b-red",
  Closed: "b-grey",
};

const ASN_TONE: Record<string, string> = {
  InTransit: "b-blue",
  Received: "b-green",
};

const INVOICE_TONE: Record<string, string> = {
  Submitted: "b-amber",
  Exception: "b-red",
  Approved: "b-green",
};

export function labelPartiallyReceived(): string {
  return "Partially received";
}

function label(status: string): string {
  if (status === "PartiallyReceived") return labelPartiallyReceived();
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function PoStatusBadge({ status }: { status: string | null | undefined }) {
  const s = status ?? "";
  const tone = PO_TONE[s] ?? "b-grey";
  return <span className={`badge ${tone}`}>{label(s)}</span>;
}

export function AsnStatusBadge({ status }: { status: string | null | undefined }) {
  const s = status ?? "";
  const tone = ASN_TONE[s] ?? "b-grey";
  const text = s === "InTransit" ? "In transit" : label(s);
  return <span className={`badge ${tone}`}>{text}</span>;
}

export function InvoiceStatusBadge({ status }: { status: string | null | undefined }) {
  const s = status ?? "";
  const tone = INVOICE_TONE[s] ?? "b-grey";
  return <span className={`badge ${tone}`}>{label(s)}</span>;
}
