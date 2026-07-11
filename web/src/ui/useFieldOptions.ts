import { useLookups } from '../lib/lookups'
import { isOptionGroups, type FieldOption, type FieldOptionGroup, type FieldSpec } from './fieldSpec'

export interface ResolvedOptions {
  flat: FieldOption[]
  groups: FieldOptionGroup[] | null
  isPending: boolean
}

/**
 * Resolves a FieldSpec's options source: static (flat or grouped) as-is;
 * customList through the existing getCustomLists client with ParentValueCode
 * filtering (lib/lookups.ts) — no new endpoint.
 */
export function useFieldOptions(spec: FieldSpec, parentValue?: string): ResolvedOptions {
  const { isPending, of } = useLookups()
  const src = spec.options
  if (!src) return { flat: [], groups: null, isPending: false }
  if (src.kind === 'static') {
    if (isOptionGroups(src.options)) return { flat: src.options.flatMap((g) => g.options), groups: src.options, isPending: false }
    return { flat: src.options, groups: null, isPending: false }
  }
  return { flat: of(src.listCode, src.parentField !== undefined ? parentValue ?? '' : undefined), groups: null, isPending }
}
