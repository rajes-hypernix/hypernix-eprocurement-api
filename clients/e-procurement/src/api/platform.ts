import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery } from "@/api/types";

export type CountryDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  createdOnUtc: string;
};

export type StateDto = {
  id: string;
  countryId: string;
  code: string;
  name: string;
  isActive: boolean;
  createdOnUtc: string;
};

export type CityDto = {
  id: string;
  stateId: string;
  name: string;
  isActive: boolean;
  createdOnUtc: string;
};

export type BankDto = {
  id: string;
  name: string;
  swiftCode?: string | null;
  countryCode: string;
  isActive: boolean;
  /** Present for newest-first sort only — not a substitute for History. */
  createdOnUtc: string;
};

export type OrgUnitDto = {
  id: string;
  code: string;
  name: string;
  type: string;
  parentId?: string | null;
  isActive: boolean;
  createdOnUtc: string;
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
  createdOnUtc: string;
};

export type CustomListItemDto = {
  id: string;
  listId: string;
  code: string;
  label: string;
  sortOrder: number;
  isActive: boolean;
  createdOnUtc: string;
};

/** Reason-code list keys used by Sourcing governance actions. */
export const CustomListKeys = {
  rfqRescind: "RFQRescind",
  rfqExtend: "RFQExtend",
  bidDecline: "BidDecline",
} as const;

export type FormTemplateListItemDto = {
  id: string;
  key: string;
  name: string;
  isActive: boolean;
  questionCount: number;
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

export type CreateFormTemplateQuestionDto = {
  order: number;
  label: string;
  type: string;
  required: boolean;
  configJson?: string | null;
  help?: string | null;
};

const ROOT = ApiPaths.platform;

export function listCountries(activeOnly = true): Promise<CountryDto[]> {
  return apiFetch<CountryDto[]>(`${ROOT}/countries${toQuery({ activeOnly })}`);
}

export function listStates(countryId: string, activeOnly = true): Promise<StateDto[]> {
  return apiFetch<StateDto[]>(
    `${ROOT}/countries/${encodeURIComponent(countryId)}/states${toQuery({ activeOnly })}`,
  );
}

export function listCities(stateId: string, activeOnly = true): Promise<CityDto[]> {
  return apiFetch<CityDto[]>(
    `${ROOT}/states/${encodeURIComponent(stateId)}/cities${toQuery({ activeOnly })}`,
  );
}

export function createCountry(input: { code: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/countries`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function setCountryActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/countries/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function deleteCountry(id: string): Promise<void> {
  return apiFetch<void>(`${ROOT}/countries/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function updateCountry(id: string, input: { code: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/countries/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function createState(input: { countryId: string; code: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/states`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateState(id: string, input: { code: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/states/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setStateActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/states/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function deleteState(id: string): Promise<void> {
  return apiFetch<void>(`${ROOT}/states/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function createCity(input: { stateId: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/cities`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateCity(id: string, input: { name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/cities/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setCityActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/cities/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function deleteCity(id: string): Promise<void> {
  return apiFetch<void>(`${ROOT}/cities/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function listBanks(countryCode?: string, activeOnly = true): Promise<BankDto[]> {
  return apiFetch<BankDto[]>(`${ROOT}/banks${toQuery({ countryCode, activeOnly })}`);
}

export function createBank(input: {
  name: string;
  countryCode: string;
  swiftCode?: string;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/banks`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateBank(
  id: string,
  input: { name: string; countryCode: string; swiftCode?: string },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/banks/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setBankActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/banks/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function deleteBank(id: string): Promise<void> {
  return apiFetch<void>(`${ROOT}/banks/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function getGeoCatalog(): Promise<GeoCatalogDto> {
  return apiFetch<GeoCatalogDto>(`${ROOT}/catalog/geo`);
}

export function getOrgCatalog(): Promise<OrgCatalogDto> {
  return apiFetch<OrgCatalogDto>(`${ROOT}/org-catalog`);
}

export function listOrgUnits(type?: string, activeOnly = true): Promise<OrgUnitDto[]> {
  return apiFetch<OrgUnitDto[]>(`${ROOT}/org-units${toQuery({ type, activeOnly })}`);
}

export function createOrgUnit(input: {
  code: string;
  name: string;
  type: string;
  parentId?: string | null;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/org-units`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function setOrgUnitActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/org-units/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function listCustomLists(activeOnly = true): Promise<CustomListDto[]> {
  return apiFetch<CustomListDto[]>(`${ROOT}/custom-lists${toQuery({ activeOnly })}`);
}

export function listCustomListItems(listKey: string, activeOnly = true): Promise<CustomListItemDto[]> {
  return apiFetch<CustomListItemDto[]>(
    `${ROOT}/custom-lists/${encodeURIComponent(listKey)}/items${toQuery({ activeOnly })}`,
  );
}

export function createCustomList(input: { key: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-lists`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateCustomList(id: string, input: { name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-lists/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setCustomListActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-lists/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function upsertCustomListItem(
  listKey: string,
  input: { code: string; label: string; sortOrder?: number; isActive?: boolean },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-lists/${encodeURIComponent(listKey)}/items`, {
    method: "PUT",
    body: JSON.stringify({
      code: input.code,
      label: input.label,
      sortOrder: input.sortOrder ?? 0,
      isActive: input.isActive ?? true,
    }),
  });
}

export function listFormTemplates(activeOnly = true): Promise<FormTemplateListItemDto[]> {
  return apiFetch<FormTemplateListItemDto[]>(`${ROOT}/form-templates${toQuery({ activeOnly })}`);
}

export function getFormTemplate(id: string): Promise<FormTemplateDto | null> {
  return apiFetch<FormTemplateDto | null>(`${ROOT}/form-templates/${encodeURIComponent(id)}`);
}

export function createFormTemplate(input: {
  key: string;
  name: string;
  questions: CreateFormTemplateQuestionDto[];
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/form-templates`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateFormTemplate(
  id: string,
  input: { name: string; questions: CreateFormTemplateQuestionDto[] },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/form-templates/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setFormTemplateActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/form-templates/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

// ---------------------------------------------------------------------------
// Custom Fields (Phase 5/6)
// ---------------------------------------------------------------------------

export type CustomFieldDefDto = {
  id: string;
  code: string;
  label: string;
  dataType: string;
  refEntity?: string | null;
  listKey?: string | null;
  scope: string;
  displayType: string;
  showInList: boolean;
  isRequired: boolean;
  helpText?: string | null;
  isActive: boolean;
  appliesTo: string[];
};

export type CustomFieldValueDto = {
  customFieldDefId: string;
  code: string;
  label: string;
  dataType: string;
  displayType: string;
  isRequired: boolean;
  listKey?: string | null;
  refEntity?: string | null;
  lineId?: string | null;
  valueText?: string | null;
  valueNumber?: number | null;
  valueDate?: string | null;
  valueDateTime?: string | null;
  valueBool?: boolean | null;
  valueListCode?: string | null;
  valueRefId?: string | null;
  valueLabel?: string | null;
};

export type CustomFieldValueInput = {
  customFieldDefId: string;
  lineId?: string | null;
  valueText?: string | null;
  valueNumber?: number | null;
  valueDate?: string | null;
  valueDateTime?: string | null;
  valueBool?: boolean | null;
  valueListCode?: string | null;
  valueRefId?: string | null;
  valueLabel?: string | null;
};

export function listCustomFieldDefs(recordType?: string): Promise<CustomFieldDefDto[]> {
  return apiFetch<CustomFieldDefDto[]>(`${ROOT}/custom-fields${toQuery({ recordType })}`);
}

export function createCustomFieldDef(input: {
  code: string;
  label: string;
  dataType: string;
  scope: string;
  refEntity?: string | null;
  listKey?: string | null;
  displayType?: string;
  showInList?: boolean;
  isRequired?: boolean;
  helpText?: string | null;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-fields`, { method: "POST", body: JSON.stringify(input) });
}

export function updateCustomFieldDef(
  id: string,
  input: { label: string; displayType: string; showInList: boolean; isRequired: boolean; helpText?: string | null },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-fields/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(input) });
}

export function setCustomFieldDefActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-fields/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function applyCustomFieldToRecordType(id: string, recordType: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-fields/${encodeURIComponent(id)}/apply/${encodeURIComponent(recordType)}`, {
    method: "POST",
  });
}

export function removeCustomFieldApplication(id: string, recordType: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/custom-fields/${encodeURIComponent(id)}/apply/${encodeURIComponent(recordType)}`, {
    method: "DELETE",
  });
}

export function getCustomFieldValues(recordType: string, recordId: string): Promise<CustomFieldValueDto[]> {
  return apiFetch<CustomFieldValueDto[]>(`${ROOT}/custom-fields/values/${recordType}/${encodeURIComponent(recordId)}`);
}

export function setCustomFieldValues(recordType: string, recordId: string, values: CustomFieldValueInput[]): Promise<void> {
  return apiFetch<void>(`${ROOT}/custom-fields/values/${recordType}/${encodeURIComponent(recordId)}`, {
    method: "PUT",
    body: JSON.stringify(values),
  });
}

// ---------------------------------------------------------------------------
// Segments (Phase 5/6)
// ---------------------------------------------------------------------------

export type SegmentAssignmentDto = {
  id: string;
  dimension: string;
  orgUnitId: string;
  orgUnitCode: string;
  orgUnitName: string;
  lineId?: string | null;
};

export function listSegmentAssignments(recordType: string, recordId: string): Promise<SegmentAssignmentDto[]> {
  return apiFetch<SegmentAssignmentDto[]>(`${ROOT}/segments/assignments/${recordType}/${encodeURIComponent(recordId)}`);
}

export function setSegmentAssignment(
  recordType: string,
  recordId: string,
  input: { lineId?: string | null; dimension: string; orgUnitId: string },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/segments/assignments/${recordType}/${encodeURIComponent(recordId)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function removeSegmentAssignment(
  recordType: string,
  recordId: string,
  dimension: string,
  lineId?: string | null,
): Promise<void> {
  return apiFetch<void>(
    `${ROOT}/segments/assignments/${recordType}/${encodeURIComponent(recordId)}/${dimension}${toQuery({ lineId })}`,
    { method: "DELETE" },
  );
}

// ---------------------------------------------------------------------------
// Entry Forms (Phase 5/6)
// ---------------------------------------------------------------------------

export type EntryFormListItemDto = { id: string; code: string; name: string; recordType: string; isSystem: boolean; isActive: boolean };
export type EntryFormGroupDto = { id: string; title: string; sort: number };
export type EntryFormFieldDto = { id: string; fieldKey: string; groupId: string; sort: number; requiredOnForm: boolean; fullWidth: boolean };
export type EntryFormDetailDto = EntryFormListItemDto & { groups: EntryFormGroupDto[]; fields: EntryFormFieldDto[] };
export type EntryFormGroupInputDto = { title: string; sort: number };
export type EntryFormFieldInputDto = { fieldKey: string; groupIndex: number; sort: number; requiredOnForm: boolean; fullWidth: boolean };
export type ResolvedFormFieldDto = {
  fieldKey: string;
  groupTitle: string;
  groupSort: number;
  sort: number;
  requiredOnForm: boolean;
  fullWidth: boolean;
  label: string;
  dataType: string;
  listKey?: string | null;
  refEntity?: string | null;
  isCustomField: boolean;
  customFieldDefId?: string | null;
};
export type ResolvedEntryFormDto = { entryFormDefId: string; code: string; name: string; fields: ResolvedFormFieldDto[] };
export type EntryFormRoleMapDto = { id: string; recordType: string; role: string; entryFormDefId: string; entryFormCode: string };

export function listEntryForms(recordType?: string): Promise<EntryFormListItemDto[]> {
  return apiFetch<EntryFormListItemDto[]>(`${ROOT}/entry-forms${toQuery({ recordType })}`);
}

export function getEntryForm(id: string): Promise<EntryFormDetailDto> {
  return apiFetch<EntryFormDetailDto>(`${ROOT}/entry-forms/${encodeURIComponent(id)}`);
}

export function createEntryForm(input: { code: string; name: string; recordType: string; isSystem?: boolean }): Promise<string> {
  return apiFetch<string>(`${ROOT}/entry-forms`, { method: "POST", body: JSON.stringify(input) });
}

export function updateEntryFormDetails(id: string, name: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/entry-forms/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify({ name }) });
}

export function setEntryFormActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/entry-forms/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function replaceEntryFormLayout(
  id: string,
  groups: EntryFormGroupInputDto[],
  fields: EntryFormFieldInputDto[],
): Promise<string> {
  return apiFetch<string>(`${ROOT}/entry-forms/${encodeURIComponent(id)}/layout`, {
    method: "PUT",
    body: JSON.stringify({ groups, fields }),
  });
}

export function upsertEntryFormRoleMap(recordType: string, role: string, entryFormDefId: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/entry-forms/role-maps`, {
    method: "PUT",
    body: JSON.stringify({ recordType, role, entryFormDefId }),
  });
}

export function listEntryFormRoleMaps(recordType?: string): Promise<EntryFormRoleMapDto[]> {
  return apiFetch<EntryFormRoleMapDto[]>(`${ROOT}/entry-forms/role-maps${toQuery({ recordType })}`);
}

export function getEntryFormForRole(recordType: string, role: string): Promise<ResolvedEntryFormDto> {
  return apiFetch<ResolvedEntryFormDto>(`${ROOT}/entry-forms/resolve/${recordType}/${encodeURIComponent(role)}`);
}
