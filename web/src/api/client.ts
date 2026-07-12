import type { components } from './schema'

type S = components['schemas']
export type HealthResponse = S['HealthResponse']
export type VendorListItem = S['VendorListItem']
export type VendorDetail = S['VendorDetail']
export type UserDto = S['UserDto']
export type VendorLoginDto = S['VendorLoginDto']
export type SwecCategoryDto = S['SwecCategoryDto']
export type AuditEntryDto = S['AuditEntryDto']
export type SetCategoriesRequest = S['SetCategoriesRequest']
export type CreateUserRequest = S['CreateUserRequest']
export type UpdateUserRequest = S['UpdateUserRequest']
export type RequisitionDto = S['RequisitionDto']
export type PrLineDto = S['PrLineDto']
export type RfqListItem = S['RfqListItem']
export type RfqDetail = S['RfqDetail']
export type RfqInvitationDto = S['RfqInvitationDto']
export type RfqEventDto = S['RfqEventDto']
export type RfqLineDto = S['RfqLineDto']
export type FormItemDto = S['FormItemDto']
// `purpose` (Rfq | Onboarding | Both) is added by Slice E; augmented here until the schema is regenerated.
export type FormTemplateDto = S['FormTemplateDto'] & { purpose?: string }
export type CreateRfqDraftRequest = S['CreateRfqDraftRequest']
export type UpdateRfqDraftRequest = S['UpdateRfqDraftRequest']
export type SaveFormTemplateRequest = S['SaveFormTemplateRequest'] & { purpose?: string }
export type PersonaDto = S['PersonaDto']
export type InvitationDto = S['InvitationDto']
export type BidDto = S['BidDto']
export type BidLineDto = S['BidLineDto']
export type BidAnswerDto = S['BidAnswerDto']
export type SaveBidRequest = S['SaveBidRequest']
export type BidOpeningDto = S['BidOpeningDto']
export type TechnicalEvalDto = S['TechnicalEvalDto']
export type VendorScoreDto = S['VendorScoreDto']
export type SetScoreRequest = S['SetScoreRequest']
export type ClarificationThreadDto = S['ClarificationThreadDto']
export type ClarificationThreadDetail = S['ClarificationThreadDetail']
export type ClarificationMessageDto = S['ClarificationMessageDto']
export type SendClarificationRequest = S['SendClarificationRequest']
export type AwardEligibilityDto = S['AwardEligibilityDto']
export type AwardDto = S['AwardDto']
export type AwardLineDto = S['AwardLineDto']
export type SubmitAwardRequest = S['SubmitAwardRequest']
export type PoListItem = S['PoListItem']
export type PoDetail = S['PoDetail']
export type PoLineDto = S['PoLineDto']
export type AsnListDto = S['AsnListDto']
export type AsnDetailDto = S['AsnDetailDto']
export type ShipPlanDto = S['ShipPlanDto']
export type GrnDetailDto = S['GrnDetailDto']
export type CreateAsnRequest = S['CreateAsnRequest']
export type ReceiveRequest = S['ReceiveRequest']
export type InvoiceListDto = S['InvoiceListDto']
export type InvoiceDetailDto = S['InvoiceDetailDto']
export type InvoiceBillablePlan = S['InvoiceBillablePlan']
export type SubmitInvoiceRequest = S['SubmitInvoiceRequest']
export type StatementSummaryDto = S['StatementSummaryDto']
export type StatementDetailDto = S['StatementDetailDto']

// Calls go through the Vite dev proxy (/api -> http://localhost:5260).
const BASE = '/api'

// The acting demo persona (dev only): sent as X-Demo-User so the server derives
// roles/vendor server-side. Identity is never trusted from a client flag.
function lsGet(key: string): string | null {
  try {
    return typeof localStorage?.getItem === 'function' ? localStorage.getItem(key) : null
  } catch {
    return null
  }
}
function lsSet(key: string, value: string): void {
  try {
    if (typeof localStorage?.setItem === 'function') localStorage.setItem(key, value)
  } catch {
    /* ignore (non-browser env) */
  }
}

let demoUser = lsGet('demoUser') ?? 'u_faridah'
export function setDemoUser(code: string) {
  demoUser = code
  lsSet('demoUser', code)
}
export const getDemoUser = () => demoUser

async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers({ Accept: 'application/json', 'Content-Type': 'application/json' })
  headers.set('X-Demo-User', demoUser)
  if (init?.headers) new Headers(init.headers).forEach((value, key) => headers.set(key, value))
  const res = await fetch(`${BASE}${path}`, { ...init, headers })
  if (!res.ok) {
    let detail = `${res.status}`
    try {
      const body = (await res.json()) as { detail?: string; title?: string }
      detail = body.detail ?? body.title ?? detail
    } catch {
      /* non-JSON error body */
    }
    throw new Error(detail)
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
}

export const getHealth = () => http<HealthResponse>('/health')
export const getPersonas = () => http<PersonaDto[]>('/auth/personas')
// The caller's allowed actions (AUTHORIZATION-MATRIX A58) — the source the display
// gating derives from; ui/gating.tsx holds no static role map.
export const getPermissions = () => http<string[]>('/auth/permissions')

// --- Saved views engine (D3, AUTHORIZATION-MATRIX A59-A61) ---
export type SavedViewFilterDto = { fieldKey: string; operator: string; value: string; value2?: string | null }
export type SavedViewColumnDto = { fieldKey: string; label?: string | null; sortDirection?: string | null }
export type SavedViewDto = {
  id: string; code: string; name: string; recordType: string; ownerUserId?: string | null
  isShared: boolean; isSystem: boolean
  filters: SavedViewFilterDto[]; columns: SavedViewColumnDto[]
}
export type ViewFieldDto = { fieldKey: string; label: string; dataType: string; kind: string; options?: string[] | null }
export type ViewRunColumn = { fieldKey: string; label: string; dataType: string }
export type ViewRunResult = {
  viewId: string; name: string; recordType: string
  columns: ViewRunColumn[]; rows: Record<string, unknown>[]
}
export type SaveViewRequest = { name: string; recordType: string; filters: SavedViewFilterDto[]; columns: SavedViewColumnDto[] }

export const getViews = (recordType?: string) => http<SavedViewDto[]>(`/views${recordType ? `?recordType=${encodeURIComponent(recordType)}` : ''}`)
export const getViewFields = (recordType: string) => http<ViewFieldDto[]>(`/views/fields?recordType=${encodeURIComponent(recordType)}`)
export const createView = (req: SaveViewRequest) => http<SavedViewDto>('/views', { method: 'POST', body: JSON.stringify(req) })
export const updateView = (id: string, req: SaveViewRequest) => http<SavedViewDto>(`/views/${id}`, { method: 'PUT', body: JSON.stringify(req) })
export const deleteView = (id: string) => http<undefined>(`/views/${id}`, { method: 'DELETE' })
export const shareView = (id: string, isShared: boolean) => http<SavedViewDto>(`/views/${id}/share`, { method: 'POST', body: JSON.stringify({ isShared }) })
export const runView = (id: string) => http<ViewRunResult>(`/views/${id}/run`)

// --- Dashboards + metric layer (D4, AUTHORIZATION-MATRIX A62-A64) ---
export type PortletDto = {
  id: string; portletType: string; title: string
  col: number; row: number; width: number
  savedViewId?: string | null; configJson: string
}
export type UserDashboardDto = { id: string; name: string; isPersonalized: boolean; portlets: PortletDto[] }
export type PortletUpsert = {
  id?: string | null; portletType: string; title: string
  col: number; row: number; width: number
  savedViewId?: string | null; configJson: string
}
export type UpdateDashboardRequest = { name?: string | null; portlets: PortletUpsert[] }
export type MetricValueDto = { id: string; label: string; unit: string; value?: number | null; notYetAvailable: boolean; excludedNullCount: number }
export type SeriesBucketDto = { bucket: string; value: number }
export type MetricSeriesDto = { id: string; label: string; unit: string; buckets: SeriesBucketDto[]; unbucketedCount: number }
export type ViewAggregateGroup = { key: string; label: string; value?: number | null }
export type ViewAggregateResult = {
  viewId: string; fn: string; fieldKey?: string | null; value?: number | null; excludedNullCount: number
  groupedBy?: string | null; groups?: ViewAggregateGroup[] | null
}

export const getMyDashboard = () => http<UserDashboardDto>('/dashboards/mine')
export const personalizeDashboard = () => http<UserDashboardDto>('/dashboards/personalize', { method: 'POST' })
export const updateMyDashboard = (req: UpdateDashboardRequest) => http<UserDashboardDto>('/dashboards/mine', { method: 'PUT', body: JSON.stringify(req) })
export const resetMyDashboard = () => http<undefined>('/dashboards/mine', { method: 'DELETE' })
export const getMetricValue = (id: string) => http<MetricValueDto>(`/metrics/${id}/value`)
export const getMetricSeries = (id: string, months = 12) => http<MetricSeriesDto>(`/metrics/${id}/series?months=${months}`)
export const aggregateView = (id: string, fn: string, field?: string | null, groupBy?: string | null) =>
  http<ViewAggregateResult>(`/views/${id}/aggregate?fn=${fn}${field ? `&field=${encodeURIComponent(field)}` : ''}${groupBy ? `&groupBy=${encodeURIComponent(groupBy)}` : ''}`)

// --- Custom fields (D5, AUTHORIZATION-MATRIX A65-A67) ---
export type CustomFieldDefDto = {
  id: string; code: string; label: string; recordType: string; dataType: string
  customListId?: string | null; required: boolean; helpText: string; active: boolean; sort: number; valueCount: number
}
export type SaveCustomFieldDefRequest = {
  label: string; recordType: string; dataType: string; customListId?: string | null
  required: boolean; helpText: string; sort: number
}
export type CustomValueDto = {
  code: string; label: string; dataType: string; required: boolean; helpText: string
  customListCode?: string | null; value?: string | null
}
export const getCustomFieldDefs = (recordType?: string) =>
  http<CustomFieldDefDto[]>(`/custom-fields${recordType ? `?recordType=${recordType}` : ''}`)
export const createCustomFieldDef = (req: SaveCustomFieldDefRequest) =>
  http<CustomFieldDefDto>('/custom-fields', { method: 'POST', body: JSON.stringify(req) })
export const updateCustomFieldDef = (id: string, req: SaveCustomFieldDefRequest) =>
  http<CustomFieldDefDto>(`/custom-fields/${id}`, { method: 'PUT', body: JSON.stringify(req) })
export const setCustomFieldActive = (id: string, active: boolean) =>
  http<CustomFieldDefDto>(`/custom-fields/${id}/active`, { method: 'POST', body: JSON.stringify(active) })
export const deleteCustomFieldDef = (id: string) => http<undefined>(`/custom-fields/${id}`, { method: 'DELETE' })
export const getCustomValues = (recordType: string, recordId: string) =>
  http<CustomValueDto[]>(`/custom-values/${recordType}/${recordId}`)
export const saveCustomValues = (recordType: string, recordId: string, values: Record<string, string | null>) =>
  http<CustomValueDto[]>(`/custom-values/${recordType}/${recordId}`, { method: 'PUT', body: JSON.stringify({ values }) })

// --- Segments (D6, AUTHORIZATION-MATRIX A68 + the folded A66/A67) ---
export type SegmentValueDto = { id: string; code: string; label: string; parentValueId?: string | null; active: boolean; sort: number }
export type SegmentApplicationDto = { recordType: string; lineLevel: boolean }
export type SegmentDefDto = {
  id: string; code: string; name: string; hasHierarchy: boolean; required: boolean; active: boolean; isSystem: boolean
  values: SegmentValueDto[]; applications: SegmentApplicationDto[]
}
export type SaveSegmentDefRequest = { name: string; hasHierarchy: boolean; required: boolean }
export type SegmentAssignmentDto = {
  segmentCode: string; segmentName: string; required: boolean
  options: SegmentValueDto[]; valueCode?: string | null; valueLabel?: string | null
}
export const getSegmentDefs = () => http<SegmentDefDto[]>('/segments')
export const createSegmentDef = (req: SaveSegmentDefRequest) =>
  http<SegmentDefDto>('/segments', { method: 'POST', body: JSON.stringify(req) })
export const updateSegmentDef = (id: string, req: SaveSegmentDefRequest) =>
  http<SegmentDefDto>(`/segments/${id}`, { method: 'PUT', body: JSON.stringify(req) })
export const addSegmentValue = (id: string, req: { label: string; parentValueId?: string | null; sort: number }) =>
  http<SegmentDefDto>(`/segments/${id}/values`, { method: 'POST', body: JSON.stringify(req) })
export const applySegment = (id: string, req: { recordType: string; lineLevel: boolean }) =>
  http<SegmentDefDto>(`/segments/${id}/applications`, { method: 'POST', body: JSON.stringify(req) })
export const unapplySegment = (id: string, recordType: string) =>
  http<SegmentDefDto>(`/segments/${id}/applications/${recordType}`, { method: 'DELETE' })
export const getSegmentAssignments = (recordType: string, recordId: string, lineId?: string | null) =>
  http<SegmentAssignmentDto[]>(`/segment-assignments/${recordType}/${recordId}${lineId ? `?lineId=${lineId}` : ''}`)
export const saveSegmentAssignments = (recordType: string, recordId: string, assignments: Record<string, string | null>, lineId?: string | null) =>
  http<SegmentAssignmentDto[]>(`/segment-assignments/${recordType}/${recordId}`, { method: 'PUT', body: JSON.stringify({ assignments, lineId: lineId ?? null }) })

// --- Entry forms + numbering (D7, AUTHORIZATION-MATRIX A69-A71) ---
export type EntryFormFieldDto = {
  fieldKey: string; subtab?: string | null; fieldGroup: string; sort: number; displayType: string
  requiredOnForm: boolean; defaultValue?: string | null; sourceFieldKey?: string | null
  fullWidth: boolean; label?: string | null; placeholder?: string | null
}
export type EntryFormDefDto = {
  id: string; code: string; name: string; recordType: string; isSystem: boolean; active: boolean
  fields: EntryFormFieldDto[]; roles: string[]
}
export type SaveEntryFormRequest = { name: string; recordType: string; fields: EntryFormFieldDto[] }
export type ResolvedFormFieldDto = {
  fieldKey: string; label: string; dataType: string; kind: string; subtab?: string | null
  fieldGroup: string; sort: number; displayType: string; requiredOnForm: boolean
  defaultValue?: string | null; sourceFieldKey?: string | null; fullWidth: boolean
  placeholder?: string | null; customListCode?: string | null
  options?: { code: string; label: string }[] | null
}
export type ResolvedFormDto = { formId: string; formCode: string; formName: string; recordType: string; fields: ResolvedFormFieldDto[] }
export type NumberingSchemeDto = { recordType: string; prefix: string; yearSegment: boolean; digits: number; nextPreview: string }

export const getEntryForms = (recordType?: string) =>
  http<EntryFormDefDto[]>(`/entry-forms${recordType ? `?recordType=${recordType}` : ''}`)
export const createEntryForm = (req: SaveEntryFormRequest) =>
  http<EntryFormDefDto>('/entry-forms', { method: 'POST', body: JSON.stringify(req) })
export const updateEntryForm = (id: string, req: SaveEntryFormRequest) =>
  http<EntryFormDefDto>(`/entry-forms/${id}`, { method: 'PUT', body: JSON.stringify(req) })
export const deleteEntryForm = (id: string) => http<undefined>(`/entry-forms/${id}`, { method: 'DELETE' })
export const assignEntryFormRoles = (id: string, roles: string[]) =>
  http<EntryFormDefDto>(`/entry-forms/${id}/roles`, { method: 'PUT', body: JSON.stringify({ roles }) })
export const resolveEntryForm = (recordType: string) =>
  http<ResolvedFormDto>(`/entry-forms/resolve?recordType=${encodeURIComponent(recordType)}`)
export const getNumberingSchemes = () => http<NumberingSchemeDto[]>('/numbering')
export const updateNumberingScheme = (recordType: string, req: { prefix: string; yearSegment: boolean; digits: number }) =>
  http<NumberingSchemeDto>(`/numbering/${recordType}`, { method: 'PUT', body: JSON.stringify(req) })

// --- Vendor portal: invitations + bidding ---
export const getMyInvitations = () => http<InvitationDto[]>('/my/rfqs')
export const getMyBid = (rfqId: string) =>
  http<BidDto | null>(`/rfqs/${rfqId}/my-bid`).catch(() => null)
export const saveBidDraft = (rfqId: string, body: SaveBidRequest) =>
  http<BidDto>(`/rfqs/${rfqId}/my-bid`, { method: 'PUT', body: JSON.stringify(body) })
export const submitBid = (rfqId: string, body: SaveBidRequest) =>
  http<BidDto>(`/rfqs/${rfqId}/my-bid/submit`, { method: 'POST', body: JSON.stringify(body) })

// --- Vendors ---
export function getVendors(filter: { q?: string; type?: string; region?: string } = {}) {
  const p = new URLSearchParams()
  if (filter.q) p.set('q', filter.q)
  if (filter.type && filter.type !== 'all') p.set('type', filter.type)
  if (filter.region && filter.region !== 'all') p.set('region', filter.region)
  const qs = p.toString()
  return http<VendorListItem[]>(`/vendors${qs ? `?${qs}` : ''}`)
}
export const getVendor = (id: string) => http<VendorDetail>(`/vendors/${id}`)
export const getVendorAudit = (id: string) => http<AuditEntryDto[]>(`/vendors/${id}/audit`)
export const getPoAudit = (id: string) => http<AuditEntryDto[]>(`/pos/${id}/audit`)
export const setVendorCategories = (id: string, body: SetCategoriesRequest) =>
  http<VendorDetail>(`/vendors/${id}/categories`, { method: 'PUT', body: JSON.stringify(body) })
export const toggleVendorStatus = (id: string) =>
  http<VendorDetail>(`/vendors/${id}/toggle-status`, { method: 'POST' })

// --- Users ---
export const getUsers = () => http<UserDto[]>('/users')
export const getVendorLogins = () => http<VendorLoginDto[]>('/users/vendor-logins')
export const createUser = (body: CreateUserRequest) =>
  http<UserDto>('/users', { method: 'POST', body: JSON.stringify(body) })
export const updateUser = (id: string, body: UpdateUserRequest) =>
  http<UserDto>(`/users/${id}`, { method: 'PUT', body: JSON.stringify(body) })

// --- SWEC ---
export const getSwec = () => http<SwecCategoryDto[]>('/swec')

// --- Requisitions ---
export type SavePrRequest = S['SavePrRequest']
export const getRequisitions = () => http<RequisitionDto[]>('/requisitions')
export const getRequisition = (id: string) => http<RequisitionDto>(`/requisitions/${id}`)
export const createPr = (body: SavePrRequest, submit: boolean) =>
  http<RequisitionDto>(`/requisitions?submit=${submit}`, { method: 'POST', body: JSON.stringify(body) })
export const updatePr = (id: string, body: SavePrRequest) =>
  http<RequisitionDto>(`/requisitions/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const submitPr = (id: string) =>
  http<RequisitionDto>(`/requisitions/${id}/submit`, { method: 'POST' })
export const cancelPrLine = (id: string, lineId: string, reason: string) =>
  http<RequisitionDto>(`/requisitions/${id}/lines/${lineId}/cancel`, { method: 'POST', body: JSON.stringify({ reason }) })
export const releasePrLine = (id: string, lineId: string, reason: string) =>
  http<RequisitionDto>(`/requisitions/${id}/lines/${lineId}/release`, { method: 'POST', body: JSON.stringify({ reason }) })
export const reopenPrLine = (id: string, lineId: string) =>
  http<RequisitionDto>(`/requisitions/${id}/lines/${lineId}/reopen`, { method: 'POST' })
export const reservePrLine = (id: string, lineId: string) =>
  http<RequisitionDto>(`/requisitions/${id}/lines/${lineId}/reserve`, { method: 'POST' })
export const unreservePrLine = (id: string, lineId: string) =>
  http<RequisitionDto>(`/requisitions/${id}/lines/${lineId}/unreserve`, { method: 'POST' })
export const cancelPr = (id: string, reason: string) =>
  http<RequisitionDto>(`/requisitions/${id}/cancel`, { method: 'POST', body: JSON.stringify({ reason }) })

// --- RFQs ---

export const getRfqs = () => http<RfqListItem[]>('/rfqs')
export const getRfq = (id: string) => http<RfqDetail>(`/rfqs/${id}`)
export const createRfqDraft = (body: CreateRfqDraftRequest) =>
  http<RfqDetail>('/rfqs', { method: 'POST', body: JSON.stringify(body) })
export const updateRfqDraft = (id: string, body: UpdateRfqDraftRequest) =>
  http<RfqDetail>(`/rfqs/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const releaseRfq = (id: string) =>
  http<RfqDetail>(`/rfqs/${id}/release`, { method: 'POST' })
export const closeRfq = (id: string) => http<RfqDetail>(`/rfqs/${id}/close`, { method: 'POST' })
export const cancelRfq = (id: string) => http<RfqDetail>(`/rfqs/${id}/cancel`, { method: 'POST' })

// --- RFQ governance (Slice I/J): invitations, rescind, extend ---
export const inviteVendorToRfq = (id: string, vendorId: string) =>
  http<RfqDetail>(`/rfqs/${id}/invitations`, { method: 'POST', body: JSON.stringify({ vendorId }) })
export const rescindInvitation = (id: string, vendorId: string, reasonCode: string, note: string | null) =>
  http<RfqDetail>(`/rfqs/${id}/invitations/${vendorId}/rescind`, { method: 'POST', body: JSON.stringify({ reasonCode, note }) })
export const extendRfq = (id: string, newClosesUtc: string, reasonCode: string, note: string | null) =>
  http<RfqDetail>(`/rfqs/${id}/extend`, { method: 'POST', body: JSON.stringify({ newClosesUtc, reasonCode, note }) })

// Vendor-side invitation actions (own invitation only, scoped server-side).
export const declineRfqInvitation = (rfqId: string, reasonCode: string, note: string | null) =>
  http<void>(`/my/rfqs/${rfqId}/decline`, { method: 'POST', body: JSON.stringify({ reasonCode, note }) })
export const intendRfq = (rfqId: string) => http<void>(`/my/rfqs/${rfqId}/intend`, { method: 'POST' })
export const withdrawBid = (rfqId: string) => http<void>(`/my/rfqs/${rfqId}/withdraw-bid`, { method: 'POST' })

// --- Bid opening + technical evaluation ---
export const getOpening = (rfqId: string) => http<BidOpeningDto>(`/rfqs/${rfqId}/opening`)
export const openTechnical = (rfqId: string) => http<BidOpeningDto>(`/rfqs/${rfqId}/open-technical`, { method: 'POST' })
export const openCommercial = (rfqId: string) => http<BidOpeningDto>(`/rfqs/${rfqId}/open-commercial`, { method: 'POST' })
export const getTechnicalEval = (rfqId: string) => http<TechnicalEvalDto>(`/rfqs/${rfqId}/technical-eval`)
export const setScore = (rfqId: string, body: SetScoreRequest) =>
  http<TechnicalEvalDto>(`/rfqs/${rfqId}/scores`, { method: 'POST', body: JSON.stringify(body) })
export const finalizeTechnical = (rfqId: string) =>
  http<TechnicalEvalDto>(`/rfqs/${rfqId}/finalize-technical`, { method: 'POST' })

// --- Purchase Orders ---
export const getPos = () => http<PoListItem[]>('/pos')
export const getPo = (id: string) => http<PoDetail>(`/pos/${id}`)
export const issuePo = (id: string) => http<PoDetail>(`/pos/${id}/issue`, { method: 'POST' })
export const acknowledgePo = (id: string) => http<PoDetail>(`/pos/${id}/acknowledge`, { method: 'POST' })

// --- Deliveries (ASN / GRN) ---
export const getAsns = () => http<AsnListDto[]>('/asns')
export const getAsn = (id: string) => http<AsnDetailDto>(`/asns/${id}`)
export const getGrnForAsn = (id: string) => http<GrnDetailDto | null>(`/asns/${id}/grn`).catch(() => null)
export const receiveAsn = (id: string, body: ReceiveRequest) =>
  http<GrnDetailDto>(`/asns/${id}/receive`, { method: 'POST', body: JSON.stringify(body) })
export const getShipPlan = (poId: string) => http<ShipPlanDto>(`/pos/${poId}/ship-plan`)
export const createAsn = (poId: string, body: CreateAsnRequest) =>
  http<AsnDetailDto>(`/pos/${poId}/asns`, { method: 'POST', body: JSON.stringify(body) })

// --- Invoices (3-way match) ---
export const getInvoices = () => http<InvoiceListDto[]>('/invoices')
export const getInvoice = (id: string) => http<InvoiceDetailDto>(`/invoices/${id}`)
export const getBillablePlan = (poId: string) => http<InvoiceBillablePlan>(`/pos/${poId}/billable`)
export const submitInvoice = (poId: string, body: SubmitInvoiceRequest) =>
  http<InvoiceDetailDto>(`/pos/${poId}/invoices`, { method: 'POST', body: JSON.stringify(body) })
export const approveInvoice = (id: string) => http<InvoiceDetailDto>(`/invoices/${id}/approve`, { method: 'POST' })
export const resolveInvoice = (id: string) => http<InvoiceDetailDto>(`/invoices/${id}/resolve`, { method: 'POST' })

// --- Statements (SOA) ---
export const getStatements = () => http<StatementSummaryDto[]>('/statements')
export const getStatement = (vendorId: string) => http<StatementDetailDto>(`/statements/${vendorId}`)
export const getMyStatement = () => http<StatementDetailDto | null>('/my/statement').catch(() => null)

// --- Awards ---
export const getAwards = () => http<AwardDto[]>('/awards')
export const getAwardEligibility = (rfqId: string) => http<AwardEligibilityDto>(`/rfqs/${rfqId}/award-eligibility`)
export const getAwardForRfq = (rfqId: string) => http<AwardDto | null>(`/rfqs/${rfqId}/award`).catch(() => null)
export const submitAward = (rfqId: string, body: SubmitAwardRequest) =>
  http<AwardDto>(`/rfqs/${rfqId}/award`, { method: 'POST', body: JSON.stringify(body) })
export const approveAward = (awardId: string) =>
  http<AwardDto>(`/awards/${awardId}/approve`, { method: 'POST' })

// --- Forms ---
export const getForms = () => http<FormTemplateDto[]>('/forms')
export const getForm = (id: string) => http<FormTemplateDto>(`/forms/${id}`)
export const createForm = (body: SaveFormTemplateRequest) =>
  http<FormTemplateDto>('/forms', { method: 'POST', body: JSON.stringify(body) })
export const updateForm = (id: string, body: SaveFormTemplateRequest) =>
  http<FormTemplateDto>(`/forms/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const deleteForm = (id: string) => http<void>(`/forms/${id}`, { method: 'DELETE' })

// ---- File upload / download (bid attachments) ----
export const fileUrl = (id: string) => `${BASE}/files/${id}`
export async function uploadFile(file: File): Promise<{ id: string; name: string; size: number }> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE}/files`, { method: 'POST', headers: { 'X-Demo-User': demoUser }, body: form })
  if (!res.ok) throw new Error('File upload failed')
  return (await res.json()) as { id: string; name: string; size: number }
}

export const getClarificationThreads = () => http<ClarificationThreadDto[]>('/clarifications')
export const getClarificationThread = (scope: string, vendorId: string) =>
  http<ClarificationThreadDetail>(`/clarifications/thread?scope=${encodeURIComponent(scope)}&vendorId=${vendorId}`)
export const sendClarification = (body: SendClarificationRequest) =>
  http<ClarificationThreadDetail>('/clarifications', { method: 'POST', body: JSON.stringify(body) })

// --- Vendor onboarding (invitations + magic link) ---
// Types are hand-declared (mirror the API DTOs) so the client stays usable without a schema regen.
export type OnboardingTemplate = { id: string; code: string; name: string; questionCount: number }
export type OnboardingInvitation = {
  id: string; email: string; type: string; status: string; invitedByName: string
  createdUtc: string; expiresUtc: string; applicationId: string | null
  applicationCode: string | null; magicLink: string
}
export type OnboardingApplication = {
  id: string; code: string; status: string; type: string; name: string; email: string
  packs: OnboardingTemplate[]; rounds: OnboardingRound[]; createdUtc: string; submittedUtc: string | null
}
export type SendOnboardingInvitationRequest = {
  email: string; type: string; name?: string; selectedTemplateIds: string[]
}

export const getOnboardingTemplates = () => http<OnboardingTemplate[]>('/onboarding/templates')
export const sendOnboardingInvitation = (body: SendOnboardingInvitationRequest) =>
  http<OnboardingInvitation>('/onboarding/invitations', { method: 'POST', body: JSON.stringify(body) })
export const resendOnboardingInvitation = (id: string) =>
  http<OnboardingInvitation>(`/onboarding/invitations/${id}/resend`, { method: 'POST' })
export const revokeOnboardingInvitation = (id: string) =>
  http<void>(`/onboarding/invitations/${id}/revoke`, { method: 'POST' })
export const resolveOnboardingLink = (token: string) =>
  http<OnboardingApplication>('/onboarding/resolve', { method: 'POST', body: JSON.stringify({ token }) })

// --- Vendor onboarding form (Slice C): token-scoped draft / save / submit / documents ---
export type OnboardingFormItem = {
  kind: string; group: string; section: string; label: string; type: string
  required: boolean; config: string; help: string; order: number
}
export type OnboardingPack = { id: string; code: string; name: string; items: OnboardingFormItem[] }
export type OnboardingBank = { bank: string; accountNo: string; swift: string }
export type OnboardingAnswerDto = { formTemplateId: string; questionOrder: number; value: string }
export type OnboardingDocumentDto = { key: string; fileName: string; storedFileId: string }
export type OnboardingFinancialYear = {
  yearIndex: number; revenue: number; netProfit: number; ebit: number; totalAssets: number
  currentAssets: number; inventory: number; currentLiabilities: number; totalLiabilities: number
  equity: number; retainedEarnings: number; fixedAssets: number
}
export type OnboardingDraft = {
  id: string; code: string; status: string; type: string
  name: string; registrationNo: string; location: string; email: string; contactName: string; contactPhone: string
  bank: OnboardingBank; categories: string[]
  financials: OnboardingFinancialYear[]; answers: OnboardingAnswerDto[]
  documents: OnboardingDocumentDto[]; packs: OnboardingPack[]
  financialBand: string | null; submittedUtc: string | null
  country: string; state: string
}
export type SaveOnboardingDraft = {
  token: string
  name?: string | null; registrationNo?: string | null; location?: string | null; email?: string | null
  contactName?: string | null; contactPhone?: string | null
  bank?: OnboardingBank | null; categories?: string[] | null
  financials?: OnboardingFinancialYear[] | null; answers?: OnboardingAnswerDto[] | null
  country?: string | null; state?: string | null
}

export const getOnboardingDraft = (token: string) =>
  http<OnboardingDraft>(`/onboarding/draft?token=${encodeURIComponent(token)}`)
export const saveOnboardingDraft = (body: SaveOnboardingDraft) =>
  http<OnboardingDraft>('/onboarding/draft', { method: 'PUT', body: JSON.stringify(body) })
export const submitOnboardingDraft = (token: string) =>
  http<OnboardingDraft>('/onboarding/draft/submit', { method: 'POST', body: JSON.stringify({ token }) })
export const deleteOnboardingDocument = (token: string, key: string) =>
  http<void>(`/onboarding/draft/documents/${key}?token=${encodeURIComponent(token)}`, { method: 'DELETE' })
export async function uploadOnboardingDocument(token: string, key: string, file: File): Promise<OnboardingDocumentDto> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE}/onboarding/draft/documents?token=${encodeURIComponent(token)}&key=${encodeURIComponent(key)}`, {
    method: 'POST', headers: { 'X-Demo-User': demoUser }, body: form,
  })
  if (!res.ok) throw new Error('Document upload failed')
  return (await res.json()) as OnboardingDocumentDto
}

// --- Vendor onboarding review (Slice D): buyer queue / review / clarify / approve, vendor resubmit ---
export type OnboardingRoundItem = { topic: string; request: string; response: string }
export type OnboardingRound = {
  roundNo: number; direction: string; status: string; message: string; raisedByName: string
  raisedUtc: string; respondedUtc: string | null; items: OnboardingRoundItem[]
}
export type OnboardingQueueItem = {
  id: string; code: string; name: string; type: string; status: string; source: string
  createdUtc: string; submittedUtc: string | null; openRoundNo: number | null; roundCount: number
  invitationId: string | null
}
export type OnboardingYearCalc = { yearIndex: number; x1: number; x2: number; x3: number; x4: number; x5: number; z: number }
export type OnboardingFinancialView = {
  band: string; risk: string; zone: string; weightedZ: number; score: number; statement: string; years: OnboardingYearCalc[]
}
export type OnboardingAnswerView = { pack: string; label: string; value: string }
export type OnboardingReview = {
  id: string; code: string; status: string; type: string; source: string
  name: string; registrationNo: string; location: string; email: string; contactName: string; contactPhone: string
  bank: OnboardingBank; categories: string[]
  financial: OnboardingFinancialView | null; answers: OnboardingAnswerView[]
  documents: OnboardingDocumentDto[]; rounds: OnboardingRound[]
  duplicateWarning: string | null; submittedUtc: string | null; decisionUtc: string | null
  promotedVendorId: string | null; rejectReason: string | null
}
export type OnboardingApproveResult = { vendorId: string; vendorCode: string; duplicateWarning: string | null }
export type ClarificationItemInput = { topic: string; request: string }

export const getOnboardingApplications = () => http<OnboardingQueueItem[]>('/onboarding/applications')
export const getOnboardingApplication = (id: string) => http<OnboardingReview>(`/onboarding/applications/${id}`)
export const startOnboardingReview = (id: string) =>
  http<OnboardingReview>(`/onboarding/applications/${id}/start-review`, { method: 'POST' })
export const clarifyOnboarding = (id: string, message: string, items: ClarificationItemInput[]) =>
  http<OnboardingReview>(`/onboarding/applications/${id}/clarify`, { method: 'POST', body: JSON.stringify({ message, items }) })
export const approveOnboarding = (id: string) =>
  http<OnboardingApproveResult>(`/onboarding/applications/${id}/approve`, { method: 'POST' })
export const rejectOnboarding = (id: string, reason: string) =>
  http<OnboardingReview>(`/onboarding/applications/${id}/reject`, { method: 'POST', body: JSON.stringify({ reason }) })
export const resubmitOnboarding = (token: string, responses: string[]) =>
  http<OnboardingApplication>('/onboarding/draft/resubmit', { method: 'POST', body: JSON.stringify({ token, responses }) })
export type CreateManualVendor = {
  name: string; registrationNo: string; type: string
  region?: string; state?: string; city?: string; country?: string
  currency?: string; paymentTerms?: string; bank?: string; accountNo?: string; swift?: string
  contactName?: string; contactEmail?: string; addressLine?: string
  registeredName?: string; taxId?: string; categories?: string[]
}
export const createManualVendor = (body: CreateManualVendor) =>
  http<{ vendor: VendorDetail; duplicateWarning: string | null }>('/vendors/manual', { method: 'POST', body: JSON.stringify(body) })

// --- Custom Lists (NetSuite-style conformed dimensions: country/state/city/currency/payment/bank) ---
export type CustomListValue = { id: string; code: string; label: string; parentValueCode: string | null; sort: number; active: boolean }
export type CustomList = { id: string; code: string; name: string; description: string | null; parentListCode: string | null; isSystem: boolean; values: CustomListValue[] }
export const getCustomLists = () => http<CustomList[]>('/custom-lists')
export const createCustomList = (body: { code: string; name: string; description?: string | null; parentListCode?: string | null }) =>
  http<CustomList>('/custom-lists', { method: 'POST', body: JSON.stringify(body) })
export const addCustomListValue = (listCode: string, body: { code: string; label: string; parentValueCode?: string | null }) =>
  http<CustomListValue>(`/custom-lists/${encodeURIComponent(listCode)}/values`, { method: 'POST', body: JSON.stringify(body) })
export const updateCustomListValue = (id: string, body: { label: string; parentValueCode?: string | null; sort: number; active: boolean }) =>
  http<CustomListValue>(`/custom-lists/values/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const deleteCustomListValue = (id: string) =>
  http<void>(`/custom-lists/values/${id}`, { method: 'DELETE' })

// --- Global search (D2 navigation shell; hand-typed like Custom Lists — schema regen rides the next gen:api) ---
export type SearchHit = { type: 'Vendor' | 'Requisition' | 'Rfq' | 'PurchaseOrder' | 'Invoice'; id: string; code: string; title: string }
export const searchGlobal = (q: string) => http<SearchHit[]>(`/search?q=${encodeURIComponent(q)}`)
