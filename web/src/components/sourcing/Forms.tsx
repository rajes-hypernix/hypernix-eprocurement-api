import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getForms, getForm, createForm, updateForm, deleteForm } from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, EmptyState, Spinner } from '../ui'
import { dateMY } from '../../lib/format'
import { fromDto, toDto, type EditItem, type FormSections } from '../../lib/formTypes'
import { QuestionEditor } from './QuestionEditor'

export function Forms({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  const openId = route.startsWith('forms/') ? route.slice('forms/'.length) : null
  return openId ? (
    <FormEditor id={openId} onBack={() => onNavigate('forms')} />
  ) : (
    <FormList onOpen={(id) => onNavigate(`forms/${id}`)} />
  )
}

const PURPOSE_LABEL: Record<string, string> = { Rfq: 'RFQ', Onboarding: 'Onboarding', Both: 'RFQ + Onboarding' }
const PURPOSE_CLS: Record<string, string> = { Rfq: 'b-blue', Onboarding: 'b-teal', Both: 'b-green' }

function FormList({ onOpen }: { onOpen: (id: string) => void }) {
  const qc = useQueryClient()
  const { data: forms = [] } = useQuery({ queryKey: ['forms'], queryFn: getForms })
  const [confirmDel, setConfirmDel] = useState<{ id: string; name: string } | null>(null)
  const [purpose, setPurpose] = useState('all')
  const invalidate = () => qc.invalidateQueries({ queryKey: ['forms'] })

  const create = useMutation({
    mutationFn: () => createForm({ name: 'New form', items: [], technicalSections: [], commercialSections: [] }),
    onSuccess: (f) => {
      void invalidate()
      if (f.id) onOpen(f.id)
    },
  })
  const duplicate = useMutation({
    mutationFn: (id: string) =>
      getForm(id).then((src) =>
        createForm({
          name: `${src.name} (copy)`,
          items: (src.items ?? []).map(fromDto).map((it, i) => toDto(it, i)),
          technicalSections: [...(src.technicalSections ?? [])],
          commercialSections: [...(src.commercialSections ?? [])],
        }),
      ),
    onSuccess: () => void invalidate(),
  })
  const remove = useMutation({
    mutationFn: (id: string) => deleteForm(id),
    onSuccess: () => { void invalidate(); setConfirmDel(null) },
  })

  const qCount = (f: (typeof forms)[number], group: string) =>
    (f.items ?? []).filter((i) => i.kind === 'question' && i.group === group).length
  const shown = forms.filter((f) => purpose === 'all' || (f.purpose ?? 'Rfq') === purpose)

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Forms</h1>
          <p>Reusable questionnaires — RFQ questionnaires for sourcing and onboarding question packs for vendor registration.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri btn-sm" onClick={() => create.mutate()}>
          <Icon name="plus" size={15} /> New form
        </button>
      </div>
      <div className="segmented" style={{ marginBottom: 14 }}>
        {['all', 'Rfq', 'Onboarding', 'Both'].map((p) => (
          <button type="button" key={p} className={`btn btn-sm ${purpose === p ? 'btn-pri' : 'btn-out'}`} onClick={() => setPurpose(p)}>
            {p === 'all' ? 'All' : PURPOSE_LABEL[p]}
          </button>
        ))}
      </div>
      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Form</th>
              <th>Purpose</th>
              <th className="amt">Commercial Q</th>
              <th className="amt">Technical Q</th>
              <th className="amt">Version</th>
              <th>Updated</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {shown.map((f) => (
              <tr key={f.id}>
                <td><div style={{ fontWeight: 700 }}>{f.name}</div><div className="hint">{f.code}</div></td>
                <td><span className={`badge ${PURPOSE_CLS[f.purpose ?? 'Rfq'] ?? 'b-grey'}`}>{PURPOSE_LABEL[f.purpose ?? 'Rfq']}</span></td>
                <td className="amt">{qCount(f, 'commercial')}</td>
                <td className="amt">{qCount(f, 'technical')}</td>
                <td className="amt">v{f.version}</td>
                <td>{dateMY(f.updatedUtc)}</td>
                <td className="amt">
                  <div className="rowactions">
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => f.id && onOpen(f.id)}><Icon name="edit" size={14} /> Edit</button>
                    <button type="button" className="btn btn-ghost btn-sm" disabled={duplicate.isPending} onClick={() => f.id && duplicate.mutate(f.id)}>Duplicate</button>
                    <button type="button" className="btn btn-ghost btn-sm" style={{ color: 'var(--red)' }} title="Delete" onClick={() => f.id && setConfirmDel({ id: f.id, name: f.name ?? 'form' })}><Icon name="x" size={14} /></button>
                  </div>
                </td>
              </tr>
            ))}
            {shown.length === 0 && <tr><td colSpan={7}><EmptyState>No forms in this view.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>

      {confirmDel && (
        <ConfirmModal
          icon="x" title="Delete form"
          body={<p className="hint" style={{ marginTop: 0 }}>Delete <b>{confirmDel.name}</b>? This cannot be undone.</p>}
          confirmLabel="Delete" danger busy={remove.isPending}
          onCancel={() => setConfirmDel(null)} onConfirm={() => remove.mutate(confirmDel.id)}
        />
      )}
    </>
  )
}

function FormEditor({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { data: form, isPending } = useQuery({ queryKey: ['form', id], queryFn: () => getForm(id) })
  const [name, setName] = useState<string | null>(null)
  const [purpose, setPurpose] = useState<string | null>(null)
  const [items, setItems] = useState<EditItem[] | null>(null)
  const [sections, setSections] = useState<FormSections | null>(null)
  const [notice, setNotice] = useState('')

  const save = useMutation({
    mutationFn: () => {
      const its = items ?? (form?.items ?? []).map(fromDto)
      const secs = sections ?? { technical: [...(form?.technicalSections ?? [])], commercial: [...(form?.commercialSections ?? [])] }
      return updateForm(id, {
        name: name ?? form?.name ?? 'Form',
        purpose: purpose ?? form?.purpose ?? 'Rfq',
        items: its.map((it, i) => toDto(it, i)),
        technicalSections: secs.technical,
        commercialSections: secs.commercial,
      })
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['forms'] })
      void qc.invalidateQueries({ queryKey: ['form', id] })
      setNotice('Form saved')
    },
    onError: (e: Error) => setNotice(e.message),
  })

  if (isPending || !form) return <Spinner label="Loading form…" />
  const curName = name ?? form.name ?? ''
  const curPurpose = purpose ?? form.purpose ?? 'Rfq'
  const curItems = items ?? (form.items ?? []).map(fromDto)
  const curSections: FormSections = sections ?? { technical: [...(form.technicalSections ?? [])], commercial: [...(form.commercialSections ?? [])] }

  return (
    <>
      <div className="crumb">
        <a onClick={onBack}>Forms</a> <Icon name="chev" size={13} /> <span>{form.name}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Edit form</h1>
          <p>{form.code} · v{form.version}</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" onClick={() => save.mutate()}>
          <Icon name="chev" size={15} /> Save form
        </button>
      </div>
      {notice && <div className="ribbon" style={{ marginBottom: 14 }}>{notice}</div>}
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="cbody">
          <div className="frow">
            <div className="field" style={{ maxWidth: 420 }}>
              <label>Form name</label>
              <input type="text" value={curName} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="field" style={{ maxWidth: 220 }}>
              <label>Purpose</label>
              <select value={curPurpose} aria-label="Purpose" onChange={(e) => setPurpose(e.target.value)}>
                <option value="Rfq">RFQ questionnaire</option>
                <option value="Onboarding">Onboarding pack</option>
                <option value="Both">RFQ + Onboarding</option>
              </select>
            </div>
          </div>
        </div>
      </div>
      <QuestionEditor items={curItems} sections={curSections} onChange={(i, s) => { setItems(i); setSections(s) }} />
    </>
  )
}
