import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { Icon } from '../components/Icon'

/**
 * CFF-T4 — ONE global confirmation layer, wired to every save across the app.
 *  - `banner`: an inline in-page save-banner (green, left-accent), fades after ~3s —
 *    the primary "it saved" signal on transactions/forms. Carries an optional code.
 *  - `toast`: a corner chip (dark, teal check), auto-dismisses after ~2.2s — quick
 *    confirmations (create a field / list value / segment).
 * Copy discipline (enforced by the browser proof): past tense, NO "successfully", NO "!".
 * Matches docs/CF-FINAL/UI-POLISH-REFERENCE.html.
 */

type Kind = 'banner' | 'toast'
type Note = { id: number; message: string; code?: string; kind: Kind }
type NotifyFn = (message: string, opts?: { code?: string; kind?: Kind }) => void

const NotifyContext = createContext<NotifyFn>(() => {})
export const useNotify = () => useContext(NotifyContext)

let seq = 0
const TTL: Record<Kind, number> = { banner: 3000, toast: 2200 }

export function NotifyProvider({ children }: { children: ReactNode }) {
  const [notes, setNotes] = useState<Note[]>([])
  const notify = useCallback<NotifyFn>((message, opts) => {
    const kind = opts?.kind ?? 'banner'
    const id = ++seq
    setNotes((n) => [...n, { id, message, code: opts?.code, kind }])
    // setTimeout is the browser timer here (app code, not a workflow script).
    setTimeout(() => setNotes((n) => n.filter((x) => x.id !== id)), TTL[kind])
  }, [])

  const banners = notes.filter((n) => n.kind === 'banner')
  const toasts = notes.filter((n) => n.kind === 'toast')

  return (
    <NotifyContext.Provider value={notify}>
      {children}
      <div className="notify-banners" aria-live="polite">
        {banners.map((n) => (
          <div key={n.id} className="save-banner" role="status" data-testid="save-banner">
            <Icon name="check" size={18} />
            <span>{n.message}</span>
            {n.code && <span className="save-banner-code">{n.code}</span>}
          </div>
        ))}
      </div>
      <div className="notify-toasts" aria-live="polite">
        {toasts.map((n) => (
          <div key={n.id} className="toast" role="status" data-testid="toast">
            <span className="toast-ic"><Icon name="check" size={16} /></span>
            <span>{n.message}</span>
          </div>
        ))}
      </div>
    </NotifyContext.Provider>
  )
}
