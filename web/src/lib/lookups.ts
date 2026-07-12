import { useQuery } from '@tanstack/react-query'
import { getCustomLists, type CustomList } from '../api/client'

export type Option = { code: string; label: string }

/**
 * Single source of truth for the app's Custom List lookups (country/state/city/currency/payment
 * terms/bank), reused by the manual New-Vendor form AND the vendor self-service form. Backed by the
 * Custom List framework — values are data, editable in Setup, never hardcoded.
 *
 * `of(listCode, parentValueCode?)` returns a dependent list's options; `labelOf(listCode, code)`
 * resolves a stored code to its label for read screens.
 */
export function useLookups() {
  const { data = [], isPending } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists, staleTime: Infinity })

  const list = (code: string): CustomList | undefined => data.find((l) => l.code === code)

  const of = (listCode: string, parentValueCode?: string): Option[] => {
    const l = list(listCode)
    if (!l || l.active === false) return []   // CF1-T2: an inactive LIST offers nothing (labels below still resolve)
    return l.values
      .filter((v) => v.active && (parentValueCode === undefined || v.parentValueCode === parentValueCode))
      // CF1-T2 (NetSuite order option): Alphabetical sorts by label; Entered keeps the Sort integers.
      .sort((a, b) => (l.orderMode === 'Alphabetical' ? a.label.localeCompare(b.label) : a.sort - b.sort))
      .map((v) => ({ code: v.code, label: v.label }))
  }

  const labelOf = (listCode: string, code: string): string =>
    list(listCode)?.values.find((v) => v.code === code)?.label ?? code

  const hasChildren = (listCode: string, parentValueCode: string): boolean =>
    (list(listCode)?.values ?? []).some((v) => v.parentValueCode === parentValueCode)

  return { isPending, of, labelOf, hasChildren, hasCities: (state: string) => hasChildren('CITY', state) }
}
