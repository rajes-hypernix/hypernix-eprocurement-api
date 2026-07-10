// DEMO DATA — hardcoded pending the analytics layer. Do not wire to API.
//
// Single source of the numbers behind the buyer dashboard analytics panel. Every widget
// imports from here so the future API swap is a one-file change (then delete src/mock/).
// Values copied verbatim from docs/dashboard-analytics-demo/eprocure-dashboard-analytics-mockup.html.

// ----- Spend trend (left area chart) -----
// 15 month buckets: indices 0..11 are actual (Jul 2025 – Jun 2026), 12..14 are forecast (Jul – Sep 2026).
export const SPEND_MONTHS = [
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep',
] as const

// Number of leading buckets that are actuals; the rest are forecast.
export const SPEND_ACTUAL_MONTHS = 12

export interface SpendSeries {
  key: string
  /** CSS custom property carrying the series colour (defined in index.css). */
  colorVar: string
  /** RM '000 per month, aligned to SPEND_MONTHS. */
  vals: number[]
}

export const SPEND_SERIES: SpendSeries[] = [
  { key: 'Materials', colorVar: '--da-materials', vals: [512, 655, 838, 782, 560, 548, 575, 610, 668, 625, 590, 690, 735, 790, 845] },
  { key: 'Services', colorVar: '--da-services', vals: [398, 415, 432, 418, 402, 395, 388, 405, 398, 412, 405, 398, 408, 420, 435] },
  { key: 'MRO & consumables', colorVar: '--da-mro', vals: [300, 368, 502, 340, 320, 335, 310, 342, 330, 378, 352, 368, 372, 390, 405] },
]

// Y-axis ceiling in RM '000.
export const SPEND_MAX = 1000

// Editorial headline figures.
export const SPEND_HEADLINE = {
  deltaLabel: 'up 12.8%',
  lead: 'Compared with this time last year, awarded spend is',
  subtitle:
    'In June 2026, committed spend increased by RM 214,380 (+8.1% growth) compared with June 2025. Materials packages drive most of the increase.',
} as const

// ----- New vendors onboarded (right rail, top) -----
export const VENDOR_BAR_MONTHS = [
  'Sep', 'Oct', 'Nov', 'Dec', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep',
] as const

export interface VendorSegment {
  key: string
  colorVar: string
}

export const VENDOR_SEGMENTS: VendorSegment[] = [
  { key: 'Manufacturer', colorVar: '--da-mfr' },
  { key: 'Distributor', colorVar: '--da-dist' },
  { key: 'Service provider', colorVar: '--da-svc' },
]

// Per month: [manufacturer, distributor, service provider], aligned to VENDOR_BAR_MONTHS + VENDOR_SEGMENTS.
export const VENDOR_BARS: number[][] = [
  [4, 3, 2], [5, 3, 3], [6, 4, 5], [4, 4, 2], [5, 3, 3], [4, 4, 3], [5, 4, 3],
  [4, 4, 3], [5, 3, 4], [4, 3, 3], [5, 4, 3], [4, 4, 3], [6, 4, 4],
]

export const VENDOR_BAR_MAX = 16

// ----- Top recent purchase orders (right rail, bottom) -----
export interface RecentPo {
  code: string
  vendor: string
  status: string
  /** App status-pill tone class (reuses the existing `.pill.b-*` styles). */
  tone: string
  /** RM total; formatted for display with the app's money formatter. */
  total: number
}

export const RECENT_POS: RecentPo[] = [
  { code: 'PO-2026-0141', vendor: 'Sentausa Engineering', status: 'Partially received', tone: 'b-amber', total: 296203.85 },
  { code: 'PO-2026-0139', vendor: 'Perkasa Valves Sdn Bhd', status: 'Acknowledged', tone: 'b-blue', total: 205850.21 },
  { code: 'PO-2026-0134', vendor: 'Borneo Instrumentation', status: 'Receipted', tone: 'b-green', total: 102100.75 },
  { code: 'PO-2026-0128', vendor: 'MegaFlow Industrial', status: 'Issued', tone: 'b-teal', total: 95401.54 },
  { code: 'PO-2026-0122', vendor: 'TitanWeld Services', status: 'Invoiced', tone: 'b-grey', total: 67234.44 },
]
