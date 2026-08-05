import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createManualVendor, type CreateManualVendorRequest } from "@/api/suppliers";
import { getGeoCatalog } from "@/api/platform";
import { useSwec } from "@/api/swec";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { SwecPicker } from "@/components/vendors/SwecPicker";
import { ApiRequestError } from "@/lib/api-client";

const REGIONS = ["Peninsular", "Sarawak", "Sabah"];
const CURRENCIES = ["MYR", "USD", "SGD", "EUR"];
const PAYMENT_TERMS = ["NET30", "NET45", "NET60", "COD"];

const empty: CreateManualVendorRequest = {
  name: "",
  registrationNo: "",
  type: "NonSwec",
  region: "Peninsular",
  countryCode: "MY",
  stateId: null,
  cityId: null,
  state: "",
  city: "",
  currencyCode: "MYR",
  paymentTerms: "NET30",
  bankId: null,
  bankName: "",
  accountNo: "",
  swift: "",
  contactName: "",
  contactEmail: "",
  addressLine: "",
  taxId: "",
  categories: [],
};

export function ManualVendorFormPage({
  onSaved,
  onBack,
}: {
  onSaved: (id: string) => void;
  onBack: () => void;
}) {
  const qc = useQueryClient();
  const { data: swec } = useSwec();
  const geoQ = useQuery({ queryKey: ["platform", "geo"], queryFn: getGeoCatalog });

  const [f, setF] = useState<CreateManualVendorRequest>(empty);
  const set = <K extends keyof CreateManualVendorRequest>(k: K, v: CreateManualVendorRequest[K]) =>
    setF((p) => ({ ...p, [k]: v }));

  const [picking, setPicking] = useState(false);
  const [warn, setWarn] = useState<string | null>(null);
  const [createdId, setCreatedId] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const country = useMemo(
    () => geoQ.data?.countries.find((c) => c.code === (f.countryCode || "MY")),
    [geoQ.data, f.countryCode],
  );
  const states = country?.states ?? [];
  const cities = states.find((s) => s.id === f.stateId)?.cities ?? [];
  const banks = (geoQ.data?.banks ?? []).filter(
    (b) => !f.countryCode || b.countryCode === f.countryCode || b.countryCode === "MY",
  );

  const save = useMutation({
    mutationFn: () => createManualVendor(f),
    onSuccess: (r) => {
      void qc.invalidateQueries({ queryKey: ["vendors"] });
      setCreatedId(r.vendorId);
      if (r.duplicateWarning) {
        setWarn(r.duplicateWarning);
        return;
      }
      onSaved(r.vendorId);
    },
    onError: (e: Error) => {
      setErr(e instanceof ApiRequestError ? e.message : e.message);
    },
  });

  const submit = () => {
    setErr(null);
    setWarn(null);
    setCreatedId(null);
    if (!f.name.trim()) {
      setErr("Enter the company name.");
      return;
    }
    save.mutate();
  };

  if (geoQ.isPending) return <Spinner label="Loading reference data…" />;

  const actions = (
    <div className="actionbar">
      <div className="spacer" style={{ flex: 1 }} />
      <button type="button" className="btn btn-out" onClick={onBack}>
        Cancel
      </button>
      <button type="button" className="btn btn-pri" disabled={save.isPending} onClick={submit}>
        <Icon name="check" size={15} /> Add to master
      </button>
    </div>
  );

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Vendor Master
        </button>{" "}
        <Icon name="chev" size={12} /> New Vendor <Icon name="chev" size={12} /> Enter manually
      </div>
      <div className="pagehead">
        <div>
          <h1>New Vendor — manual entry</h1>
          <p>Keyed straight into the master. Best for a known supplier you’re setting up quickly.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}
      {warn && createdId ? (
        <Notice tone="warn" icon="flag">
          {warn} — vendor was still created as {createdId.slice(0, 8)}….{" "}
          <button type="button" className="btn btn-out btn-sm" onClick={() => onSaved(createdId)}>
            Open vendor
          </button>
        </Notice>
      ) : null}

      {actions}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Company</h3>
        </div>
        <div className="cbody">
          <div className="grid g2">
            <div className="field">
              <label>Company name *</label>
              <input value={f.name} onChange={(e) => set("name", e.target.value)} />
            </div>
            <div className="field">
              <label>Registered name</label>
              <input
                value={f.registeredName ?? ""}
                placeholder="If different from company name"
                onChange={(e) => set("registeredName", e.target.value)}
              />
            </div>
          </div>
          <div className="grid g3">
            <div className="field">
              <label>Reg. no. (SSM)</label>
              <input
                value={f.registrationNo}
                placeholder="1234567-A"
                onChange={(e) => set("registrationNo", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Tax ID</label>
              <input value={f.taxId ?? ""} onChange={(e) => set("taxId", e.target.value)} />
            </div>
            <div className="field">
              <label>Registration type</label>
              <select value={f.type} onChange={(e) => set("type", e.target.value)}>
                <option value="NonSwec">Non-SWEC</option>
                <option value="Swec">PETRONAS SWEC</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Location</h3>
        </div>
        <div className="cbody">
          <div className="grid g3">
            <div className="field">
              <label>Country</label>
              <select
                value={f.countryCode ?? "MY"}
                onChange={(e) => {
                  set("countryCode", e.target.value);
                  set("stateId", null);
                  set("state", "");
                  set("cityId", null);
                  set("city", "");
                }}
              >
                {(geoQ.data?.countries ?? []).map((c) => (
                  <option key={c.id} value={c.code}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>State</label>
              <select
                value={f.stateId ?? ""}
                onChange={(e) => {
                  const s = states.find((x) => x.id === e.target.value);
                  set("stateId", s?.id ?? null);
                  set("state", s?.name ?? "");
                  set("cityId", null);
                  set("city", "");
                }}
              >
                <option value="">Select state</option>
                {states.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>City</label>
              <select
                value={f.cityId ?? ""}
                onChange={(e) => {
                  const c = cities.find((x) => x.id === e.target.value);
                  set("cityId", c?.id ?? null);
                  set("city", c?.name ?? "");
                }}
              >
                <option value="">Select city</option>
                {cities.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid g2">
            <div className="field">
              <label>Region</label>
              <select value={f.region ?? "Peninsular"} onChange={(e) => set("region", e.target.value)}>
                {REGIONS.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Address line</label>
              <input value={f.addressLine ?? ""} onChange={(e) => set("addressLine", e.target.value)} />
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Commercial &amp; banking</h3>
        </div>
        <div className="cbody">
          <div className="grid g2">
            <div className="field">
              <label>Currency</label>
              <select
                value={f.currencyCode ?? "MYR"}
                onChange={(e) => set("currencyCode", e.target.value)}
              >
                {CURRENCIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Payment terms</label>
              <select
                value={f.paymentTerms ?? "NET30"}
                onChange={(e) => set("paymentTerms", e.target.value)}
              >
                {PAYMENT_TERMS.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid g3">
            <div className="field">
              <label>Bank</label>
              <select
                value={f.bankId ?? ""}
                onChange={(e) => {
                  const b = banks.find((x) => x.id === e.target.value);
                  set("bankId", b?.id ?? null);
                  set("bankName", b?.name ?? "");
                }}
              >
                <option value="">Select bank</option>
                {banks.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Account no.</label>
              <input value={f.accountNo ?? ""} onChange={(e) => set("accountNo", e.target.value)} />
            </div>
            <div className="field">
              <label>SWIFT</label>
              <input value={f.swift ?? ""} onChange={(e) => set("swift", e.target.value)} />
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Contact &amp; categories</h3>
        </div>
        <div className="cbody">
          <div className="grid g2">
            <div className="field">
              <label>Contact name</label>
              <input value={f.contactName ?? ""} onChange={(e) => set("contactName", e.target.value)} />
            </div>
            <div className="field">
              <label>Contact email</label>
              <input
                type="email"
                value={f.contactEmail ?? ""}
                onChange={(e) => set("contactEmail", e.target.value)}
              />
            </div>
          </div>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>SWEC categories</label>
            <div>
              {(f.categories ?? []).length === 0 ? <span className="hint">None selected. </span> : null}
              {(f.categories ?? []).map((c) => (
                <span key={c} className="swchip" title={swec?.path(c)}>
                  {swec?.label(c) ?? c}
                </span>
              ))}
              <button type="button" className="btn btn-out btn-sm" onClick={() => setPicking(true)}>
                <Icon name="edit" size={14} /> Select categories
              </button>
            </div>
          </div>
        </div>
      </div>

      {actions}

      {picking ? (
        <SwecPicker
          initial={f.categories ?? []}
          vendorName={f.name || "this vendor"}
          onCancel={() => setPicking(false)}
          onSave={(codes) => {
            set("categories", codes);
            setPicking(false);
          }}
        />
      ) : null}
    </>
  );
}
