import { useId, useMemo, useRef, useState } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { useFieldOptions } from './useFieldOptions'

/**
 * CF-FIX1-T7 (A5): THE standardized searchable select — the operator's "proper list
 * style": type-to-filter, full names, Ledger tokens, never a raw browser select.
 * FieldSpec-family (charter single-contract rule): dispatched from renderField when
 * `spec.searchable` is set, options resolve through the SAME useFieldOptions machinery
 * as SelectField/MultiSelectField — so all three operator-named variants ride here:
 *   (1) single select                  — dataType 'select'
 *   (2) multi-select                   — dataType 'multiSelect' (pipe-joined value)
 *   (3) dependent (relationships)      — the render site passes parentValue; child
 *                                        options filter via ParentValueCode as today.
 * Keyboard contract (operator gate): typing filters; ArrowUp/Down move the highlight;
 * Enter selects; Escape closes and restores.
 */
export function SearchSelectField({ spec, value, onChange, chrome, error, parentValue }: FieldProps & { parentValue?: string }) {
  const id = useId()
  const mode = effectiveMode(spec)
  const { flat, groups } = useFieldOptions(spec, parentValue)
  const options = useMemo(() => groups ? groups.flatMap((g) => g.options) : flat, [flat, groups])
  const multi = spec.dataType === 'multiSelect'
  const selected = useMemo(() => (multi && value ? value.split('|') : value ? [value] : []), [multi, value])

  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [highlight, setHighlight] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  if (mode === 'hidden') return null
  const locked = mode === 'disabled' || mode === 'readOnly'

  const shown = options.filter((o) => o.label.toLowerCase().includes(query.trim().toLowerCase()))
  const labelOf = (code: string) => options.find((o) => o.code === code)?.label ?? code
  const display = selected.map(labelOf).join(', ')

  const pick = (code: string) => {
    if (multi) {
      const next = selected.includes(code) ? selected.filter((c) => c !== code) : [...selected, code]
      onChange(next.join('|'))
      setQuery('')
      inputRef.current?.focus()
    } else {
      onChange(code)
      setOpen(false)
      setQuery('')
    }
  }
  const close = () => { setOpen(false); setQuery('') }

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setHighlight((h) => Math.min(h + 1, shown.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setHighlight((h) => Math.max(h - 1, 0)) }
    else if (e.key === 'Enter') { e.preventDefault(); if (shown[highlight]) pick(shown[highlight].code) }
    else if (e.key === 'Escape') { e.preventDefault(); close() }
  }

  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <div className="sselect" data-open={open || undefined}>
        {!open && (
          <button
            type="button" id={id} className="sselect-control" disabled={locked}
            aria-label={spec.label} aria-haspopup="listbox" aria-expanded={open}
            onClick={() => { setOpen(true); setHighlight(0); setTimeout(() => inputRef.current?.focus(), 0) }}
          >
            <span className={display ? '' : 'sselect-placeholder'}>{display || spec.placeholder || 'Select…'}</span>
            <span className="sselect-caret" aria-hidden>▾</span>
          </button>
        )}
        {open && (
          <div className="sselect-pop">
            <input
              ref={inputRef} className="sselect-search" role="combobox"
              aria-label={`Search ${spec.label}`} aria-expanded="true" aria-controls={`${id}-list`}
              placeholder="Type to search…" value={query}
              onChange={(e) => { setQuery(e.target.value); setHighlight(0) }}
              onKeyDown={onKey}
              onBlur={(e) => { if (!e.relatedTarget || !(e.relatedTarget as HTMLElement).closest('.sselect-pop')) close() }}
            />
            <ul className="sselect-list" role="listbox" id={`${id}-list`} aria-label={`${spec.label} options`}>
              {shown.map((o, i) => (
                <li key={o.code}
                  role="option" aria-selected={selected.includes(o.code)}
                  className={`sselect-opt${i === highlight ? ' hi' : ''}${selected.includes(o.code) ? ' on' : ''}`}
                  onMouseEnter={() => setHighlight(i)}
                  onMouseDown={(e) => { e.preventDefault(); pick(o.code) }}
                >
                  {multi && <span className="sselect-tick" aria-hidden>{selected.includes(o.code) ? '☑' : '☐'}</span>}
                  {o.label}
                </li>
              ))}
              {shown.length === 0 && <li className="sselect-empty">No matches.</li>}
            </ul>
          </div>
        )}
      </div>
    </FieldChrome>
  )
}
