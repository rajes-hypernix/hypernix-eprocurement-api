import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createManualVendor, type CreateManualVendor } from '../../api/client'
import { useLookups } from '../../lib/lookups'
import { useSwec } from '../../api/swec'
import { Icon } from '../Icon'
import { Notice, Spinner } from '../ui'
import { SwecPicker } from './SwecPicker'

/**
 * Manual New-Vendor entry (VENDOR-ONBOARDING-SPEC §1) — fully editable, straight to the master, no
 * approval. Conformed-dimension fields (country/state/city/currency/payment terms/bank/type) are
 * dropdowns backed by the seeded reference lookups (DATA-MODEL-ANALYTICS §4); the selected CODE is
 * stored and the label is shown. Free-form fields (name, reg. no., address, contact) stay text.
 */
export function ManualVendorForm({ onSaved, onBack }: { onSaved: (id: string) => void; onBack: () => void }) {
  const qc = useQueryClient()
  const { isPending, of, hasCities } = useLookups()
  const { data: swec } = useSwec()

  const [f, setF] = useState<CreateManualVendor>({
    name: '', registrationNo: '', type: 'Non-SWEC', country: 'MY', state: '', city: '',
    currency: 'MYR', paymentTerms: 'NET30', bank: '', accountNo: '', swift: '',
    contactName: '', contactEmail: '', addressLine: '', taxId: '', categories: [],
  })
  const set = <K extends keyof CreateManualVendor>(k: K, v: CreateManualVendor[K]) => setF((p) => ({ ...p, [k]: v }))
  const [picking, setPicking] = useState(false)
  const [warn, setWarn] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => createManualVendor(f),
    onSuccess: (r) => {
      void qc.invalidateQueries({ queryKey: ['vendors'] })
      if (r.duplicateWarning) { setWarn(r.duplicateWarning); return }
      onSaved(r.vendor.id!)
    },
    onError: (e: Error) => setErr(e.message),
  })

  const submit = () => {
    setErr(null); setWarn(null)
    if (!f.name.trim()) { setErr('Enter the company name.'); return }
    save.mutate()
  }

  if (isPending) return <Spinner label="Loading reference data…" />
  const isMy = f.country === 'MY'
  const showCityList = isMy && !!f.state && hasCities(f.state)

  // One action row, rendered at the top AND bottom of the form (kept in one place so they can't drift).
  const actions = (
    <div className="actionbar">
      <div className="spacer" style={{ flex: 1 }} />
      <button type="button" className="btn btn-out" onClick={onBack}>Cancel</button>
      <button type="button" className="btn btn-pri" disabled={save.isPending} onClick={submit}><Icon name="check" size={15} /> Add to master</button>
    </div>
  )

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>Vendor Master</button> <Icon name="chev" size={12} /> New Vendor <Icon name="chev" size={12} /> Enter manually
      </div>
      <div className="pagehead">
        <div><h1>New Vendor — manual entry</h1><p>Keyed straight into the master. Best for a known supplier you’re setting up quickly.</p></div>
      </div>

      {err && <Notice tone="error" icon="x">{err}</Notice>}
      {warn && <Notice tone="warn" icon="flag">{warn} — you can still proceed by saving again, or go back and check.
        <button type="button" className="btn btn-out btn-sm" style={{ marginLeft: 10 }} disabled={save.isPending} onClick={() => save.mutate()}>Save anyway</button>
      </Notice>}

      {actions}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Company</h3></div>
        <div className="cbody">
          <div className="grid g2">
            <Field label="Company name" required value={f.name} onChange={(v) => set('name', v)} />
            <Field label="Registered name" value={f.registeredName ?? ''} placeholder="If different from company name" onChange={(v) => set('registeredName', v)} />
          </div>
          <div className="grid g3">
            <Field label="Reg. no. (SSM)" value={f.registrationNo} placeholder="1234567-A" onChange={(v) => set('registrationNo', v)} />
            <Field label="Tax ID" value={f.taxId ?? ''} onChange={(v) => set('taxId', v)} />
            <Select label="Registration type" value={f.type} onChange={(v) => set('type', v)} options={[{ code: 'Non-SWEC', label: 'Non-SWEC' }, { code: 'SWEC', label: 'PETRONAS SWEC' }]} />
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Location</h3></div>
        <div className="cbody">
          <div className="grid g3">
            <Select label="Country" value={f.country ?? 'MY'} onChange={(v) => { set('country', v); set('state', ''); set('city', '') }} options={of('COUNTRY')} />
            {isMy
              ? <Select label="State / Region" value={f.state ?? ''} onChange={(v) => { set('state', v); set('city', '') }} options={of('STATE', 'MY')} placeholder="Select state" />
              : <Field label="State / Region" value={f.state ?? ''} onChange={(v) => set('state', v)} />}
            {showCityList
              ? <Select label="City" value={f.city ?? ''} onChange={(v) => set('city', v)} options={of('CITY', f.state)} placeholder="Select city" />
              : <Field label="City" value={f.city ?? ''} onChange={(v) => set('city', v)} />}
          </div>
          <Field label="Address line" value={f.addressLine ?? ''} onChange={(v) => set('addressLine', v)} />
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Commercial &amp; banking</h3></div>
        <div className="cbody">
          <div className="grid g2">
            <Select label="Currency" value={f.currency ?? 'MYR'} onChange={(v) => set('currency', v)} options={of('CURRENCY')} />
            <Select label="Payment terms" value={f.paymentTerms ?? 'NET30'} onChange={(v) => set('paymentTerms', v)} options={of('PAYMENT_TERMS')} />
          </div>
          <div className="grid g3">
            <Select label="Bank" value={f.bank ?? ''} onChange={(v) => set('bank', v)} options={of('BANK')} placeholder="Select bank" />
            <Field label="Account no." value={f.accountNo ?? ''} onChange={(v) => set('accountNo', v)} />
            <Field label="SWIFT" value={f.swift ?? ''} onChange={(v) => set('swift', v)} />
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Contact &amp; categories</h3></div>
        <div className="cbody">
          <div className="grid g2">
            <Field label="Contact name" value={f.contactName ?? ''} onChange={(v) => set('contactName', v)} />
            <Field label="Contact email" value={f.contactEmail ?? ''} onChange={(v) => set('contactEmail', v)} />
          </div>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>SWEC categories</label>
            <div>
              {(f.categories ?? []).length === 0
                ? <span className="hint">None selected. </span>
                : (f.categories ?? []).map((c) => <span key={c} className="swchip" title={swec?.path(c)} style={{ marginRight: 6 }}>{swec?.label(c) ?? c}</span>)}
              <button type="button" className="btn btn-out btn-sm" style={{ marginLeft: 6 }} onClick={() => setPicking(true)}><Icon name="edit" size={13} /> Select categories</button>
            </div>
          </div>
        </div>
      </div>

      {actions}

      {picking && (
        <SwecPicker initial={f.categories ?? []} vendorName={f.name || 'this vendor'}
          onCancel={() => setPicking(false)} onSave={(codes) => { set('categories', codes); setPicking(false) }} />
      )}
    </>
  )
}

function Field({ label, value, onChange, placeholder, required }: { label: string; value: string; onChange: (v: string) => void; placeholder?: string; required?: boolean }) {
  return (
    <div className="field">
      <label>{label} {required && <span className="req">*</span>}</label>
      <input value={value} placeholder={placeholder} aria-label={label} onChange={(e) => onChange(e.target.value)} />
    </div>
  )
}
function Select({ label, value, onChange, options, placeholder }: { label: string; value: string; onChange: (v: string) => void; options: { code: string; label: string }[]; placeholder?: string }) {
  return (
    <div className="field">
      <label>{label}</label>
      <select value={value} aria-label={label} onChange={(e) => onChange(e.target.value)}>
        {placeholder && <option value="">{placeholder}</option>}
        {options.map((o) => <option key={o.code} value={o.code}>{o.label}</option>)}
      </select>
    </div>
  )
}
