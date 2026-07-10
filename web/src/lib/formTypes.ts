// Mirrors the prototype's QTYPES — the 12 question field types.
export const QTYPES: [string, string][] = [
  ['short_text', 'Short text'],
  ['long_text', 'Long text'],
  ['number', 'Number'],
  ['money', 'Money'],
  ['percent', 'Percentage'],
  ['list', 'List (single)'],
  ['multi', 'Multi-select'],
  ['yesno', 'Yes / No'],
  ['date', 'Date'],
  ['attachment', 'Attachment'],
  ['table', 'Table / matrix'],
  ['group', 'Repeatable group'],
]

export const typeLabel = (t: string): string => QTYPES.find((x) => x[0] === t)?.[1] ?? t

export interface FormItemConfig {
  options?: string[]
  unit?: string
  columns?: string[]
  rows?: string[]
  cellType?: string
  fields?: { label: string; type: string }[]
  max?: number
  filetypes?: string
  multiple?: boolean
}

export function parseConfig(json: string | null | undefined): FormItemConfig {
  if (!json) return {}
  try {
    return JSON.parse(json) as FormItemConfig
  } catch {
    return {}
  }
}

import type { FormItemDto } from '../api/client'

// Editor-friendly item with a parsed config object + a stable client-only id so the
// editor can track expand/move without indices shifting under it.
export interface EditItem {
  id: string
  kind: string
  group: string
  section: string
  label: string
  type: string
  required: boolean
  config: FormItemConfig
  help: string
}

let _idSeq = 0
export const newItemId = () => `q${++_idSeq}`

export const fromDto = (d: FormItemDto): EditItem => ({
  id: newItemId(),
  kind: d.kind ?? 'question',
  group: d.group ?? 'technical',
  section: d.section ?? '',
  label: d.label ?? '',
  type: d.type ?? 'short_text',
  required: d.required ?? false,
  config: parseConfig(d.config),
  help: d.help ?? '',
})

// Default config for a freshly-added question of a given type (mirrors qNewItem).
export const defaultConfig = (type: string): FormItemConfig => {
  if (type === 'list' || type === 'multi') return { options: ['Option 1'] }
  if (type === 'table') return { columns: ['FY2023', 'FY2024', 'FY2025'], rows: ['Revenue', 'Net profit', 'Total assets'], cellType: 'money' }
  if (type === 'group') return { fields: [{ label: 'Project name', type: 'short_text' }, { label: 'Contract value', type: 'money' }, { label: 'Year', type: 'number' }], max: 3 }
  return {}
}

export interface FormSections { technical: string[]; commercial: string[] }
export type GroupKey = 'technical' | 'commercial'

// Sub-groups for a group = declared sections ∪ any section names referenced by items.
export const sectionsOf = (g: GroupKey, sections: FormSections, items: EditItem[]): string[] => {
  const secs = [...(sections[g] ?? [])]
  for (const it of items) if (it.group === g && it.section && !secs.includes(it.section)) secs.push(it.section)
  return secs
}

export const toDto = (e: EditItem, order: number): FormItemDto => ({
  kind: e.kind,
  group: e.group,
  section: e.section,
  label: e.label,
  type: e.type,
  required: e.required,
  config: JSON.stringify(e.config ?? {}),
  help: e.help,
  order,
})

