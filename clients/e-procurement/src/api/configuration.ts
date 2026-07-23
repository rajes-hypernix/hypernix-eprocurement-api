import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery } from "@/api/types";

const ROOT = ApiPaths.platform;

export type SettingDto = {
  id: string;
  key: string;
  value: string;
  valueKind: string;
  label: string;
  description?: string | null;
};

export type CurrencyDto = {
  id: string;
  code: string;
  name: string;
  symbol: string;
  decimals: number;
  isActive: boolean;
};

export type ExchangeRateCurrentDto = {
  currencyCode: string;
  currencyName: string;
  rateToBase?: number | null;
  effectiveDate?: string | null;
  isBaseCurrency: boolean;
};

export type ExchangeRateDto = {
  id: string;
  currencyCode: string;
  rateToBase: number;
  effectiveDate: string;
  enteredByUserId: string;
  createdOnUtc: string;
};

export type TaxCodeDto = {
  id: string;
  code: string;
  name: string;
  ratePct: number;
  isActive: boolean;
};

export type IncotermDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
};

export type ItemDto = {
  id: string;
  itemCode: string;
  description: string;
  uom: string;
  isActive: boolean;
};

export type PaymentScheduleRowDto = {
  id: string;
  seq: number;
  percent: number;
  basis: string;
  days?: number | null;
  label?: string | null;
};

export type PaymentTermDto = {
  id: string;
  code: string;
  name: string;
  kind: string;
  isActive: boolean;
  dueDays?: number | null;
  dayOfMonth?: number | null;
  monthsAhead?: number | null;
  minimumDaysBeforeDue?: number | null;
  discountPct?: number | null;
  discountDays?: number | null;
  rows: PaymentScheduleRowDto[];
};

export type PaymentScheduleRowInput = {
  seq: number;
  percent: number;
  basis: string;
  days?: number | null;
  label?: string | null;
};

export type ScheduleInstalmentDto = {
  dueDate?: string | null;
  percent: number;
  discountDate?: string | null;
  discountPct?: number | null;
  label?: string | null;
};

export type LocationAddressDto = {
  id: string;
  label: string;
  line1: string;
  line2?: string | null;
  city: string;
  state: string;
  postcode: string;
  country: string;
  isDefault: boolean;
  sort: number;
};

export type LocationAddressInput = {
  label: string;
  line1: string;
  city: string;
  state: string;
  postcode: string;
  line2?: string | null;
  country?: string;
  isDefault?: boolean;
  sort?: number | null;
};

export type LocationDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  addresses: LocationAddressDto[];
};

export type NumberingSchemeDto = {
  id: string;
  recordType: string;
  prefix: string;
  yearSegment: boolean;
  digits: number;
  previewExample: string;
};

// ---- Settings ----
export function listSettings(): Promise<SettingDto[]> {
  return apiFetch<SettingDto[]>(`${ROOT}/settings`);
}

export function updateSetting(key: string, value: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/settings/${encodeURIComponent(key)}`, {
    method: "PUT",
    body: JSON.stringify({ value }),
  });
}

// ---- Currencies ----
export function listCurrencies(activeOnly = true): Promise<CurrencyDto[]> {
  return apiFetch<CurrencyDto[]>(`${ROOT}/currencies${toQuery({ activeOnly })}`);
}

export function createCurrency(input: {
  code: string;
  name: string;
  symbol: string;
  decimals?: number;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/currencies`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateCurrency(
  id: string,
  input: { name: string; symbol: string; decimals: number },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/currencies/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setCurrencyActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/currencies/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

// ---- Exchange rates ----
export function listCurrentExchangeRates(): Promise<ExchangeRateCurrentDto[]> {
  return apiFetch<ExchangeRateCurrentDto[]>(`${ROOT}/exchange-rates/current`);
}

export function listExchangeRateHistory(currencyCode: string): Promise<ExchangeRateDto[]> {
  return apiFetch<ExchangeRateDto[]>(
    `${ROOT}/exchange-rates/${encodeURIComponent(currencyCode)}/history`,
  );
}

export function appendExchangeRate(input: {
  currencyCode: string;
  rateToBase: number;
  effectiveDate: string;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/exchange-rates`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

// ---- Tax codes ----
export function listTaxCodes(activeOnly = true): Promise<TaxCodeDto[]> {
  return apiFetch<TaxCodeDto[]>(`${ROOT}/tax-codes${toQuery({ activeOnly })}`);
}

export function createTaxCode(input: { code: string; name: string; ratePct: number }): Promise<string> {
  return apiFetch<string>(`${ROOT}/tax-codes`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateTaxCode(id: string, input: { name: string; ratePct: number }): Promise<string> {
  return apiFetch<string>(`${ROOT}/tax-codes/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setTaxCodeActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/tax-codes/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

// ---- Payment terms ----
export function listPaymentTerms(activeOnly = true): Promise<PaymentTermDto[]> {
  return apiFetch<PaymentTermDto[]>(`${ROOT}/payment-terms${toQuery({ activeOnly })}`);
}

export function getPaymentTerm(id: string): Promise<PaymentTermDto> {
  return apiFetch<PaymentTermDto>(`${ROOT}/payment-terms/${encodeURIComponent(id)}`);
}

export function createPaymentTerm(input: {
  code: string;
  name: string;
  kind: string;
  dueDays?: number | null;
  dayOfMonth?: number | null;
  monthsAhead?: number | null;
  minimumDaysBeforeDue?: number | null;
  discountPct?: number | null;
  discountDays?: number | null;
  rows?: PaymentScheduleRowInput[] | null;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/payment-terms`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updatePaymentTerm(
  id: string,
  input: {
    name: string;
    kind: string;
    dueDays?: number | null;
    dayOfMonth?: number | null;
    monthsAhead?: number | null;
    minimumDaysBeforeDue?: number | null;
    discountPct?: number | null;
    discountDays?: number | null;
    rows?: PaymentScheduleRowInput[] | null;
  },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/payment-terms/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setPaymentTermActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/payment-terms/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function computePaymentSchedule(id: string, baseDate: string): Promise<ScheduleInstalmentDto[]> {
  return apiFetch<ScheduleInstalmentDto[]>(
    `${ROOT}/payment-terms/${encodeURIComponent(id)}/schedule${toQuery({ baseDate })}`,
  );
}

// ---- Incoterms ----
export function listIncoterms(activeOnly = true): Promise<IncotermDto[]> {
  return apiFetch<IncotermDto[]>(`${ROOT}/incoterms${toQuery({ activeOnly })}`);
}

export function createIncoterm(input: { code: string; name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/incoterms`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateIncoterm(id: string, input: { name: string }): Promise<string> {
  return apiFetch<string>(`${ROOT}/incoterms/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setIncotermActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/incoterms/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

// ---- Locations ----
export function listLocations(activeOnly = true): Promise<LocationDto[]> {
  return apiFetch<LocationDto[]>(`${ROOT}/locations${toQuery({ activeOnly })}`);
}

export function getLocation(id: string): Promise<LocationDto> {
  return apiFetch<LocationDto>(`${ROOT}/locations/${encodeURIComponent(id)}`);
}

export function createLocation(input: {
  code: string;
  name: string;
  addresses: LocationAddressInput[];
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/locations`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateLocation(
  id: string,
  input: { name: string; addresses: LocationAddressInput[] },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/locations/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setLocationActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/locations/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

// ---- Items ----
export function listItems(activeOnly = true): Promise<ItemDto[]> {
  return apiFetch<ItemDto[]>(`${ROOT}/items${toQuery({ activeOnly })}`);
}

export function createItem(input: {
  itemCode: string;
  description: string;
  uom?: string | null;
}): Promise<string> {
  return apiFetch<string>(`${ROOT}/items`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateItem(
  id: string,
  input: { itemCode: string; description: string; uom: string },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/items/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setItemActive(id: string, isActive: boolean): Promise<string> {
  return apiFetch<string>(`${ROOT}/items/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function deleteItem(id: string): Promise<void> {
  return apiFetch<void>(`${ROOT}/items/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

// ---- Numbering ----
export function listNumberingSchemes(): Promise<NumberingSchemeDto[]> {
  return apiFetch<NumberingSchemeDto[]>(`${ROOT}/numbering-schemes`);
}

export function updateNumberingScheme(
  recordType: string,
  input: { prefix: string; yearSegment: boolean; digits: number },
): Promise<string> {
  return apiFetch<string>(`${ROOT}/numbering-schemes/${encodeURIComponent(recordType)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function peekDocumentNumber(recordType: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/numbering-schemes/${encodeURIComponent(recordType)}/peek`);
}

export function mintDocumentNumber(recordType: string): Promise<string> {
  return apiFetch<string>(`${ROOT}/numbering-schemes/${encodeURIComponent(recordType)}/mint`, {
    method: "POST",
  });
}
