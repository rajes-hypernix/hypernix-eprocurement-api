import type { SearchHitDto, SearchHitType } from "@/api/search";

const GROUP_LABELS: Record<SearchHitType, string> = {
  Vendor: "Vendors",
  Requisition: "Requisitions",
  Rfq: "RFQs",
  PurchaseOrder: "Purchase Orders",
  Invoice: "Invoices",
  Asn: "Deliveries",
  Statement: "Statements",
};

export function searchGroupLabel(type: SearchHitType): string {
  return GROUP_LABELS[type] ?? type;
}

export function searchHitPath(hit: SearchHitDto, isVendor: boolean): string {
  switch (hit.type) {
    case "Vendor":
      return `/vendors/${hit.id}`;
    case "Requisition":
      return `/reqs/${hit.id}`;
    case "Rfq":
      return isVendor ? `/bids/${hit.id}` : `/rfqs/${hit.id}`;
    case "PurchaseOrder":
      return `/pos/${hit.id}`;
    case "Invoice":
      return `/invoices/${hit.id}`;
    case "Asn":
      return `/deliveries/asn/${hit.id}`;
    case "Statement":
      return isVendor ? `/statement/${hit.id}` : `/statements/${hit.id}`;
    default:
      return "/dashboard";
  }
}

export function groupSearchHits(hits: SearchHitDto[]): Array<{ type: SearchHitType; label: string; items: SearchHitDto[] }> {
  const order: SearchHitType[] = [
    "Vendor",
    "Requisition",
    "Rfq",
    "PurchaseOrder",
    "Invoice",
    "Asn",
    "Statement",
  ];
  const buckets = new Map<SearchHitType, SearchHitDto[]>();
  for (const hit of hits) {
    const list = buckets.get(hit.type) ?? [];
    list.push(hit);
    buckets.set(hit.type, list);
  }
  return order
    .filter((type) => buckets.has(type))
    .map((type) => ({
      type,
      label: searchGroupLabel(type),
      items: buckets.get(type)!,
    }));
}
