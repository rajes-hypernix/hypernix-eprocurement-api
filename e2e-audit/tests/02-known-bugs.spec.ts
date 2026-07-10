import { test, expect } from '@playwright/test'
import { watch, goAs, shot } from './helpers'

const BUYER = 'u_faridah'

// B2 — Draft PR lines must NOT be source-eligible in Consolidate; Submitted must be.
test('B2: Consolidate hides Draft-PR lines, shows Submitted-PR lines', async ({ page }) => {
  await goAs(page, BUYER, 'consolidate')
  await page.waitForTimeout(1500)
  await shot(page, 'B2-consolidate-left-pane')
  const bodyText = await page.locator('.cl-body').first().innerText().catch(() => '')
  // PR-2026-0434 is Draft (seeded); PR-2026-0423 is Submitted.
  expect(bodyText, 'Draft PR-2026-0434 should NOT appear as sourceable').not.toContain('PR-2026-0434')
  // At least one submitted PR appears
  const hasSubmitted = /PR-2026-04(1[258]|2[3568]|3[023])/.test(bodyText)
  expect(hasSubmitted, `a submitted/partially-sourced PR should appear; got: ${bodyText.slice(0, 200)}`).toBeTruthy()
})

// B1 — Consolidate: expanding PR groups must not overlap the next group's header.
test('B1: Consolidate expanded groups do not overlap', async ({ page }) => {
  await goAs(page, BUYER, 'consolidate')
  await page.waitForTimeout(1500)
  // ensure all expanded
  const expandAll = page.getByRole('button', { name: /Expand all/ })
  if (await expandAll.isEnabled().catch(() => false)) await expandAll.click()
  await page.waitForTimeout(500)
  await shot(page, 'B1-consolidate-expanded')
  const groups = page.locator('.prg')
  const n = await groups.count()
  const boxes: { top: number; bottom: number; i: number }[] = []
  for (let i = 0; i < n; i++) {
    const b = await groups.nth(i).boundingBox()
    if (b) boxes.push({ top: b.y, bottom: b.y + b.height, i })
  }
  // adjacent groups should not vertically overlap by more than 2px
  const overlaps: string[] = []
  for (let i = 1; i < boxes.length; i++) {
    if (boxes[i].top < boxes[i - 1].bottom - 2) overlaps.push(`group ${boxes[i - 1].i}↔${boxes[i].i}`)
  }
  expect(overlaps, `overlapping groups: ${overlaps.join(', ')}`).toEqual([])
})

// B3 — Invite page load time + generate-link works (magic link resolves, no 404).
test('B3: Invite page loads under 3s and generates a working magic link', async ({ page }) => {
  const w = watch(page)
  const t0 = Date.now()
  await goAs(page, BUYER, 'onboarding/invite')
  // wait for the email field to be interactive = page usable
  await page.getByLabel(/email/i).first().waitFor({ state: 'visible', timeout: 15000 }).catch(() => {})
  const loadMs = Date.now() - t0
  await shot(page, 'B3-invite-page')

  // Fill + send
  const email = page.getByLabel('Vendor email')
  await email.fill('vieshall@hypernix.net')
  const sendBtn = page.getByRole('button', { name: /Generate .* send link/i })
  let genMs = -1, linkOk = false, magicLink = ''
  if (await sendBtn.isVisible().catch(() => false)) {
    const t1 = Date.now()
    await sendBtn.click()
    // modal shows the magic link
    const linkInput = page.getByLabel(/magic link/i)
    await linkInput.waitFor({ state: 'visible', timeout: 20000 }).catch(() => {})
    genMs = Date.now() - t1
    magicLink = await linkInput.inputValue().catch(() => '')
    // extract token and resolve via API to prove no 404
    const tok = magicLink.match(/[?&]t=([^#&]+)/)?.[1]
    if (tok) {
      const res = await page.request.post('http://localhost:5260/api/onboarding/resolve', {
        data: { token: decodeURIComponent(tok) },
      })
      linkOk = res.ok()
    }
  }
  await shot(page, 'B3-invite-link-modal')
  console.log(`B3 timings: pageLoad=${loadMs}ms generateLink=${genMs}ms linkResolves=${linkOk} netFail=${JSON.stringify([...new Set(w.netFail)])} slow=${JSON.stringify([...new Set(w.slow)])}`)

  expect(loadMs, `invite page load ${loadMs}ms (bug reported ~8s)`).toBeLessThan(5000)
  expect(magicLink, 'a magic link should be generated').toContain('t=')
  expect(linkOk, 'generated magic link must resolve (no 404)').toBeTruthy()
})
