import { useQuery } from '@tanstack/react-query'
import { getSwec, type SwecCategoryDto } from './client'

export interface SwecNode extends SwecCategoryDto {
  children: SwecNode[]
}

export interface SwecIndex {
  byCode: Map<string, SwecCategoryDto>
  tree: SwecNode[]
  label: (code: string) => string
  path: (code: string) => string
  /** A code plus all of its descendant codes — for branch-aware category matching. */
  desc: (code: string) => string[]
}

function buildIndex(rows: SwecCategoryDto[]): SwecIndex {
  const byCode = new Map<string, SwecCategoryDto>()
  for (const r of rows) if (r.code) byCode.set(r.code, r)

  const nodes = new Map<string, SwecNode>()
  for (const r of rows) if (r.code) nodes.set(r.code, { ...r, children: [] })
  const tree: SwecNode[] = []
  for (const n of nodes.values()) {
    if (n.parentCode && nodes.has(n.parentCode)) nodes.get(n.parentCode)!.children.push(n)
    else tree.push(n)
  }

  const desc = (code: string): string[] => {
    const out: string[] = []
    const walk = (n: SwecNode) => { if (n.code) out.push(n.code); n.children.forEach(walk) }
    const n = nodes.get(code)
    if (n) walk(n); else out.push(code)
    return out
  }

  return {
    byCode,
    tree,
    label: (code) => byCode.get(code)?.name ?? code,
    path: (code) => byCode.get(code)?.pathText ?? code,
    desc,
  }
}

export function useSwec() {
  return useQuery({
    queryKey: ['swec'],
    queryFn: getSwec,
    staleTime: Infinity,
    select: buildIndex,
  })
}
