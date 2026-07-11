import { useSyncExternalStore } from 'react'

/**
 * Recent records (D2 navigation shell) — genuinely functional, per-browser via
 * localStorage, capped. Server-side persistence rides D4's personalization
 * tables (BACKLOG row). Keyed per principal so switching personas never shows
 * another identity's trail.
 */

export interface RecentRecord {
  type: 'Vendor' | 'Requisition' | 'Rfq' | 'PurchaseOrder' | 'Invoice' | 'Asn' | 'Statement'
  code: string
  hash: string        // '#vendors/<id>' etc. — the route to reopen
}

const CAP = 8
const key = (principal: string) => `eprocure.recents.${principal}`

let listeners: (() => void)[] = []
const emit = () => listeners.forEach((l) => l())

export function getRecents(principal: string): RecentRecord[] {
  try { return JSON.parse(localStorage.getItem(key(principal)) ?? '[]') as RecentRecord[] } catch { return [] }
}

export function recordRecent(principal: string, rec: RecentRecord) {
  if (!rec.code) return
  const next = [rec, ...getRecents(principal).filter((r) => r.hash !== rec.hash)].slice(0, CAP)
  localStorage.setItem(key(principal), JSON.stringify(next))
  emit()
}

export function useRecents(principal: string): RecentRecord[] {
  const raw = useSyncExternalStore(
    (cb) => { listeners.push(cb); return () => { listeners = listeners.filter((l) => l !== cb) } },
    () => localStorage.getItem(key(principal)) ?? '[]',
    () => '[]',
  )
  try { return JSON.parse(raw) as RecentRecord[] } catch { return [] }
}
