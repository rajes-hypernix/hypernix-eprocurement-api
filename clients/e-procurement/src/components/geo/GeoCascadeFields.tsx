import type { CountryLookupDto } from "@/api/platform";

export type GeoCascadeValue = {
  countryCode: string;
  stateId: string | null;
  cityId: string | null;
  state: string;
  city: string;
};

export function malaysiaRegion(stateCode?: string | null, stateName?: string | null): string {
  const code = (stateCode ?? "").toUpperCase();
  const name = (stateName ?? "").toLowerCase();
  if (code === "SBH" || name.includes("sabah")) return "Sabah";
  if (code === "SWK" || name.includes("sarawak")) return "Sarawak";
  return "Peninsular";
}

export function regionFromGeo(
  countries: CountryLookupDto[],
  countryCode: string,
  stateId: string | null,
  stateName: string,
): string {
  if (countryCode !== "MY") return "";
  const state = countries
    .find((c) => c.code === countryCode)
    ?.states.find((s) => s.id === stateId);
  return malaysiaRegion(state?.code, stateName || state?.name);
}

export function GeoCascadeFields({
  countries,
  value,
  onChange,
}: {
  countries: CountryLookupDto[];
  value: GeoCascadeValue;
  onChange: (next: GeoCascadeValue) => void;
}) {
  const country = countries.find((c) => c.code === value.countryCode);
  const states = country?.states ?? [];
  const hasStateCatalog = states.length > 0;
  const selectedState = states.find((s) => s.id === value.stateId);
  const cities = selectedState?.cities ?? [];
  const hasCityCatalog = cities.length > 0;

  const setCountry = (countryCode: string) => {
    onChange({
      countryCode,
      stateId: null,
      cityId: null,
      state: "",
      city: "",
    });
  };

  const setStateFromCatalog = (stateId: string) => {
    const s = states.find((x) => x.id === stateId);
    onChange({
      ...value,
      stateId: s?.id ?? null,
      state: s?.name ?? "",
      cityId: null,
      city: "",
    });
  };

  const setStateFreeText = (state: string) => {
    onChange({
      ...value,
      stateId: null,
      state,
      cityId: null,
      city: "",
    });
  };

  const setCityFromCatalog = (cityId: string) => {
    const c = cities.find((x) => x.id === cityId);
    onChange({
      ...value,
      cityId: c?.id ?? null,
      city: c?.name ?? "",
    });
  };

  const setCityFreeText = (city: string) => {
    onChange({ ...value, cityId: null, city });
  };

  return (
    <div className="grid g3">
      <div className="field">
        <label htmlFor="geo-country">Country</label>
        <select
          id="geo-country"
          value={value.countryCode}
          onChange={(e) => setCountry(e.target.value)}
        >
          {countries.length === 0 ? <option value="">No countries</option> : null}
          {countries.map((c) => (
            <option key={c.id} value={c.code}>
              {c.name}
            </option>
          ))}
        </select>
      </div>
      <div className="field">
        <label htmlFor="geo-state">State / Region</label>
        {hasStateCatalog ? (
          <select
            id="geo-state"
            value={value.stateId ?? ""}
            onChange={(e) => setStateFromCatalog(e.target.value)}
          >
            <option value="">Select state</option>
            {states.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        ) : (
          <input
            id="geo-state"
            type="text"
            value={value.state}
            placeholder="State / region"
            disabled={!value.countryCode}
            onChange={(e) => setStateFreeText(e.target.value)}
          />
        )}
      </div>
      <div className="field">
        <label htmlFor="geo-city">City</label>
        {hasCityCatalog ? (
          <select
            id="geo-city"
            value={value.cityId ?? ""}
            onChange={(e) => setCityFromCatalog(e.target.value)}
          >
            <option value="">Select city</option>
            {cities.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        ) : (
          <input
            id="geo-city"
            type="text"
            value={value.city}
            placeholder={hasStateCatalog && !value.stateId ? "Select state first" : "City"}
            disabled={hasStateCatalog && !value.stateId}
            onChange={(e) => setCityFreeText(e.target.value)}
          />
        )}
      </div>
    </div>
  );
}
