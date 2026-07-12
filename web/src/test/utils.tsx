import type { ReactElement, ReactNode } from 'react'
import { render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { IdentityProvider } from '../identity'

export function renderWithQuery(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
  return render(ui, { wrapper: Wrapper })
}

/// Includes the IdentityProvider (needed by App / TopBar / vendor screens).
export function renderWithProviders(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <IdentityProvider>{children}</IdentityProvider>
    </QueryClientProvider>
  )
  return render(ui, { wrapper: Wrapper })
}

/// D7: the seeded Standard PR Form as the resolve endpoint returns it — PrForm renders
/// from this; tests that mount PrForm mock resolveEntryForm with it.
export const STANDARD_PR_FORM = {
  formId: 'f0', formCode: 'ef_standard_pr_form', formName: 'Standard PR Form', recordType: 'Requisition',
  fields: [
    { fieldKey: 'Requestor', label: 'Requestor', dataType: 'Text', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 0, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: 'Name', customListCode: null, options: null },
    { fieldKey: 'Department', label: 'Department', dataType: 'Text', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 1, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: 'e.g. Maintenance', customListCode: null, options: null },
    { fieldKey: 'Category', label: 'Category', dataType: 'Text', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 2, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: 'e.g. Piping', customListCode: null, options: null },
    { fieldKey: 'Location', label: 'Location', dataType: 'Text', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 3, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: 'e.g. Bintulu Plant', customListCode: null, options: null },
    { fieldKey: 'Job', label: 'Job / Cost ref', dataType: 'Text', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 4, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: 'JOB-…', customListCode: null, options: null },
    { fieldKey: 'RequiredDate', label: 'Required by', dataType: 'Date', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 5, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: null, customListCode: null, options: null },
    { fieldKey: 'Memo', label: 'Memo / Justification', dataType: 'Text', kind: 'Native', subtab: null, fieldGroup: 'Header', sort: 6, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: true, placeholder: 'Short description of the requirement', customListCode: null, options: null },
  ],
}

/// D7.5: mock plumbing for the ViewPicker rollout mounts — a record type's system view
/// plus a paged run built from PascalCase rows. List tests feed their old fixtures
/// through this (the rows are the same data, keyed the registry way).
export const mockSystemView = (recordType: string) => ({
  id: `sys-${recordType}`, code: 'VIEW-SYS-TEST', name: `All ${recordType}`, recordType,
  ownerUserId: null, isShared: true, isSystem: true, filters: [], columns: [],
})
export const mockViewRun = (recordType: string, rows: Record<string, unknown>[]) => ({
  viewId: `sys-${recordType}`, name: `All ${recordType}`, recordType,
  columns: [], rows, page: 1, size: 50, total: rows.length,
})
