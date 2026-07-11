import { describe, it, expect } from 'vitest'
import grandfather from './raw-form-elements.grandfather.json'

/**
 * The design-system adoption rule (D1 Phase 4, mirroring the API's
 * ArchitectureTests source-scan pattern): no NEW raw <input>/<select>/
 * <textarea> in web/src/components — new fields go through the ui/ primitives
 * (FieldSpec pipeline). Existing usages are grandfathered per file at the
 * counts recorded when this rule landed; the list SHRINKS as screens migrate
 * (regenerate it in the migration commit) and never grows.
 *
 * Regenerate after migrating a screen: recount raw form elements per file and
 * rewrite src/test/raw-form-elements.grandfather.json — this test fails if a
 * file EXCEEDS its grandfathered count, a new file appears, or a migrated
 * file is still listed.
 */

const RAW = /<(input|select|textarea)\b/g

// Component sources as raw strings via the bundler (no node fs — runs the
// same under vitest and any future browser-mode runner).
const sources = import.meta.glob('../components/**/*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>

const currentCounts = (): Record<string, number> => {
  const out: Record<string, number> = {}
  for (const [p, src] of Object.entries(sources)) {
    if (p.endsWith('.test.tsx')) continue
    const n = (src.match(RAW) ?? []).length
    if (n > 0) out[p.replace('../components/', 'src/components/')] = n
  }
  return out
}

describe('design-system adoption rule', () => {
  it('no raw form element appears outside the grandfather list, and no file grows', () => {
    const current = currentCounts()
    expect(Object.keys(current).length).toBeGreaterThan(0)  // the scan must find the tree

    const allowed = grandfather as Record<string, number>
    const violations: string[] = []
    for (const [file, count] of Object.entries(current)) {
      const cap = allowed[file]
      if (cap === undefined) violations.push(`${file}: ${count} raw form element(s) in a NEW file — use the ui/ primitives (FieldSpec pipeline)`)
      else if (count > cap) violations.push(`${file}: ${count} raw form element(s), grandfathered at ${cap} — new fields must use the ui/ primitives`)
    }
    expect(violations, violations.join('\n')).toEqual([])
  })

  it('the grandfather list only shrinks (stale entries are pruned at migration)', () => {
    const current = currentCounts()
    const stale = Object.keys(grandfather as Record<string, number>).filter((f) => !(f in current))
    expect(stale, `migrated files still grandfathered — remove from the list: ${stale.join(', ')}`).toEqual([])
  })
})
