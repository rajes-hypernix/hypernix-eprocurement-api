import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery, type PagedResponse } from "@/api/types";

export type VendorListItemDto = {
  id: string;
  code: string;
  name: string;
  type: string;
  categories: string[];
  region: string;
  state: string;
  rating: number;
  status: string;
};

export type VendorContactDto = {
  name: string;
  role: string;
  email: string;
  phone: string;
  isPrimary: boolean;
};

export type VendorAddressDto = {
  type: string;
  line: string;
  city: string;
  state: string;
  country: string;
  postcode: string;
  isPrimary: boolean;
};

export type VendorBankAccountDto = {
  bank: string;
  accountNo: string;
  swift: string;
  currency: string;
  isPrimary: boolean;
};

export type VendorCertificationDto = {
  name: string;
  number: string;
  validTo: string;
  status: string;
};

export type VendorCurrencyDto = {
  code: string;
  isPrimary: boolean;
};

export type VendorDto = {
  id: string;
  code: string;
  name: string;
  registeredName: string;
  registrationNo: string;
  taxId: string;
  type: string;
  llrcTier?: string | null;
  status: string;
  region: string;
  state: string;
  city: string;
  country: string;
  rating: number;
  paymentTerms: string;
  creditLimit: number;
  categories: string[];
  contacts: VendorContactDto[];
  addresses: VendorAddressDto[];
  bankAccounts: VendorBankAccountDto[];
  certifications: VendorCertificationDto[];
  currencies: VendorCurrencyDto[];
  createdUtc: string;
  updatedUtc: string;
};

export type CreateManualVendorRequest = {
  name: string;
  registrationNo: string;
  type: string;
  region?: string | null;
  state?: string | null;
  city?: string | null;
  country?: string | null;
  currency?: string | null;
  paymentTerms?: string | null;
  bank?: string | null;
  accountNo?: string | null;
  swift?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  addressLine?: string | null;
  registeredName?: string | null;
  taxId?: string | null;
  categories?: string[] | null;
};

export type CreateManualVendorResult = {
  vendorId: string;
  code: string;
  duplicateWarning?: string | null;
};

export type SwecCategoryDto = {
  code: string;
  name: string;
  parentCode?: string | null;
  level: number;
  isLeaf: boolean;
  pathText: string;
};

const ROOT = ApiPaths.suppliers;

export function searchVendors(
  params: {
    search?: string;
    pageNumber?: number;
    pageSize?: number;
    sortBy?: string;
    sortDir?: string;
  } = {},
): Promise<PagedResponse<VendorListItemDto>> {
  return apiFetch<PagedResponse<VendorListItemDto>>(
    `${ROOT}/vendors${toQuery({
      search: params.search,
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 20,
      sortBy: params.sortBy,
      sortDir: params.sortDir,
    })}`,
  );
}

export function getVendor(vendorId: string): Promise<VendorDto> {
  return apiFetch<VendorDto>(`${ROOT}/vendors/${vendorId}`);
}

export function createManualVendor(body: CreateManualVendorRequest): Promise<CreateManualVendorResult> {
  return apiFetch<CreateManualVendorResult>(`${ROOT}/vendors/manual`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function setVendorCategories(vendorId: string, categories: string[]): Promise<string> {
  return apiFetch<string>(`${ROOT}/vendors/${vendorId}/categories`, {
    method: "PUT",
    body: JSON.stringify({ categories }),
  });
}

export function toggleVendorStatus(vendorId: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/vendors/${vendorId}/toggle-status`, {
    method: "POST",
  });
}

export function listSwecCategories(): Promise<SwecCategoryDto[]> {
  return apiFetch<SwecCategoryDto[]>(`${ROOT}/swec`);
}
