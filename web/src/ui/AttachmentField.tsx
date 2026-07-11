import { useId } from 'react'
import { uploadFile, fileUrl } from '../api/client'
import { Icon } from '../components/Icon'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/**
 * File upload (census F35) — the AnswerInput attachment renderer as a
 * primitive. Value is the stored `<fileId>::<name>` pipe-joined string so the
 * evaluator download path keeps working; wraps the EXISTING uploadFile /
 * fileUrl client (perimeter-scoped — no API change). spec.config carries
 * multiple/filetypes, exactly as question templates store them.
 */
export function AttachmentField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const locked = mode === 'disabled' || mode === 'readOnly'
  const multiple = spec.config?.multiple ?? false
  const files = value ? value.split('|').filter(Boolean) : []

  const add = async (list: FileList | null) => {
    const picked = Array.from(list ?? [])
    if (!picked.length) return
    const uploaded: string[] = []
    for (const f of picked) {
      try { const r = await uploadFile(f); uploaded.push(`${r.id}::${r.name}`) } catch { /* skip failed upload */ }
    }
    if (!uploaded.length) return
    onChange((multiple ? [...files, ...uploaded] : uploaded.slice(0, 1)).join('|'))
  }

  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <div aria-invalid={error ? true : undefined}>
        {!locked && (
          <label className="btn btn-out btn-sm" style={{ cursor: 'pointer', display: 'inline-flex' }}>
            <Icon name="upload" size={13} /> Choose file{multiple ? 's' : ''}
            <input
              id={id}
              type="file"
              multiple={multiple}
              accept={spec.config?.filetypes ? spec.config.filetypes.split(',').map((t) => `.${t.trim().toLowerCase()}`).join(',') : undefined}
              style={{ display: 'none' }}
              aria-label={spec.label}
              onChange={(e) => { void add(e.target.files); e.currentTarget.value = '' }}
            />
          </label>
        )}
        <div style={{ marginTop: 6 }}>
          {files.length === 0 && <span className="hint">No file chosen.</span>}
          {files.map((entry, i) => {
            const [fid, name] = entry.includes('::') ? entry.split('::') : ['', entry]
            return (
              <span className="file" key={i} style={{ display: 'inline-flex', width: 'auto', marginRight: 8 }}>
                <span className="ext">{(name.split('.').pop() ?? 'FILE').toUpperCase()}</span>
                {fid ? <a href={fileUrl(fid)} target="_blank" rel="noreferrer">{name}</a> : name}
                {!locked && (
                  <button type="button" className="lnk" aria-label={`Remove ${name}`} onClick={() => onChange(files.filter((_, x) => x !== i).join('|'))}>
                    <Icon name="x" size={12} />
                  </button>
                )}
              </span>
            )
          })}
        </div>
      </div>
    </FieldChrome>
  )
}
