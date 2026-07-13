/**
 * CF-FIX1-T1: THE display names for record types — full professional names, no shortforms
 * (operator ruling, Custom_Fields.docx §8). Display-only: enum values and RecordType
 * storage are untouched. Every surface that shows a record type renders through this map
 * so the naming can never drift screen-by-screen.
 */
const LABELS: Record<string, string> = {
  Requisition: 'Requisition',
  Rfq: 'Request For Quote',
  PurchaseOrder: 'Purchase Order',
  Invoice: 'Invoice',
  Asn: 'Advance Shipment Notice',
  Vendor: 'Vendor',
  Onboarding: 'Onboarding',
  Grn: 'Goods Receipt',   // CF-FIX4-T2: first-class record type
}

export const recordTypeLabel = (rt: string): string => LABELS[rt] ?? rt

/** Options for a record-type picker: enum code stored, full name shown. */
export const recordTypeOptions = (types: string[]): { code: string; label: string }[] =>
  types.map((t) => ({ code: t, label: recordTypeLabel(t) }))
