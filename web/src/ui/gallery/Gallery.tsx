import { useState } from 'react'
import type { FieldSpec } from '../fieldSpec'
import { TextField } from '../TextField'
import { TextAreaField } from '../TextAreaField'
import { CodeField } from '../CodeField'
import { SelectField } from '../SelectField'
import { DependentSelectField } from '../DependentSelectField'
import { MultiSelectField } from '../MultiSelectField'
import { YesNoField } from '../YesNoField'
import { SegmentedField } from '../SegmentedField'
import { CheckboxField } from '../CheckboxField'
import { NumberField } from '../NumberField'
import { MoneyField } from '../MoneyField'
import { DateField } from '../DateField'
import { AttachmentField } from '../AttachmentField'
import { TableField } from '../TableField'
import { RepeatingGroupField } from '../RepeatingGroupField'
import { Button, LinkButton } from '../Button'
import { StatusBadge, Pill, Chip } from '../badges'
import type { Tone } from '../tokens'

/**
 * /design/gallery — the living catalogue (dev-only; Storybook is not in the
 * stack). Every primitive in every state, rendered from FieldSpec fixtures.
 * This module is loaded via a DEV-gated dynamic import in App.tsx and is
 * tree-shaken out of production builds.
 */

const TONES: Tone[] = ['grey', 'blue', 'teal', 'amber', 'green', 'red']

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card" style={{ marginBottom: 14 }}>
      <div className="chead"><h3>{title}</h3></div>
      <div className="cbody">{children}</div>
    </div>
  )
}

/** One primitive across the five chrome states, from a single base spec. */
function States({ render }: { render: (spec: FieldSpec, key: string) => React.ReactNode }) {
  const base: FieldSpec = { key: 'g', label: 'Label', dataType: 'text' }
  return (
    <div className="grid g3">
      {render({ ...base, help: 'Plain, with help text' }, 'plain')}
      {render({ ...base, label: 'Required', required: true }, 'required')}
      {render({ ...base, label: 'Disabled', disabled: true }, 'disabled')}
      {render({ ...base, label: 'Read only', readOnly: true }, 'readonly')}
      {render({ ...base, label: 'With error' }, 'error')}
    </div>
  )
}

export default function Gallery() {
  const [vals, setVals] = useState<Record<string, string>>({
    money: '15250.5', date: '2026-07-15', ro: 'Locked value', country: 'MY',
    multi: 'ISO 9001', seg: 'Single', yes: 'Yes',
    table: JSON.stringify([['1200', '1500'], ['300', '420']]),
    group: JSON.stringify([['Jetty upgrade', '2000000']]),
    att: 'demo::specification.pdf',
  })
  const [checked, setChecked] = useState(true)
  const v = (k: string) => vals[k] ?? ''
  const set = (k: string) => (x: string) => setVals((p) => ({ ...p, [k]: x }))

  const staticOpts = (codes: string[]) => ({ kind: 'static' as const, options: codes.map((c) => ({ code: c, label: c })) })

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Design gallery</h1>
          <p>Every field primitive in every state, rendered from FieldSpec fixtures. Dev-only route — tree-shaken out of production builds.</p>
        </div>
      </div>

      <Section title="TextField — the five chrome states">
        <States render={(s, k) => (
          <TextField key={k} spec={s} value={k === 'readonly' ? v('ro') : v(`t-${k}`)} onChange={set(`t-${k}`)} error={k === 'error' ? 'required' : undefined} />
        )} />
        <div className="grid g3">
          <TextField spec={{ key: 'em', label: 'Email', dataType: 'email', placeholder: 'name@company.com' }} value={v('em')} onChange={set('em')} />
          <TextField spec={{ key: 'ph', label: 'With placeholder', dataType: 'text', placeholder: 'e.g. Bintulu Plant' }} value={v('ph')} onChange={set('ph')} />
          <div className="field"><label>Bare chrome (cell mode)</label><TextField spec={{ key: 'bare', label: 'Bare input', dataType: 'text', placeholder: 'aria-label only' }} value={v('bare')} onChange={set('bare')} chrome="bare" /></div>
        </div>
      </Section>

      <Section title="TextAreaField / CodeField">
        <div className="grid g2">
          <TextAreaField spec={{ key: 'ta', label: 'Reason', dataType: 'longText', placeholder: 'e.g. duplicate of another PR', help: 'Recorded in the audit trail' }} value={v('ta')} onChange={set('ta')} />
          <div>
            <CodeField spec={{ key: 'code', label: 'Value code', dataType: 'code', placeholder: 'NET30' }} value={v('code')} onChange={set('code')} />
            <CodeField spec={{ key: 'code-ro', label: 'Persisted code (read only)', dataType: 'code', readOnly: true }} value="RFQ-2026-0001" onChange={() => {}} />
          </div>
        </div>
      </Section>

      <Section title="Selects — static, custom list, dependent, grouped">
        <div className="grid g3">
          <SelectField spec={{ key: 's1', label: 'Registration type', dataType: 'select', options: staticOpts(['SWEC', 'Non-SWEC']) }} value={v('s1')} onChange={set('s1')} />
          <SelectField spec={{ key: 's2', label: 'Currency (custom list)', dataType: 'select', placeholder: 'Select currency', options: { kind: 'customList', listCode: 'CURRENCY' } }} value={v('s2')} onChange={set('s2')} />
          <SelectField spec={{ key: 's3', label: 'Topic (grouped)', dataType: 'select', options: { kind: 'static', options: [{ label: 'Specific RFQ', options: [{ code: 'r1', label: 'RFQ-2026-0001' }, { code: 'r2', label: 'RFQ-2026-0002' }] }] } }} value={v('s3')} onChange={set('s3')} />
        </div>
        <div className="grid g3">
          <SelectField spec={{ key: 'country', label: 'Country', dataType: 'select', options: { kind: 'customList', listCode: 'COUNTRY' } }} value={v('country')} onChange={(x) => setVals((p) => ({ ...p, country: x, state: '' }))} />
          <DependentSelectField spec={{ key: 'state', label: 'State / Region', dataType: 'select', placeholder: 'Select state', options: { kind: 'customList', listCode: 'STATE', parentField: 'country' } }} value={v('state')} onChange={set('state')} parentValue={v('country')} />
          <YesNoField spec={{ key: 'yes', label: 'ISO 9001 certified?', dataType: 'yesNo' }} value={v('yes')} onChange={set('yes')} />
        </div>
      </Section>

      <Section title="MultiSelect / Segmented / Checkbox">
        <div className="grid g3">
          <MultiSelectField spec={{ key: 'multi', label: 'Certifications', dataType: 'multiSelect', options: staticOpts(['ISO 9001', 'ISO 14001', 'CIDB G7']) }} value={v('multi')} onChange={set('multi')} />
          <SegmentedField spec={{ key: 'seg', label: 'Envelope', dataType: 'segmented', help: 'Dual seals technical & commercial separately', options: staticOpts(['Single', 'Dual']) }} value={v('seg')} onChange={set('seg')} />
          <CheckboxField spec={{ key: 'cb', label: 'Active (offered in dropdowns)', dataType: 'boolean' }} value={checked} onChange={setChecked} />
        </div>
      </Section>

      <Section title="Number / Money / Date">
        <div className="grid g3">
          <NumberField spec={{ key: 'n1', label: 'Qty (guarded 0–40)', dataType: 'number', validation: { min: 0, max: 40 }, help: 'The ASN/GRN/invoice cap pattern' }} value={v('n1')} onChange={set('n1')} />
          <NumberField spec={{ key: 'n2', label: 'Lead time', dataType: 'number', unit: 'weeks' }} value={v('n2')} onChange={set('n2')} />
          <NumberField spec={{ key: 'n3', label: 'Withholding tax', dataType: 'percent' }} value={v('n3')} onChange={set('n3')} />
        </div>
        <div className="grid g3">
          <MoneyField spec={{ key: 'money', label: 'Unit price (formats on blur)', dataType: 'money', currencyCode: 'MYR' }} value={v('money')} onChange={set('money')} />
          <DateField spec={{ key: 'date', label: 'Required by', dataType: 'date' }} value={v('date')} onChange={set('date')} />
          <DateField spec={{ key: 'dt', label: 'New close (instant)', dataType: 'dateTime', validation: { min: '2026-07-12T00:00:00Z' } }} value={v('dt')} onChange={set('dt')} />
        </div>
        <div className="grid g3">
          <DateField spec={{ key: 'date-ro', label: 'Read only (dd/MM/yyyy via fmtDay)', dataType: 'date', readOnly: true }} value="2026-07-15" onChange={() => {}} />
          <NumberField spec={{ key: 'score', label: 'Score (commit on blur)', dataType: 'number', validation: { min: 0, max: 100 } }} value={v('score')} onChange={set('score')} commitOnBlur onCommit={() => {}} />
          <MoneyField spec={{ key: 'money-ro', label: 'Money (read only)', dataType: 'money', readOnly: true }} value="98500" onChange={() => {}} />
        </div>
      </Section>

      <Section title="Attachment / Table / Repeating group">
        <div className="grid g2">
          <AttachmentField spec={{ key: 'att', label: 'Certificates', dataType: 'attachment', config: { multiple: true, filetypes: 'PDF, XLSX' } }} value={v('att')} onChange={set('att')} />
          <AttachmentField spec={{ key: 'att-ro', label: 'Read only', dataType: 'attachment', readOnly: true }} value="demo::signed-contract.pdf" onChange={() => {}} />
        </div>
        <TableField spec={{ key: 'table', label: 'Financials (RM’000)', dataType: 'table', config: { columns: ['FY2024', 'FY2025'], rows: ['Revenue', 'Net profit'], cellType: 'money' } }} value={v('table')} onChange={set('table')} />
        <RepeatingGroupField spec={{ key: 'group', label: 'Track record', dataType: 'group', config: { fields: [{ label: 'Project', type: 'short_text' }, { label: 'Contract value', type: 'money' }], max: 3 } }} value={v('group')} onChange={set('group')} />
      </Section>

      <Section title="Buttons">
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
          <Button variant="primary" icon="check">Primary</Button>
          <Button variant="outline">Outline</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger">Danger</Button>
          <Button variant="ghost" red>Ghost red</Button>
          <Button variant="primary" size="sm" icon="plus">Small</Button>
          <Button variant="primary" busy>Busy</Button>
          <Button variant="outline" disabled>Disabled</Button>
          <LinkButton>Link button</LinkButton>
        </div>
      </Section>

      <Section title="Badges, pills, chips — the six tones">
        <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', marginBottom: 10 }}>
          {TONES.map((t) => <StatusBadge key={t} tone={t}>{t}</StatusBadge>)}
        </div>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 10 }}>
          {TONES.map((t) => <Pill key={t} tone={t}>{t}</Pill>)}
        </div>
        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          <Chip title="Mechanical > Piping">SWEC 3.02.11</Chip>
          <Chip hit title="Matched against the RFQ category">SWEC 3.02.14 (hit)</Chip>
          <Chip onRemove={() => {}}>Removable</Chip>
        </div>
      </Section>
    </>
  )
}
