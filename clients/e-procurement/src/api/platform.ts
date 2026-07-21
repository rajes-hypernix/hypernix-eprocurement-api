import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery } from "@/api/types";

export type CountryDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
};

export type BankDto = {
  id: string;
  name: string;
  swiftCode?: string | null;
  countryCode: string;
  isActive: boolean;
};

export type OrgUnitDto = {
  id: string;
  code: string;
  name: string;
  type: string;
  parentId?: string | null;
  isActive: boolean;
};

export type OrgCatalogDto = {
  units: OrgUnitDto[];
};

export type CityLookupDto = { id: string; name: string };
export type StateLookupDto = {
  id: string;
  code: string;
  name: string;
  cities: CityLookupDto[];
};
export type CountryLookupDto = {
  id: string;
  code: string;
  name: string;
  states: StateLookupDto[];
};
export type GeoCatalogDto = {
  countries: CountryLookupDto[];
  banks: BankDto[];
};

export type CustomListDto = {
  id: string;
  key: string;
  name: string;
  isActive: boolean;
};

export type FormTemplateListItemDto = {
  id: string;
  key: string;
  name: string;
  isActive: boolean;
  questionCount: number;
};

const ROOT = ApiPaths.platform;

export function listCountries(): Promise<CountryDto[]> {
  return apiFetch<CountryDto[]>(`${ROOT}/countries`);
}

export function listBanks(countryCode?: string): Promise<BankDto[]> {
  return apiFetch<BankDto[]>(`${ROOT}/banks${toQuery({ countryCode })}`);
}

export function getGeoCatalog(): Promise<GeoCatalogDto> {
  return apiFetch<GeoCatalogDto>(`${ROOT}/catalog/geo`);
}

export function getOrgCatalog(): Promise<OrgCatalogDto> {
  return apiFetch<OrgCatalogDto>(`${ROOT}/org-catalog`);
}

export function listOrgUnits(): Promise<OrgUnitDto[]> {
  return apiFetch<OrgUnitDto[]>(`${ROOT}/org-units`);
}

export function listCustomLists(): Promise<CustomListDto[]> {
  return apiFetch<CustomListDto[]>(`${ROOT}/custom-lists`);
}

export function listFormTemplates(): Promise<FormTemplateListItemDto[]> {
  return apiFetch<FormTemplateListItemDto[]>(`${ROOT}/form-templates`);
}
