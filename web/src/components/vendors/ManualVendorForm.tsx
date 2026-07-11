import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createManualVendor, type CreateManualVendor } from '../../api/client'
import { useLookups } from '../../lib/lookups'
import { useSwec } from '../../api/swec'
import { Icon } from '../Icon'
import { Notice, Spinner } from '../ui'
import { SwecPicker } from './SwecPicker'
import type { FieldSpec } from '../../ui/fieldSpec'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { DependentSelectField } from '../../ui/DependentSelectField'
import { Button } from '../../ui/Button'
import { Chip } from '../../ui/badges'

/**
 * Manual New-Vendor entry (VENDOR-ONBOARDING-SPEC §1) — fully editable, straight to the master, no
 * approval. Conformed-dimension fields (country/state/city/currency/payment terms/bank/type) are
 * dropdowns backed by the seeded reference lookups (DATA-MODEL-ANALYTICS §4); the selected CODE is
 * stored and the label is shown. Free-form fields (name, reg. no., address, contact) stay text.
 * D2: renders through the ui/ primitives — the local rival Field/Select died here.
 */

const text = (key: string, label: string, over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key, label, dataType: 'text', ...over })

export function ManualVendorForm({ onSaved, onBack }: { onSaved: (id: string) => void; onBack: () => void }) {
  const qc = useQueryClient()
  const { isPending } = useLookups()
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

  // One action row, rendered at the top AND bottom of the form (kept in one place so they can't drift).
  const actions = (
    <div className="actionbar">
      <div className="spacer" style={{ flex: 1 }} />
      <Button variant="outline" onClick={onBack}>Cancel</Button>
      <Button variant="primary" icon="check" busy={save.isPending} onClick={submit}>Add to master</Button>
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
        <Button variant="outline" size="sm" busy={save.isPending} onClick={() => save.mutate()}>Save anyway</Button>
      </Notice>}

      {actions}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Company</h3></div>
        <div className="cbody">
          <div className="grid g2">
            <TextField spec={text('name', 'Company name', { required: true })} value={f.name} onChange={(v) => set('name', v)} />
            <TextField spec={text('registeredName', 'Registered name', { placeholder: 'If different from company name' })} value={f.registeredName ?? ''} onChange={(v) => set('registeredName', v)} />
          </div>
          <div className="grid g3">
            <TextField spec={text('registrationNo', 'Reg. no. (SSM)', { placeholder: '1234567-A' })} value={f.registrationNo} onChange={(v) => set('registrationNo', v)} />
            <TextField spec={text('taxId', 'Tax ID')} value={f.taxId ?? ''} onChange={(v) => set('taxId', v)} />
            <SelectField
              spec={{ key: 'type', label: 'Registration type', dataType: 'select', options: { kind: 'static', options: [{ code: 'Non-SWEC', label: 'Non-SWEC' }, { code: 'SWEC', label: 'PETRONAS SWEC' }] } }}
              value={f.type ?? 'Non-SWEC'} onChange={(v) => set('type', v)}
            />
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Location</h3></div>
        <div className="cbody">
          <div className="grid g3">
            <SelectField
              spec={{ key: 'country', label: 'Country', dataType: 'select', options: { kind: 'customList', listCode: 'COUNTRY' } }}
              value={f.country ?? 'MY'} onChange={(v) => { set('country', v); set('state', ''); set('city', '') }}
            />
            <DependentSelectField
              spec={{ key: 'state', label: 'State / Region', dataType: 'select', placeholder: 'Select state', options: { kind: 'customList', listCode: 'STATE', parentField: 'country' } }}
              value={f.state ?? ''} onChange={(v) => { set('state', v); set('city', '') }} parentValue={f.country ?? ''}
            />
            <DependentSelectField
              spec={{ key: 'city', label: 'City', dataType: 'select', placeholder: 'Select city', options: { kind: 'customList', listCode: 'CITY', parentField: 'state' } }}
              value={f.city ?? ''} onChange={(v) => set('city', v)} parentValue={f.state ?? ''}
            />
          </div>
          <TextField spec={text('addressLine', 'Address line')} value={f.addressLine ?? ''} onChange={(v) => set('addressLine', v)} />
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Commercial &amp; banking</h3></div>
        <div className="cbody">
          <div className="grid g2">
            <SelectField spec={{ key: 'currency', label: 'Currency', dataType: 'select', options: { kind: 'customList', listCode: 'CURRENCY' } }} value={f.currency ?? 'MYR'} onChange={(v) => set('currency', v)} />
            <SelectField spec={{ key: 'paymentTerms', label: 'Payment terms', dataType: 'select', options: { kind: 'customList', listCode: 'PAYMENT_TERMS' } }} value={f.paymentTerms ?? 'NET30'} onChange={(v) => set('paymentTerms', v)} />
          </div>
          <div className="grid g3">
            <SelectField spec={{ key: 'bank', label: 'Bank', dataType: 'select', placeholder: 'Select bank', options: { kind: 'customList', listCode: 'BANK' } }} value={f.bank ?? ''} onChange={(v) => set('bank', v)} />
            <TextField spec={text('accountNo', 'Account no.')} value={f.accountNo ?? ''} onChange={(v) => set('accountNo', v)} />
            <TextField spec={text('swift', 'SWIFT')} value={f.swift ?? ''} onChange={(v) => set('swift', v)} />
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead"><h3>Contact &amp; categories</h3></div>
        <div className="cbody">
          <div className="grid g2">
            <TextField spec={text('contactName', 'Contact name')} value={f.contactName ?? ''} onChange={(v) => set('contactName', v)} />
            <TextField spec={text('contactEmail', 'Contact email', { dataType: 'email' })} value={f.contactEmail ?? ''} onChange={(v) => set('contactEmail', v)} />
          </div>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>SWEC categories</label>
            <div>
              {(f.categories ?? []).length === 0
                ? <span className="hint">None selected. </span>
                : (f.categories ?? []).map((c) => <Chip key={c} title={swec?.path(c)}>{swec?.label(c) ?? c}</Chip>)}
              <Button variant="outline" size="sm" icon="edit" onClick={() => setPicking(true)}>Select categories</Button>
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
