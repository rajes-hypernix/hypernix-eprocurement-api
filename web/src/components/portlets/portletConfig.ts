// ConfigJson shapes per portlet type — mirrors Application/Dashboards/PortletConfigs.cs
// (the framework's ONE sanctioned JSON; the server validates on save, we parse defensively).

export interface KpiMeterConfig { metricId?: string | null; fn?: string | null; fieldKey?: string | null; target?: number | null; link?: string | null; groupBy?: string | null }
export interface ScorecardItem { metricId: string; link?: string | null }
export interface KpiScorecardConfig { items: ScorecardItem[] }
export interface ReminderItem { savedViewId: string; label: string; route: string }
export interface RemindersConfig { items: ReminderItem[] }
export interface SavedViewListConfig { topN: number; route?: string | null }
export interface ShortcutItem { label: string; route: string; action?: string | null }
export interface ShortcutsConfig { items: ShortcutItem[] }
export interface ChartConfig { seriesIds: string[]; months: number; minMonths: number }

export function parseConfig<T>(json: string, fallback: T): T {
  try { return { ...fallback, ...(JSON.parse(json) as T) } } catch { return fallback }
}

/** Format a metric/aggregate value by its unit — RM money, % signed, days suffixed, plain count. */
export function fmtMetric(value: number, unit: string): string {
  if (unit === 'RM') return `RM ${value.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
  if (unit === '%') return `${value > 0 ? '+' : ''}${value}%`
  if (unit === 'days') return `${value} days`
  return `${value}`
}
