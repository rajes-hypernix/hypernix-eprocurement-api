import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getVendor, setVendorCategories, toggleVendorStatus } from "@/api/suppliers";
import { useSwec } from "@/api/swec";
import { Spinner } from "@/components/ui";
import { StatusBadge, TypeBadge } from "@/components/vendors/badges";
import { EntityPage, Kv, Stat } from "@/components/vendors/EntityPage";
import { SwecPicker } from "@/components/vendors/SwecPicker";
import { dateMY, fmt, initials } from "@/lib/format";
import { Icon } from "@/components/Icon";

const TABS: [string, string][] = [
  ["overview", "Overview"],
  ["categories", "Categories"],
  ["contacts", "Contacts"],
  ["addresses", "Addresses"],
  ["banking", "Banking & Currencies"],
  ["compliance", "Compliance"],
  ["performance", "Performance"],
];

export function VendorDetailPage({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { data: swec } = useSwec();
  const [tab, setTab] = useState("overview");
  const [picking, setPicking] = useState(false);

  const { data: v, isPending } = useQuery({
    queryKey: ["vendor", id],
    queryFn: () => getVendor(id),
  });

  const saveCats = useMutation({
    mutationFn: (codes: string[]) => setVendorCategories(id, codes),
    onSuccess: () => {
      setPicking(false);
      void qc.invalidateQueries({ queryKey: ["vendor", id] });
      void qc.invalidateQueries({ queryKey: ["vendors"] });
    },
  });

  const toggle = useMutation({
    mutationFn: () => toggleVendorStatus(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["vendor", id] });
      void qc.invalidateQueries({ queryKey: ["vendors"] });
    },
  });

  if (isPending || !v) return <Spinner label="Loading vendor…" />;

  const cats = v.categories ?? [];
  const contacts = v.contacts ?? [];
  const addresses = v.addresses ?? [];
  const bankAccounts = v.bankAccounts ?? [];
  const certifications = v.certifications ?? [];
  const currencies = v.currencies ?? [];
  const displayName = v.registeredName || v.name;

  const content: Record<string, React.ReactNode> = {
    overview: (
      <>
        <div className="grid g4" style={{ marginBottom: 16 }}>
          <Stat label="On-time delivery" value="not yet available" sub="rolling 12 mo" />
          <Stat label="Quality acceptance" value="not yet available" sub="goods accepted" />
          <Stat label="Win rate" value="not yet available" sub="RFQs won" />
          <Stat label="Spend YTD" value="not yet available" sub="from POs" />
        </div>
        <div className="grid g2">
          <div className="card">
            <div className="chead">
              <h3>Identity</h3>
            </div>
            <div className="cbody">
              <Kv k="Registered name" v={v.registeredName || "—"} />
              <Kv k="Vendor code" v={v.code} />
              <Kv k="SSM / reg. no." v={v.registrationNo || "—"} />
              <Kv k="Tax ID (SST)" v={v.taxId || "—"} />
              <Kv k="Country" v={v.countryCode || "—"} />
              <Kv k="Region / state" v={`${v.region || "—"} · ${v.state || "—"}`} />
              <Kv k="City" v={v.city || "—"} />
            </div>
          </div>
          <div className="card">
            <div className="chead">
              <h3>Registration &amp; Terms</h3>
            </div>
            <div className="cbody">
              <Kv k="Type" v={<TypeBadge type={v.type} />} />
              <Kv k="LLRC tier" v={v.llrcTier ?? "—"} />
              <Kv k="Status" v={<StatusBadge status={v.status} />} />
              <Kv k="Payment terms" v={v.paymentTerms || "—"} />
              <Kv k="Credit limit" v={`RM ${fmt(v.creditLimit)}`} />
              <Kv k="Created" v={dateMY(v.createdOnUtc)} />
              <Kv k="Last updated" v={dateMY(v.lastModifiedOnUtc ?? v.createdOnUtc)} />
            </div>
          </div>
        </div>
      </>
    ),
    categories: (
      <div className="card">
        <div className="chead">
          <h3>SWEC Categories</h3>
          <div className="spacer" />
          <button type="button" className="btn btn-out btn-sm" onClick={() => setPicking(true)}>
            <Icon name="edit" size={14} /> Edit categories
          </button>
        </div>
        <div className="cbody">
          {cats.length === 0 ? <p className="hint">No categories tagged yet.</p> : null}
          {cats.map((c) => (
            <div key={c} className="swrow">
              <span className="swpath">{swec?.path(c) ?? c}</span>
              <span className="swcode">{c}</span>
            </div>
          ))}
        </div>
      </div>
    ),
    contacts: (
      <div className="card">
        <div className="chead">
          <h3>Contacts</h3>
          <div className="spacer" />
          <span className="hint">{contacts.length} contact(s)</span>
        </div>
        <div className="cbody">
          {contacts.length === 0 ? <p className="hint">No contacts on file.</p> : null}
          {contacts.map((c, i) => (
            <div className="ctc" key={i}>
              <div className="ctc-av">{initials(c.name)}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 700 }}>
                  {c.name} {c.isPrimary ? <span className="badge b-teal">Primary</span> : null}
                </div>
                <div className="hint">{c.role || "—"}</div>
              </div>
              <div style={{ textAlign: "right", fontSize: 12.5 }}>
                <div>{c.email || "—"}</div>
                <div className="hint">{c.phone || "—"}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    ),
    addresses: (
      <div className="card">
        <div className="chead">
          <h3>Addresses</h3>
          <div className="spacer" />
          <span className="hint">{addresses.length} address(es)</span>
        </div>
        <div className="cbody grid g2">
          {addresses.length === 0 ? <p className="hint">No addresses on file.</p> : null}
          {addresses.map((a, i) => (
            <div className="addr" key={i}>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
                <span className="pill b-grey">{a.type}</span>
                {a.isPrimary ? <span className="badge b-teal">Primary</span> : null}
              </div>
              <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                {a.line || "—"}
                <br />
                {a.city}
                {a.postcode ? `, ${a.postcode}` : ""}
                <br />
                {a.state}, {a.countryCode}
              </div>
            </div>
          ))}
        </div>
      </div>
    ),
    banking: (
      <>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="chead">
            <h3>Transacting Currencies</h3>
          </div>
          <div className="cbody">
            {currencies.length === 0 ? <span className="hint">None</span> : null}
            {currencies.map((c) => (
              <span key={c.code} className="swchip">
                {c.code}
                {c.isPrimary ? " · primary" : ""}
              </span>
            ))}
          </div>
        </div>
        <div className="card">
          <div className="chead">
            <h3>Bank Accounts</h3>
            <div className="spacer" />
            <span className="hint">for electronic payment</span>
          </div>
          <table>
            <thead>
              <tr>
                <th>Bank</th>
                <th>Account no.</th>
                <th>SWIFT</th>
                <th>Currency</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {bankAccounts.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    <span className="hint">No bank accounts on file.</span>
                  </td>
                </tr>
              ) : null}
              {bankAccounts.map((b, i) => (
                <tr key={i}>
                  <td style={{ fontWeight: 600 }}>{b.bankName}</td>
                  <td>{b.accountNo}</td>
                  <td>{b.swift}</td>
                  <td>{b.currencyCode}</td>
                  <td>{b.isPrimary ? <span className="badge b-teal">Primary</span> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </>
    ),
    compliance: (
      <div className="card">
        <div className="chead">
          <h3>Certifications &amp; Compliance</h3>
        </div>
        <table>
          <thead>
            <tr>
              <th>Certificate / document</th>
              <th>Reference</th>
              <th>Valid to</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {certifications.length === 0 ? (
              <tr>
                <td colSpan={4}>
                  <span className="hint">No certifications on file.</span>
                </td>
              </tr>
            ) : null}
            {certifications.map((c, i) => (
              <tr key={i}>
                <td style={{ fontWeight: 600 }}>{c.name}</td>
                <td>{c.number}</td>
                <td>{c.validTo}</td>
                <td>
                  <span className="badge b-green">{c.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    ),
    performance: (
      <>
        <div className="grid g4" style={{ marginBottom: 16 }}>
          <Stat label="Compliance breaches" value="not yet available" sub="rolling 12 mo" />
          <Stat label="Avg lead time" value="not yet available" sub="order to receipt" />
          <Stat label="Response rate" value="not yet available" sub="RFQ invites" />
          <Stat label="Rating" value={`★ ${v.rating}`} sub="overall" />
        </div>
        <div className="card">
          <div className="chead">
            <h3>Performance KPIs</h3>
          </div>
          <div className="cbody">
            <p className="hint">
              Live performance metrics land when Procurement POs and deliveries feed supplier scorecards.
              Rating above is the stored vendor rating.
            </p>
          </div>
        </div>
      </>
    ),
  };

  return (
    <EntityPage
      crumbParent="Vendor Master"
      onCrumbParent={onBack}
      crumbCurrent={displayName}
      title={
        <>
          {displayName} <TypeBadge type={v.type} />
        </>
      }
      subtitle={
        <>
          {v.code} · {v.region} · {v.state} · <StatusBadge status={v.status} />
        </>
      }
      actions={
        <>
          <button type="button" className="btn btn-out btn-sm" onClick={() => setPicking(true)}>
            <Icon name="edit" size={14} /> Categories
          </button>
          <button
            type="button"
            className="btn btn-out btn-sm"
            disabled={toggle.isPending}
            onClick={() => toggle.mutate()}
          >
            {v.status === "Inactive" ? "Reactivate" : "Deactivate"}
          </button>
        </>
      }
      tabs={TABS.map(([k, l]) => ({ key: k, label: l, content: content[k] }))}
      tab={tab}
      onTabChange={setTab}
    >
      {picking ? (
        <SwecPicker
          initial={cats}
          vendorName={displayName}
          busy={saveCats.isPending}
          onCancel={() => setPicking(false)}
          onSave={(codes) => saveCats.mutate(codes)}
        />
      ) : null}
    </EntityPage>
  );
}
