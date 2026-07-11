import { describe, it, expect } from 'vitest'
import grandfather from './page-archetypes.grandfather.json'

/**
 * The archetype adoption rule (D2 Phase 4, same mechanics as D1's raw-element
 * rule): a NEW page component under components/ must compose an archetype
 * (import from ui/archetypes). Heuristic: files matching the page naming
 * conventions. Today's non-composing pages are grandfathered; the list
 * shrinks at their gates and never grows.
 */

const PAGE_NAME = /(Page|Screens|List|Detail|Master|Queue|Hub|Builder)\.tsx$/
const IMPORTS_ARCHETYPE = /from '.*ui\/archetypes\//

const sources = import.meta.glob('../components/**/*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>

const nonComposingPages = (): string[] => {
  const out: string[] = []
  for (const [p, src] of Object.entries(sources)) {
    const name = p.split('/').pop()!
    if (!PAGE_NAME.test(name) || name.endsWith('.test.tsx')) continue
    if (!IMPORTS_ARCHETYPE.test(src)) out.push(p.replace('../components/', 'src/components/'))
  }
  return out.sort()
}

describe('archetype adoption rule', () => {
  it('every NEW page component composes an archetype (grandfather shrinks only)', () => {
    const current = nonComposingPages()
    const allowed = new Set(grandfather as string[])
    const violations = current.filter((f) => !allowed.has(f))
    expect(violations, `new page component(s) not composing a ui/archetypes page: ${violations.join(', ')}`).toEqual([])
  })

  it('migrated pages are pruned from the grandfather list', () => {
    const current = new Set(nonComposingPages())
    const stale = (grandfather as string[]).filter((f) => !current.has(f))
    expect(stale, `pages now composing an archetype — remove from the list: ${stale.join(', ')}`).toEqual([])
  })
})
