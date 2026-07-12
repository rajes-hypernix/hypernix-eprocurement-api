import { describe, it, expect } from 'vitest'
import { IMPLEMENTED_BASES } from '../App'
import { BUYER_CENTER_TABS, VENDOR_CENTER_TABS } from '../centerTabs'
import { ICON_NAMES } from '../components/Icon'

/**
 * The placeholder drift guard. App.tsx renders "Coming in a later slice" for any route
 * base missing from IMPLEMENTED_BASES — a list that went stale at D6/D7/D7.5, so four
 * shipped screens (Segments, Entry Forms, Numbering, Saved Views home) rendered with
 * the placeholder appended underneath. This test makes the failure mode impossible to
 * ship again: every key the internal sidebar can navigate to must either be implemented
 * or be a DECLARED placeholder — an undeclared gap fails loudly here, at commit time.
 */

// Nav keys that are placeholders ON PURPOSE (a real decision, not drift). Adding a key
// here is saying "this screen deliberately shows 'coming soon'" — expect to justify it.
const DECLARED_PLACEHOLDERS: string[] = []

describe('nav coverage — no shipped screen renders the placeholder', () => {
  it('every internal sidebar key is implemented or a declared placeholder', () => {
    const navKeys = BUYER_CENTER_TABS.flatMap((tab) => tab.items.map((i) => i.key))
    const gaps = navKeys.filter(
      (k) => !IMPLEMENTED_BASES.includes(k) && !DECLARED_PLACEHOLDERS.includes(k),
    )
    expect(gaps, `nav key(s) without a screen and not declared as placeholders — a new screen's base was not added to IMPLEMENTED_BASES in App.tsx: ${gaps.join(', ')}`).toEqual([])
  })

  it('declared placeholders never overlap the implemented list (stale declarations fail too)', () => {
    const stale = DECLARED_PLACEHOLDERS.filter((k) => IMPLEMENTED_BASES.includes(k))
    expect(stale, `now implemented — remove from DECLARED_PLACEHOLDERS: ${stale.join(', ')}`).toEqual([])
  })

  it('no two Administration items share an icon (CF1-T3 — glyphs were clip×5/edit×3 shared)', () => {
    const admin = BUYER_CENTER_TABS.find((t) => t.key === 'administration')!
    const icons = admin.items.map((i) => i.icon)
    const dupes = icons.filter((ic, i) => icons.indexOf(ic) !== i)
    expect(dupes, `duplicate admin icons: ${dupes.join(', ')}`).toEqual([])
  })

  it('every nav icon names a real glyph (an unknown name renders an empty svg SILENTLY)', () => {
    // The Segments entry shipped with icon 'chart' before the glyph existed — no error,
    // just a blank space in the sidebar. Unknown names now fail here instead.
    const items = [...BUYER_CENTER_TABS, ...VENDOR_CENTER_TABS].flatMap((t) => t.items)
    const bad = items.filter((i) => i.icon && !ICON_NAMES.includes(i.icon)).map((i) => `${i.key}:${i.icon}`)
    expect(bad, `nav item(s) with an unknown icon name: ${bad.join(', ')}`).toEqual([])
  })
})
