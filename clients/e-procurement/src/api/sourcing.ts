import { apiFetch } from "@/lib/api-client";
import { ApiPaths } from "@/api/types";

const ROOT = ApiPaths.sourcing;

// ---------------------------------------------------------------------------
// Requisitions
// ---------------------------------------------------------------------------

export type PrLineDto = {
  id: string;
  itemCode: string;
  description: string;
  qty: number;
  uom: string;
  estUnitPrice: number;
  lifecycleStatus: string;
  ref?: string | null;
};

export type PrLineInput = {
  itemCode: string;
  description: string;
  qty: number;
  uom: string;
  estUnitPrice: number;
};

export type RequisitionDto = {
  id: string;
  code: string;
  requestor: string;
  department: string;
  departmentCode?: string | null;
  location: string;
  locationCode?: string | null;
  category: string;
  categoryCode?: string | null;
  job: string;
  jobCode?: string | null;
  memo: string;
  costCentre: string;
  project?: string | null;
  entryFormId?: string | null;
  raisedOn?: string | null;
  requiredOn?: string | null;
  submitted: boolean;
  submittedUtc?: string | null;
  headerStatus: string;
  currency: string;
  lines: PrLineDto[];
  createdUtc: string;
  updatedUtc: string;
};

export type RequisitionListItemDto = {
  id: string;
  code: string;
  requestor: string;
  department: string;
  headerStatus: string;
  submitted: boolean;
  lineCount: number;
  createdUtc: string;
};

export type CreateRequisitionRequest = {
  requestor: string;
  department: string;
  departmentCode?: string | null;
  location: string;
  locationCode?: string | null;
  category: string;
  categoryCode?: string | null;
  job: string;
  jobCode?: string | null;
  memo: string;
  costCentre: string;
  project?: string | null;
  entryFormId?: string | null;
  raisedOn?: string | null;
  requiredOn?: string | null;
  currency: string;
  lines: PrLineInput[];
  submit?: boolean;
};

export type UpdateRequisitionRequest = {
  department: string;
  departmentCode?: string | null;
  location: string;
  locationCode?: string | null;
  category: string;
  categoryCode?: string | null;
  job: string;
  jobCode?: string | null;
  memo: string;
  costCentre: string;
  project?: string | null;
  requiredOn?: string | null;
};

export function listRequisitions(): Promise<RequisitionListItemDto[]> {
  return apiFetch(`${ROOT}/requisitions`);
}

export function getRequisition(id: string): Promise<RequisitionDto> {
  return apiFetch(`${ROOT}/requisitions/${id}`);
}

export function createRequisition(body: CreateRequisitionRequest): Promise<string> {
  return apiFetch(`${ROOT}/requisitions`, { method: "POST", body: JSON.stringify(body) });
}

export function updateRequisition(id: string, body: UpdateRequisitionRequest): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${id}`, { method: "PUT", body: JSON.stringify(body) });
}

export function submitRequisition(id: string): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${id}/submit`, { method: "POST" });
}

export function cancelRequisition(id: string): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${id}/cancel`, { method: "POST" });
}

export function cancelRequisitionLine(reqId: string, lineId: string, reason?: string | null): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${reqId}/lines/${lineId}/cancel`, {
    method: "POST",
    body: JSON.stringify({ reason: reason ?? null }),
  });
}

export function releaseRequisitionLine(reqId: string, lineId: string, reason?: string | null): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${reqId}/lines/${lineId}/release`, {
    method: "POST",
    body: JSON.stringify({ reason: reason ?? null }),
  });
}

export function reserveRequisitionLine(reqId: string, lineId: string): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${reqId}/lines/${lineId}/reserve`, { method: "POST" });
}

export function unreserveRequisitionLine(reqId: string, lineId: string): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${reqId}/lines/${lineId}/unreserve`, { method: "POST" });
}

export function reopenRequisitionLine(reqId: string, lineId: string): Promise<string> {
  return apiFetch(`${ROOT}/requisitions/${reqId}/lines/${lineId}/reopen`, { method: "POST" });
}

export type EligibleOrderLineDto = {
  prId: string;
  prCode: string;
  prLineId: string;
  itemCode: string;
  description: string;
  qty: number;
  uom: string;
  estUnitPrice: number;
  qtyOrdered: number;
  qtyRemaining: number;
  sourceable: boolean;
  ineligibleReason?: string | null;
};

export function getEligibleRequisitionLinesForOrdering(): Promise<EligibleOrderLineDto[]> {
  return apiFetch(`${ROOT}/requisitions/order-builder/eligible-lines`);
}

// ---------------------------------------------------------------------------
// RFQs
// ---------------------------------------------------------------------------

export type RfqLineDto = {
  lineCode: string;
  itemCode: string;
  description: string;
  qty: number;
  uom: string;
  prRef?: string | null;
  sourcePrLineIds: string[];
};

export type RfqLineInput = {
  lineCode: string;
  itemCode: string;
  description: string;
  qty: number;
  uom: string;
  prRef?: string | null;
  sourcePrLineIds?: string[] | null;
};

export type FormItemDto = {
  kind: string;
  group: string;
  section: string;
  label: string;
  type: string;
  required: boolean;
  configJson?: string | null;
  help?: string | null;
  order: number;
};

export type RfqInvitationDto = {
  id: string;
  vendorId: string;
  vendorName?: string | null;
  vendorCode?: string | null;
  roundNumber: number;
  status: string;
  declineReasonCode?: string | null;
  declineNote?: string | null;
  rescindReasonCode?: string | null;
  rescindNote?: string | null;
  invitedUtc: string;
  viewedUtc?: string | null;
  respondedUtc?: string | null;
  rescindedUtc?: string | null;
};

export type RfqEventDto = {
  id: string;
  eventType: string;
  vendorId?: string | null;
  actorUserId?: string | null;
  reasonCode?: string | null;
  reasonNote?: string | null;
  oldClosesUtc?: string | null;
  newClosesUtc?: string | null;
  occurredUtc: string;
};

export type RfqDetailDto = {
  id: string;
  code: string;
  title: string;
  envelope: string;
  status: string;
  currency: string;
  ownerUserId?: string | null;
  opensUtc?: string | null;
  closesUtc?: string | null;
  originalClosesUtc?: string | null;
  extensionCount: number;
  releasedUtc?: string | null;
  closedUtc?: string | null;
  prRefs: string[];
  lines: RfqLineDto[];
  formItems: FormItemDto[];
  technicalSections: string[];
  commercialSections: string[];
  technicalEvaluatorIds: string[];
  commercialEvaluatorIds: string[];
  technicalOpened: boolean;
  techFinalized: boolean;
  commercialOpened: boolean;
  invitations: RfqInvitationDto[];
  events: RfqEventDto[];
  createdUtc: string;
  updatedUtc: string;
};

export type RfqListItemDto = {
  id: string;
  code: string;
  title: string;
  envelope: string;
  status: string;
  currency: string;
  closesUtc?: string | null;
  invitedCount: number;
  lineCount: number;
};

export type CreateRfqDraftRequest = {
  title?: string | null;
  envelope: string;
  currency: string;
  prRefs: string[];
  lines: RfqLineInput[];
};

export type UpdateRfqDraftRequest = {
  title: string;
  envelope: string;
  currency: string;
  opensUtc?: string | null;
  closesUtc?: string | null;
  lines: RfqLineInput[];
  formItems: FormItemDto[];
  technicalSections: string[];
  commercialSections: string[];
  technicalEvaluatorIds: string[];
  commercialEvaluatorIds: string[];
};

export function listRfqs(): Promise<RfqListItemDto[]> {
  return apiFetch(`${ROOT}/rfqs`);
}

export function getRfq(id: string): Promise<RfqDetailDto> {
  return apiFetch(`${ROOT}/rfqs/${id}`);
}

export function createRfqDraft(body: CreateRfqDraftRequest): Promise<string> {
  return apiFetch(`${ROOT}/rfqs`, { method: "POST", body: JSON.stringify(body) });
}

export function updateRfqDraft(id: string, body: UpdateRfqDraftRequest): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${id}`, { method: "PUT", body: JSON.stringify(body) });
}

export function releaseRfq(id: string): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${id}/release`, { method: "POST" });
}

export function closeRfq(id: string): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${id}/close`, { method: "POST" });
}

export function cancelRfq(id: string): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${id}/cancel`, { method: "POST" });
}

export function extendRfq(
  id: string,
  newClosesUtc: string,
  reasonCode?: string | null,
  note?: string | null,
): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${id}/extend`, {
    method: "POST",
    body: JSON.stringify({ newClosesUtc, reasonCode: reasonCode ?? null, note: note ?? null }),
  });
}

export function inviteVendor(rfqId: string, vendorId: string): Promise<RfqInvitationDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/invitations`, {
    method: "POST",
    body: JSON.stringify({ vendorId }),
  });
}

export function rescindInvitation(
  rfqId: string,
  vendorId: string,
  reasonCode: string,
  note?: string | null,
): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/invitations/${vendorId}/rescind`, {
    method: "POST",
    body: JSON.stringify({ reasonCode, note: note ?? null }),
  });
}

/** Vendor-side: decline an invitation (bare auth, no permission — vendorId derived from the caller's claim). */
export function declineInvitation(rfqId: string, reasonCode: string, note?: string | null): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/decline`, {
    method: "POST",
    body: JSON.stringify({ reasonCode, note: note ?? null }),
  });
}

// ---------------------------------------------------------------------------
// Bids (vendor portal)
// ---------------------------------------------------------------------------

export type BidLineDto = {
  itemCode: string;
  bidding: boolean;
  price: number;
  qty: number;
  partial: boolean;
  altItem?: string | null;
};

export type BidAnswerDto = { questionOrder: number; value: string };

export type BidDto = {
  id: string;
  code: string;
  rfqId: string;
  vendorId: string;
  submitted: boolean;
  submittedUtc?: string | null;
  savedDraft: boolean;
  withdrawnUtc?: string | null;
  lead?: string | null;
  warranty?: string | null;
  lines: BidLineDto[];
  answers: BidAnswerDto[];
  files: string[];
};

export type MyInvitationDto = {
  rfqId: string;
  rfqCode: string;
  title: string;
  envelope: string;
  rfqStatus: string;
  invitationStatus: string;
  opensUtc?: string | null;
  closesUtc?: string | null;
  hasSubmittedBid: boolean;
};

export type RfqForBiddingDto = {
  rfqId: string;
  code: string;
  title: string;
  envelope: string;
  status: string;
  currency: string;
  opensUtc?: string | null;
  closesUtc?: string | null;
  lines: RfqLineDto[];
  formItems: FormItemDto[];
  technicalSections: string[];
  commercialSections: string[];
};

export function listMyInvitations(): Promise<MyInvitationDto[]> {
  return apiFetch(`${ROOT}/my/rfqs`);
}

/** Vendor-scoped RFQ read for the bid form (lines/questions) — GetRfqById is buyer-only. */
export function getRfqForBidding(rfqId: string): Promise<RfqForBiddingDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/for-bidding`);
}

export function getMyBid(rfqId: string): Promise<BidDto | undefined> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/my-bid`);
}

export function saveBidDraft(
  rfqId: string,
  body: { lead?: string | null; warranty?: string | null; lines: BidLineDto[]; answers: BidAnswerDto[]; files: string[] },
): Promise<BidDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/my-bid`, { method: "PUT", body: JSON.stringify(body) });
}

export function submitBid(rfqId: string): Promise<BidDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/my-bid/submit`, { method: "POST" });
}

export function withdrawBid(rfqId: string): Promise<BidDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/my-bid/withdraw`, { method: "POST" });
}

// ---------------------------------------------------------------------------
// Evaluation
// ---------------------------------------------------------------------------

export type BidOpeningStatusDto = {
  rfqId: string;
  envelope: string;
  rfqStatus: string;
  technicalOpened: boolean;
  techFinalized: boolean;
  commercialOpened: boolean;
  invitedCount: number;
  submittedBidCount: number;
};

export type TechnicalScoreDetailDto = { evaluatorId: string; criterion: string; score: number };

export type TechnicalEvalRowDto = {
  vendorId: string;
  vendorName?: string | null;
  vendorCode?: string | null;
  alias: string;
  masked: boolean;
  committeeScore?: number | null;
  pass: boolean;
  scores: TechnicalScoreDetailDto[];
};

export type TechnicalScoreDto = { vendorId: string; criterion: string; score: number };

export const TECHNICAL_CRITERIA = ["Compliance", "Experience", "Delivery", "QA"] as const;

export function getBidOpening(rfqId: string): Promise<BidOpeningStatusDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/bid-opening`);
}

export function openTechnicalEnvelope(rfqId: string): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/technical-envelope/open`, { method: "POST" });
}

export function openCommercialEnvelope(rfqId: string): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/commercial-envelope/open`, { method: "POST" });
}

export function getTechnicalEval(rfqId: string): Promise<TechnicalEvalRowDto[]> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/technical-eval`);
}

export function setTechnicalScore(
  rfqId: string,
  vendorId: string,
  criterion: string,
  score: number,
): Promise<TechnicalScoreDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/technical-eval/scores`, {
    method: "PUT",
    body: JSON.stringify({ vendorId, criterion, score }),
  });
}

export function finalizeTechnical(rfqId: string): Promise<string> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/technical-eval/finalize`, { method: "POST" });
}

// ---------------------------------------------------------------------------
// Awards
// ---------------------------------------------------------------------------

export type AwardAllocationDto = { rfqLineCode: string; vendorId: string; qty: number; unitPrice: number };

export type AwardDto = {
  id: string;
  code: string;
  rfqId: string;
  status: string;
  createdByUserId: string;
  approverUserId?: string | null;
  approvedUtc?: string | null;
  totalValue: number;
  allocations: AwardAllocationDto[];
  createdUtc: string;
  updatedUtc: string;
};

export type AwardListItemDto = {
  id: string;
  code: string;
  rfqId: string;
  rfqCode: string;
  status: string;
  totalValue: number;
  createdUtc: string;
};

export type AwardEligibilityRowDto = {
  vendorId: string;
  vendorName?: string | null;
  vendorCode?: string | null;
  alias: string;
  masked: boolean;
  eligible: boolean;
  committeeScore?: number | null;
  technicallyPassed: boolean;
};

export function getAwardEligibility(rfqId: string): Promise<AwardEligibilityRowDto[]> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/award-eligibility`);
}

export function getAward(rfqId: string): Promise<AwardDto | undefined> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/award`);
}

export function listAwards(): Promise<AwardListItemDto[]> {
  return apiFetch(`${ROOT}/awards`);
}

export function submitAward(rfqId: string, allocations: AwardAllocationDto[]): Promise<AwardDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/award`, { method: "PUT", body: JSON.stringify({ allocations }) });
}

export function approveAward(rfqId: string): Promise<AwardDto> {
  return apiFetch(`${ROOT}/rfqs/${rfqId}/award/approve`, { method: "POST" });
}

// ---------------------------------------------------------------------------
// Clarifications
// ---------------------------------------------------------------------------

export type ClarificationMessageDto = {
  id: string;
  scope: string;
  vendorId: string;
  senderKind: string;
  senderName: string;
  body: string;
  published: boolean;
  createdUtc: string;
  readByBuyer: boolean;
  readByVendor: boolean;
};

export type ClarificationThreadDto = {
  scope: string;
  vendorId: string;
  vendorName?: string | null;
  vendorCode?: string | null;
  messageCount: number;
  lastMessageUtc: string;
  hasUnread: boolean;
};

export function listClarificationThreads(): Promise<ClarificationThreadDto[]> {
  return apiFetch(`${ROOT}/clarifications/threads`);
}

export function getClarificationThread(scope: string, vendorId: string): Promise<ClarificationMessageDto[]> {
  return apiFetch(`${ROOT}/clarifications/threads/${encodeURIComponent(scope)}/${vendorId}`);
}

export function sendClarification(
  scope: string,
  vendorId: string | null,
  body: string,
  published: boolean,
): Promise<ClarificationMessageDto[]> {
  return apiFetch(`${ROOT}/clarifications`, {
    method: "POST",
    body: JSON.stringify({ scope, vendorId, body, published }),
  });
}
