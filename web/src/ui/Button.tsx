import type { ReactNode, MouseEventHandler } from 'react'
import { Icon } from '../components/Icon'

export type ButtonVariant = 'primary' | 'outline' | 'ghost' | 'danger'

const VARIANT_CLS: Record<ButtonVariant, string> = {
  primary: 'btn-pri',
  outline: 'btn-out',
  ghost: 'btn-ghost',
  danger: 'btn-pri btn-danger',
}

/**
 * The Button family (D0 census §1j). `red` replaces the ~10 inline
 * style={{color:'var(--red)'}} sites on ghost/outline destructive actions —
 * retired by migration, not by restyle. `busy` disables while a mutation is
 * pending (the existing isPending idiom).
 */
export function Button({ variant = 'outline', size, icon, busy = false, red = false, disabled, onClick, children, ariaLabel, type = 'button' }: {
  variant?: ButtonVariant
  size?: 'sm'
  icon?: string
  busy?: boolean
  /** red-text destructive idiom on ghost/outline buttons */
  red?: boolean
  disabled?: boolean
  onClick?: MouseEventHandler<HTMLButtonElement>
  children?: ReactNode
  ariaLabel?: string
  type?: 'button' | 'submit'
}) {
  return (
    <button
      type={type}
      className={`btn ${VARIANT_CLS[variant]}${size ? ` btn-${size}` : ''}${red ? ' btn-red' : ''}`}
      disabled={disabled || busy}
      aria-label={ariaLabel}
      aria-busy={busy || undefined}
      onClick={onClick}
    >
      {icon && <Icon name={icon} size={size === 'sm' ? 13 : 15} />} {children}
    </button>
  )
}

/** Inline link-style button (.lnk — breadcrumb backs, inline actions). */
export function LinkButton({ onClick, children, ariaLabel }: {
  onClick?: MouseEventHandler<HTMLButtonElement>
  children: ReactNode
  ariaLabel?: string
}) {
  return <button type="button" className="lnk" aria-label={ariaLabel} onClick={onClick}>{children}</button>
}
