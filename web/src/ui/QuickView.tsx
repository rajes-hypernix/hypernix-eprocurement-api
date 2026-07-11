import { useRef, useState, type ReactNode } from 'react'
import { Spinner } from '../components/ui'
import { Kv } from './display'

export interface QuickViewField { label: string; value: ReactNode }

/**
 * QuickView hover card (D2 navigation shell): on hover-intent (delay +
 * cancel-on-leave) fetch the EXISTING GET detail endpoint and render a
 * compact summary as Kv rows. No new endpoints; the fetcher maps a detail
 * DTO to display fields.
 */
export function QuickView({ fetcher, title, children, delayMs = 350 }: {
  /** resolves the summary fields from the existing detail endpoint */
  fetcher: () => Promise<{ title: string; fields: QuickViewField[] }>
  title?: string
  children: ReactNode
  delayMs?: number
}) {
  const [open, setOpen] = useState(false)
  const [data, setData] = useState<{ title: string; fields: QuickViewField[] } | null>(null)
  const [failed, setFailed] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const alive = useRef(false)

  const enter = () => {
    alive.current = true
    timer.current = setTimeout(() => {
      setOpen(true)
      if (!data && !failed) {
        fetcher().then((d) => { if (alive.current) setData(d) })
          .catch(() => { if (alive.current) setFailed(true) })
      }
    }, delayMs)
  }
  const leave = () => {
    alive.current = false
    if (timer.current) clearTimeout(timer.current)   // hover-intent cancel
    setOpen(false)
  }

  return (
    <span className="qv-anchor" onMouseEnter={enter} onMouseLeave={leave} onFocus={enter} onBlur={leave}>
      {children}
      {open && (
        <span className="qv-card" role="tooltip" aria-label={title ?? data?.title}>
          {failed ? <span className="hint">Couldn’t load the preview.</span>
            : !data ? <Spinner label="Loading…" />
            : (
              <>
                <span className="qv-title">{data.title}</span>
                {data.fields.map((f) => <Kv key={f.label} k={f.label} v={f.value} />)}
              </>
            )}
        </span>
      )}
    </span>
  )
}
