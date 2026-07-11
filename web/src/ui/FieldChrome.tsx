import type { ReactNode } from 'react'
import type { ChromeMode, FieldSpec } from './fieldSpec'

/**
 * The ONE field shell every primitive renders through — the feature union of
 * the three rival Field components the D0 census found (label + placeholder +
 * required marker + aria) plus the states the rivals never absorbed (help,
 * error). Two modes:
 *  - 'labelled': the .field block (label, required *, control, help, error) —
 *    today's ManualVendorForm/OnboardingForm/PrForm markup.
 *  - 'bare': the control alone (table cells, inline editors); the visual
 *    label is dropped and the primitive carries aria-label={spec.label},
 *    formalising today's ad-hoc aria-label practice.
 */
export function FieldChrome({ spec, chrome = 'labelled', error, htmlFor, children }: {
  spec: FieldSpec
  chrome?: ChromeMode
  error?: string
  htmlFor: string
  children: ReactNode
}) {
  if (chrome === 'bare') return <>{children}</>
  return (
    <div className={`field${error ? ' f-error' : ''}`}>
      <label htmlFor={htmlFor}>
        {spec.label} {spec.required && <span className="req">*</span>}
        {error && <span className="ferr"> {error}</span>}
      </label>
      {children}
      {spec.help && <div className="hint">{spec.help}</div>}
    </div>
  )
}
