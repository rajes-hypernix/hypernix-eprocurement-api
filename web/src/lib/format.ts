export function fmt(n: number | null | undefined): string {
  return Number(n ?? 0).toLocaleString('en-MY', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

export function initials(name: string | null | undefined): string {
  return (name ?? '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase()
}

export function dateMY(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return d.toLocaleDateString('en-GB') // dd/mm/yyyy
}

// dd/mm/yyyy from an ISO date/instant, using the date part as-stored (no timezone shift).
// Deadlines are stored as midnight-UTC of the picked day, so reformatting the yyyy-mm-dd
// slice preserves the day exactly — converting to local time could roll it forward a day.
export function fmtDay(iso: string | null | undefined): string {
  if (!iso) return '—'
  const [y, m, d] = iso.slice(0, 10).split('-')
  return y && m && d ? `${d}/${m}/${y}` : (iso ?? '—')
}

// Today's local date as dd/mm/yyyy — the default for "document dated today" form fields
// (ASN shipped date, invoice date). Local components, so it matches the user's calendar day.
export function todayMY(): string {
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getDate())}/${p(d.getMonth() + 1)}/${d.getFullYear()}`
}

const ROLE_LABELS: Record<string, string> = {
  Buyer: 'Buyer',
  Approver: 'Approver (DoA)',
  TechEvaluator: 'Technical Evaluator',
  CommEvaluator: 'Commercial Evaluator',
  Admin: 'Admin',
  Vendor: 'Vendor',
}
export const roleLabel = (r: string) => ROLE_LABELS[r] ?? r
