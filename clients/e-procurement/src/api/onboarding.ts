import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery } from "@/api/types";
import type {
  VendorAddressDto,
  VendorBankAccountDto,
  VendorCertificationDto,
  VendorContactDto,
  SwecCategoryDto,
} from "@/api/suppliers";
import type { BankDto, CountryLookupDto } from "@/api/platform";

const ROOT = `${ApiPaths.suppliers}/onboarding`;

export type OnboardingInvitationDto = {
  id: string;
  email: string;
  type: string;
  status: string;
  invitedByName: string;
  createdUtc: string;
  expiresUtc: string;
  applicationId?: string | null;
  applicationCode?: string | null;
  magicLink?: string | null;
  selectedTemplateIds: string[];
};

export type OnboardingQueueItemDto = {
  id: string;
  code: string;
  name: string;
  type: string;
  status: string;
  source: string;
  createdUtc: string;
  submittedUtc?: string | null;
  openRoundNo?: number | null;
  roundCount: number;
  invitationId?: string | null;
};

export type OnboardingRoundItemDto = { topic: string; request: string; response: string };
export type OnboardingRoundDto = {
  roundNo: number;
  direction: string;
  status: string;
  message: string;
  raisedByName: string;
  raisedUtc: string;
  respondedUtc?: string | null;
  items: OnboardingRoundItemDto[];
};

export type OnboardingApplicationDto = {
  id: string;
  code: string;
  status: string;
  type: string;
  name: string;
  email: string;
  createdUtc: string;
  submittedUtc?: string | null;
  selectedTemplateIds: string[];
  rounds: OnboardingRoundDto[];
};

export type OnboardingDocumentDto = { key: string; fileName: string; uploadedUtc: string };
export type OnboardingAnswerDto = { formTemplateId: string; questionOrder: number; value: string };

export type OnboardingFinancialYearDto = {
  yearIndex: number;
  revenue: number;
  netProfit: number;
  ebit: number;
  totalAssets: number;
  currentAssets: number;
  inventory: number;
  currentLiabilities: number;
  totalLiabilities: number;
  equity: number;
  retainedEarnings: number;
  fixedAssets: number;
};

export type OnboardingFinancialYearCalcDto = {
  yearIndex: number;
  x1: number;
  x2: number;
  x3: number;
  x4: number;
  x5: number;
  z: number;
};

export type OnboardingFinancialViewDto = {
  band: string;
  risk: string;
  zone: string;
  weightedZ: number;
  score: number;
  statement: string;
  years: OnboardingFinancialYearCalcDto[];
};

export type OnboardingDraftDto = {
  id: string;
  code: string;
  status: string;
  type: string;
  name: string;
  registeredName: string;
  registrationNo: string;
  taxId: string;
  email: string;
  contactName: string;
  contactPhone: string;
  region: string;
  state: string;
  city: string;
  country: string;
  categories: string[];
  contacts: VendorContactDto[];
  addresses: VendorAddressDto[];
  bankAccounts: VendorBankAccountDto[];
  certifications: VendorCertificationDto[];
  financialYears: OnboardingFinancialYearDto[];
  documents: OnboardingDocumentDto[];
  selectedTemplateIds: string[];
  answers: OnboardingAnswerDto[];
};

export type OnboardingReviewDto = {
  id: string;
  code: string;
  status: string;
  type: string;
  source: string;
  name: string;
  registeredName: string;
  registrationNo: string;
  taxId: string;
  email: string;
  contactName: string;
  contactPhone: string;
  region: string;
  state: string;
  city: string;
  country: string;
  categories: string[];
  contacts: VendorContactDto[];
  addresses: VendorAddressDto[];
  bankAccounts: VendorBankAccountDto[];
  certifications: VendorCertificationDto[];
  financial?: OnboardingFinancialViewDto | null;
  documents: OnboardingDocumentDto[];
  rounds: OnboardingRoundDto[];
  answers: OnboardingAnswerDto[];
  duplicateWarning?: string | null;
  createdUtc: string;
  submittedUtc?: string | null;
  decisionUtc?: string | null;
};

export type FormTemplateQuestionDto = {
  id: string;
  order: number;
  label: string;
  type: string;
  required: boolean;
  configJson?: string | null;
  help?: string | null;
};

export type FormTemplateDto = {
  id: string;
  key: string;
  name: string;
  isActive: boolean;
  questions: FormTemplateQuestionDto[];
};

export type OnboardingLookupsDto = {
  swec: SwecCategoryDto[];
  countries: CountryLookupDto[];
  banks: BankDto[];
  formTemplates: FormTemplateDto[];
};

export type OnboardingApproveResultDto = {
  vendorId: string;
  vendorCode: string;
  duplicateWarning?: string | null;
};

export type SaveOnboardingDraftRequest = {
  token: string;
  name?: string | null;
  registeredName?: string | null;
  registrationNo?: string | null;
  taxId?: string | null;
  email?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  region?: string | null;
  state?: string | null;
  city?: string | null;
  country?: string | null;
  categories?: string[] | null;
  contacts?: VendorContactDto[] | null;
  addresses?: VendorAddressDto[] | null;
  bankAccounts?: VendorBankAccountDto[] | null;
  certifications?: VendorCertificationDto[] | null;
  financialYears?: OnboardingFinancialYearDto[] | null;
  answers?: OnboardingAnswerDto[] | null;
};

const anon = { skipAuth: true as const };

export function listOnboardingApplications(): Promise<OnboardingQueueItemDto[]> {
  return apiFetch(`${ROOT}/applications`);
}

export function createOnboardingInvitation(body: {
  email?: string | null;
  type: string;
  selectedTemplateIds?: string[] | null;
}): Promise<OnboardingInvitationDto> {
  return apiFetch(`${ROOT}/invitations`, { method: "POST", body: JSON.stringify(body) });
}

export function resendOnboardingInvitation(invitationId: string): Promise<OnboardingInvitationDto> {
  return apiFetch(`${ROOT}/invitations/${invitationId}/resend`, { method: "POST" });
}

export function revokeOnboardingInvitation(invitationId: string): Promise<string> {
  return apiFetch(`${ROOT}/invitations/${invitationId}/revoke`, { method: "POST" });
}

export function getOnboardingApplication(applicationId: string): Promise<OnboardingReviewDto> {
  return apiFetch(`${ROOT}/applications/${applicationId}`);
}

export function startOnboardingReview(applicationId: string): Promise<OnboardingApplicationDto> {
  return apiFetch(`${ROOT}/applications/${applicationId}/start-review`, { method: "POST" });
}

export function clarifyOnboarding(
  applicationId: string,
  message: string,
  items: { topic: string; request: string }[],
): Promise<OnboardingApplicationDto> {
  return apiFetch(`${ROOT}/applications/${applicationId}/clarify`, {
    method: "POST",
    body: JSON.stringify({ message, items }),
  });
}

export function approveOnboarding(applicationId: string): Promise<OnboardingApproveResultDto> {
  return apiFetch(`${ROOT}/applications/${applicationId}/approve`, { method: "POST" });
}

export function rejectOnboarding(applicationId: string, reason: string): Promise<OnboardingApplicationDto> {
  return apiFetch(`${ROOT}/applications/${applicationId}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

export function resolveOnboardingLink(token: string): Promise<OnboardingApplicationDto> {
  return apiFetch(`${ROOT}/resolve`, { method: "POST", body: JSON.stringify({ token }), ...anon });
}

export function getOnboardingLookups(token: string): Promise<OnboardingLookupsDto> {
  return apiFetch(`${ROOT}/lookups`, { method: "POST", body: JSON.stringify({ token }), ...anon });
}

export function getOnboardingDraft(token: string): Promise<OnboardingDraftDto> {
  return apiFetch(`${ROOT}/draft${toQuery({ token })}`, anon);
}

export function saveOnboardingDraft(body: SaveOnboardingDraftRequest): Promise<OnboardingDraftDto> {
  return apiFetch(`${ROOT}/draft`, { method: "PUT", body: JSON.stringify(body), ...anon });
}

export function submitOnboardingDraft(token: string): Promise<OnboardingDraftDto> {
  return apiFetch(`${ROOT}/draft/submit`, { method: "POST", body: JSON.stringify({ token }), ...anon });
}

export function resubmitOnboardingDraft(token: string, responses: string[]): Promise<OnboardingApplicationDto> {
  return apiFetch(`${ROOT}/draft/resubmit`, {
    method: "POST",
    body: JSON.stringify({ token, responses }),
    ...anon,
  });
}

export function uploadOnboardingDocument(
  token: string,
  key: string,
  file: File,
): Promise<OnboardingDocumentDto> {
  const form = new FormData();
  form.append("file", file);
  return apiFetch(`${ROOT}/draft/documents${toQuery({ token, key })}`, {
    method: "POST",
    body: form,
    ...anon,
  });
}

export function deleteOnboardingDocument(token: string, key: string): Promise<void> {
  return apiFetch(`${ROOT}/draft/documents/${encodeURIComponent(key)}${toQuery({ token })}`, {
    method: "DELETE",
    ...anon,
  });
}
