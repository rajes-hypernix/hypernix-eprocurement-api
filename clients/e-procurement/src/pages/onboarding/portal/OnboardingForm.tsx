import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  deleteOnboardingDocument,
  getOnboardingDraft,
  getOnboardingLookups,
  saveOnboardingDraft,
  submitOnboardingDraft,
  uploadOnboardingDocument,
  type FormTemplateDto,
  type OnboardingDocumentDto,
  type OnboardingFinancialYearDto,
  type OnboardingLookupsDto,
} from "@/api/onboarding";
import { buildSwecIndex } from "@/api/swec";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { SwecPicker } from "@/components/vendors/SwecPicker";
import {
  FIN_ITEMS,
  blankFin,
  computeFin,
  type FinData,
  type FinKey,
} from "@/lib/altmanZ";
import { formatVendorType, isSwecType } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";
import { Stat } from "@/components/vendors/EntityPage";
import { GeoCascadeFields, regionFromGeo } from "@/components/geo/GeoCascadeFields";
import { ContactPhoneInput } from "@/components/geo/ContactPhoneInput";

const DOCS: { id: string; name: string; req: "all" | "opt" | "swec" | "nonswec" }[] = [
  { id: "ssm", name: "SSM / CCM Registration", req: "all" },
  { id: "iso", name: "ISO 9001:2015 Certificate", req: "opt" },
  { id: "cidb", name: "CIDB Grade Certificate", req: "opt" },
  { id: "bank", name: "Bank Confirmation Letter", req: "all" },
  { id: "acct", name: "Audited Accounts (3 years)", req: "nonswec" },
  { id: "swec", name: "PETRONAS SWEC Certificate", req: "swec" },
];

const FIN_FIELDS: Record<FinKey, keyof OnboardingFinancialYearDto> = {
  revenue: "revenue",
  netProfit: "netProfit",
  ebit: "ebit",
  totalAssets: "totalAssets",
  currentAssets: "currentAssets",
  inventory: "inventory",
  currentLiab: "currentLiabilities",
  totalLiab: "totalLiabilities",
  equity: "equity",
  retainedEarnings: "retainedEarnings",
  fixedAssets: "fixedAssets",
};

const answerKey = (templateId: string, order: number) => `${templateId}:${order}`;

const FIN_YEAR_LABELS = ["FY-2", "FY-1", "Current"] as const;

type RequiredGap = { stepKey: string; label: string };

function collectRequiredGaps(input: {
  company: { name: string; registrationNo: string; email: string };
  docs: OnboardingDocumentDto[];
  isSwec: boolean;
  fin: FinData;
  templates: FormTemplateDto[];
  answers: Record<string, string>;
}): RequiredGap[] {
  const gaps: RequiredGap[] = [];
  if (!input.company.name.trim()) gaps.push({ stepKey: "company", label: "Registered name" });
  if (!input.company.registrationNo.trim()) {
    gaps.push({ stepKey: "company", label: "Registration number (SSM)" });
  }
  if (!input.company.email.trim()) gaps.push({ stepKey: "company", label: "Primary contact email" });

  for (const d of DOCS.filter((x) => x.req === "all")) {
    if (!input.docs.some((x) => x.key === d.id)) {
      gaps.push({ stepKey: "docs", label: d.name });
    }
  }

  if (!input.isSwec) {
    for (let i = 0; i < 3; i++) {
      if (input.fin.totalAssets[i] <= 0 || input.fin.totalLiab[i] <= 0) {
        gaps.push({
          stepKey: "fin",
          label: `${FIN_YEAR_LABELS[i]}: total assets and total liabilities`,
        });
      }
    }
  }

  for (const t of input.templates) {
    for (const q of t.questions) {
      if (q.required && !(input.answers[answerKey(t.id, q.order)] ?? "").trim()) {
        gaps.push({ stepKey: `tpl:${t.id}`, label: q.label });
      }
    }
  }
  return gaps;
}

function finFromDraft(years: OnboardingFinancialYearDto[]): FinData {
  const fin = blankFin();
  for (const y of years) {
    for (const [, k] of FIN_ITEMS) {
      fin[k][y.yearIndex] = Number(y[FIN_FIELDS[k]]) || 0;
    }
  }
  return fin;
}

function finToDraft(fin: FinData): OnboardingFinancialYearDto[] {
  return [0, 1, 2].map((i) => {
    const y = { yearIndex: i } as OnboardingFinancialYearDto;
    for (const [, k] of FIN_ITEMS) {
      (y[FIN_FIELDS[k]] as number) = fin[k][i];
    }
    return y;
  });
}

function selectedTemplates(lookups: OnboardingLookupsDto, ids: string[]): FormTemplateDto[] {
  const set = new Set(ids);
  return lookups.formTemplates.filter((t) => set.has(t.id));
}

export function OnboardingForm({
  token,
  onSubmitted,
}: {
  token: string;
  onSubmitted: () => void;
}) {
  const draftQ = useQuery({
    queryKey: ["onboarding-draft", token],
    queryFn: () => getOnboardingDraft(token),
    retry: false,
  });

  const lookupsQ = useQuery({
    queryKey: ["onboarding-lookups", token],
    queryFn: () => getOnboardingLookups(token),
    retry: false,
    staleTime: Infinity,
  });

  const draft = draftQ.data;
  const lookups = lookupsQ.data;

  const [company, setCompany] = useState({
    name: "",
    registeredName: "",
    registrationNo: "",
    taxId: "",
    email: "",
    contactName: "",
    contactPhone: "",
    region: "Peninsular",
    countryCode: "MY",
    stateId: null as string | null,
    cityId: null as string | null,
    state: "",
    city: "",
  });
  const [bank, setBank] = useState({
    bankId: null as string | null,
    bankName: "",
    accountNo: "",
    swift: "",
  });
  const [cats, setCats] = useState<string[]>([]);
  const [picking, setPicking] = useState(false);
  const [fin, setFin] = useState<FinData>(blankFin());
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [docs, setDocs] = useState<OnboardingDocumentDto[]>([]);
  const [step, setStep] = useState(0);
  const [visited, setVisited] = useState<Set<number>>(new Set([0]));
  const [err, setErr] = useState<string | null>(null);
  const [showGaps, setShowGaps] = useState(false);
  const hydrated = useRef(false);

  useEffect(() => {
    if (!draft || hydrated.current) return;
    hydrated.current = true;
    const primary = (draft.bankAccounts ?? [])[0];
    setCompany({
      name: draft.name,
      registeredName: draft.registeredName,
      registrationNo: draft.registrationNo,
      taxId: draft.taxId,
      email: draft.email,
      contactName: draft.contactName,
      contactPhone: draft.contactPhone,
      region: draft.region || (draft.countryCode && draft.countryCode !== "MY" ? "" : "Peninsular"),
      countryCode: draft.countryCode || "MY",
      stateId: draft.stateId ?? null,
      cityId: draft.cityId ?? null,
      state: draft.state,
      city: draft.city,
    });
    setBank({
      bankId: primary?.bankId ?? null,
      bankName: primary?.bankName ?? "",
      accountNo: primary?.accountNo ?? "",
      swift: primary?.swift ?? "",
    });
    setCats(draft.categories ?? []);
    setFin(finFromDraft(draft.financialYears ?? []));
    setDocs(draft.documents ?? []);
    const a: Record<string, string> = {};
    for (const ans of draft.answers ?? []) {
      a[answerKey(ans.formTemplateId, ans.questionOrder)] = ans.value;
    }
    setAnswers(a);
  }, [draft]);

  const isSwec = isSwecType(draft?.type);
  const templates = useMemo(
    () => (lookups && draft ? selectedTemplates(lookups, draft.selectedTemplateIds) : []),
    [lookups, draft],
  );

  const steps = useMemo(() => {
    const s: { k: string; n: string }[] = [
      { k: "company", n: "Company & contact" },
      { k: "banking", n: "Banking" },
      { k: "docs", n: "Documents" },
      { k: "cats", n: "SWEC categories" },
    ];
    if (!isSwec) s.push({ k: "fin", n: "Financials" });
    for (const t of templates) s.push({ k: `tpl:${t.id}`, n: t.name });
    s.push({ k: "review", n: "Review & submit" });
    return s;
  }, [isSwec, templates]);

  const gaps = useMemo(
    () =>
      collectRequiredGaps({
        company,
        docs,
        isSwec,
        fin,
        templates,
        answers,
      }),
    [company, docs, isSwec, fin, templates, answers],
  );

  const gapsByStep = useMemo(() => {
    const groups: { stepKey: string; name: string; items: RequiredGap[] }[] = [];
    for (const st of steps) {
      const items = gaps.filter((g) => g.stepKey === st.k);
      if (items.length) groups.push({ stepKey: st.k, name: st.n, items });
    }
    return groups;
  }, [steps, gaps]);

  const body = () => ({
    token,
    name: company.name,
    registeredName: company.registeredName,
    registrationNo: company.registrationNo,
    taxId: company.taxId,
    email: company.email,
    contactName: company.contactName,
    contactPhone: company.contactPhone,
    region: company.region,
    state: company.state,
    city: company.city,
    countryCode: company.countryCode,
    stateId: company.stateId,
    cityId: company.cityId,
    categories: cats,
    bankAccounts:
      bank.bankId && bank.bankName
        ? [
            {
              bankId: bank.bankId,
              bankName: bank.bankName,
              accountNo: bank.accountNo,
              swift: bank.swift,
              currencyCode: "MYR",
              isPrimary: true,
            },
          ]
        : null,
    financialYears: isSwec ? null : finToDraft(fin),
    answers: templates.flatMap((t) =>
      t.questions.map((q) => ({
        formTemplateId: t.id,
        questionOrder: q.order,
        value: answers[answerKey(t.id, q.order)] ?? "",
      })),
    ),
  });

  const saveChain = useRef<Promise<unknown>>(Promise.resolve());
  const saveDraft = () => {
    saveChain.current = saveChain.current
      .catch(() => {})
      .then(() => saveOnboardingDraft(body()))
      .catch((e: unknown) =>
        setErr(e instanceof ApiRequestError ? e.message : "Could not save your progress."),
      );
    return saveChain.current;
  };

  const submit = useMutation({
    mutationFn: async () => {
      await saveDraft();
      return submitOnboardingDraft(token);
    },
    onSuccess: onSubmitted,
    onError: (e: Error) =>
      setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const goto = (i: number) => {
    setErr(null);
    void saveDraft();
    const t = Math.max(0, Math.min(steps.length - 1, i));
    setVisited((v) => new Set(v).add(t));
    setStep(t);
  };

  const stepComplete = (k: string): boolean => !gaps.some((g) => g.stepKey === k);

  const next = () => {
    setErr(null);
    goto(step + 1);
  };

  const trySubmit = () => {
    if (gaps.length) {
      setShowGaps(true);
      return;
    }
    submit.mutate();
  };

  if (draftQ.isPending || lookupsQ.isPending) {
    return (
      <div className="onboard-land">
        <div className="onboard-card">
          <Spinner label="Opening your application…" />
        </div>
      </div>
    );
  }

  if (!draft || !lookups) {
    return (
      <div className="onboard-land">
        <div className="onboard-card">
          <Notice tone="error" icon="x">
            This onboarding link is not valid or has expired.
          </Notice>
        </div>
      </div>
    );
  }

  const swecIndex = buildSwecIndex(lookups.swec);
  const cur = steps[step];
  const fc = computeFin(fin);
  const banks = lookups.banks.filter((b) => b.countryCode === company.countryCode);

  return (
    <div className="ob-shell">
      <div className="pagehead">
        <div>
          <h1>{company.name || "Supplier"} — onboarding</h1>
          <p>
            {formatVendorType(draft.type)} · {draft.code} · progress saves automatically.
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="obwrap">
        <div className="obside">
          {steps.map((st, i) => {
            const done = visited.has(i) && stepComplete(st.k);
            const warn = visited.has(i) && !stepComplete(st.k) && i !== step;
            return (
              <button
                type="button"
                key={st.k}
                className={`obstep ${i === step ? "on" : ""} ${done ? "done" : ""} ${warn ? "warn" : ""}`}
                onClick={() => goto(i)}
              >
                <span className="n">{done ? "✓" : warn ? "!" : i + 1}</span> {st.n}
              </button>
            );
          })}
        </div>

        <div className="obmain">
          {cur.k === "company" ? (
            <Section title="Company & contact">
              <div className="grid g2">
                <Field label="Registered name *">
                  <input
                    value={company.name}
                    onChange={(e) => setCompany({ ...company, name: e.target.value })}
                  />
                </Field>
                <Field label="Legal registered name">
                  <input
                    value={company.registeredName}
                    placeholder="If different from above"
                    onChange={(e) => setCompany({ ...company, registeredName: e.target.value })}
                  />
                </Field>
              </div>
              <div className="grid g2">
                <Field label="Reg. no. (SSM) *">
                  <input
                    value={company.registrationNo}
                    placeholder="1234567-A"
                    onChange={(e) => setCompany({ ...company, registrationNo: e.target.value })}
                  />
                </Field>
                <Field label="Tax ID (SST)">
                  <input
                    value={company.taxId}
                    onChange={(e) => setCompany({ ...company, taxId: e.target.value })}
                  />
                </Field>
              </div>
              <GeoCascadeFields
                countries={lookups.countries}
                value={{
                  countryCode: company.countryCode,
                  stateId: company.stateId,
                  cityId: company.cityId,
                  state: company.state,
                  city: company.city,
                }}
                onChange={(geo) =>
                  setCompany({
                    ...company,
                    ...geo,
                    region: regionFromGeo(lookups.countries, geo.countryCode, geo.stateId, geo.state),
                  })
                }
              />
              <div className="grid g3">
                <Field label="Primary contact email *">
                  <input
                    type="email"
                    value={company.email}
                    onChange={(e) => setCompany({ ...company, email: e.target.value })}
                  />
                </Field>
                <Field label="Contact name">
                  <input
                    value={company.contactName}
                    onChange={(e) => setCompany({ ...company, contactName: e.target.value })}
                  />
                </Field>
                <div className="field">
                  <label htmlFor="contact-phone">Contact phone</label>
                  <ContactPhoneInput
                    countryCode={company.countryCode}
                    value={company.contactPhone}
                    onChange={(contactPhone) => setCompany({ ...company, contactPhone })}
                  />
                </div>
              </div>
            </Section>
          ) : null}

          {cur.k === "banking" ? (
            <Section title="Banking">
              <div className="grid g3">
                <Field label="Bank">
                  <select
                    value={bank.bankId ?? ""}
                    onChange={(e) => {
                      const b = banks.find((x) => x.id === e.target.value);
                      setBank({ ...bank, bankId: b?.id ?? null, bankName: b?.name ?? "" });
                    }}
                  >
                    <option value="">Select bank</option>
                    {banks.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Account no.">
                  <input
                    value={bank.accountNo}
                    onChange={(e) => setBank({ ...bank, accountNo: e.target.value })}
                  />
                </Field>
                <Field label="SWIFT">
                  <input
                    value={bank.swift}
                    onChange={(e) => setBank({ ...bank, swift: e.target.value })}
                  />
                </Field>
              </div>
              <p className="hint" style={{ margin: 0 }}>
                Primary account · currency MYR
              </p>
            </Section>
          ) : null}

          {cur.k === "docs" ? (
            <Section title="Documents">
              {DOCS.filter(
                (d) => (d.req !== "swec" || isSwec) && (d.req !== "nonswec" || !isSwec),
              ).map((d) => {
                const up = docs.find((x) => x.key === d.id);
                return (
                  <div className="doc" key={d.id}>
                    <div className="di">
                      <Icon name="doc" size={15} />
                    </div>
                    <div className="dn">
                      {d.name} {d.req === "all" ? <span className="req">*</span> : null}
                    </div>
                    {up ? (
                      <>
                        <span className="filepill">
                          <Icon name="check" size={12} /> {up.fileName}
                        </span>
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm"
                          onClick={() => {
                            void deleteOnboardingDocument(token, d.id).then(() =>
                              setDocs((xs) => xs.filter((x) => x.key !== d.id)),
                            );
                          }}
                        >
                          <Icon name="x" size={13} />
                        </button>
                      </>
                    ) : (
                      <label className="btn btn-out btn-sm" style={{ cursor: "pointer" }}>
                        <Icon name="upload" size={13} /> Upload
                        <input
                          type="file"
                          style={{ display: "none" }}
                          aria-label={`Upload ${d.name}`}
                          onChange={(e) => {
                            const f = e.target.files?.[0];
                            e.currentTarget.value = "";
                            if (!f) return;
                            void uploadOnboardingDocument(token, d.id, f).then((doc) =>
                              setDocs((xs) => [...xs.filter((x) => x.key !== d.id), doc]),
                            );
                          }}
                        />
                      </label>
                    )}
                  </div>
                );
              })}
            </Section>
          ) : null}

          {cur.k === "cats" ? (
            <Section title="SWEC categories">
              <p className="hint" style={{ marginTop: 0 }}>
                Select the PETRONAS SWEC categories you&apos;re registered for. These are matched
                against buyers&apos; RFQ categories.
              </p>
              <div className="catwrap">
                {cats.length === 0 ? (
                  <span className="hint">None selected yet.</span>
                ) : (
                  cats.map((c) => (
                    <span
                      key={c}
                      className="swchip"
                      title={swecIndex.path(c)}
                      style={{ cursor: "pointer" }}
                      onClick={() => setCats((xs) => xs.filter((x) => x !== c))}
                    >
                      {swecIndex.label(c) ?? c} ✕
                    </span>
                  ))
                )}
              </div>
              <button
                type="button"
                className="btn btn-out btn-sm"
                style={{ marginTop: 12 }}
                onClick={() => setPicking(true)}
              >
                <Icon name="edit" size={13} /> Select categories
              </button>
            </Section>
          ) : null}

          {cur.k === "fin" && !isSwec ? (
            <>
              <Notice tone="warn" icon="flag">
                Non-SWEC supplier — enter 3 years of figures (RM&apos;000). An Altman Z-score is
                computed automatically as you type.
              </Notice>
              <div className="card" style={{ marginBottom: 14 }}>
                <div className="cbody">
                  <div className="grid g3">
                    <Stat
                      label="Altman Z · weighted"
                      value={Number.isFinite(fc.zW) ? fc.zW.toFixed(2) : "—"}
                      sub={fc.bd.zone}
                    />
                    <Stat
                      label="Score"
                      value={Number.isFinite(fc.score) ? `${fc.score} / 100` : "—"}
                    />
                    <div className="card stat" style={{ margin: 0 }}>
                      <div className="lbl">Band → Risk</div>
                      <div className="num">Band {fc.bd.band}</div>
                      <div className="sub">
                        <span className={`badge ${fc.bd.cls}`}>{fc.bd.risk} risk</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <Section title="Financial data (RM'000)">
                <table className="comp">
                  <thead>
                    <tr>
                      <th>Item</th>
                      <th className="amt">FY-2</th>
                      <th className="amt">FY-1</th>
                      <th className="amt">Current</th>
                    </tr>
                  </thead>
                  <tbody>
                    {FIN_ITEMS.map(([label, k]) => (
                      <tr key={k}>
                        <td style={{ fontWeight: 600 }}>{label}</td>
                        {[0, 1, 2].map((i) => (
                          <td key={i}>
                            <input
                              type="number"
                              className="amt"
                              aria-label={`${label} year ${i + 1}`}
                              value={fin[k][i] || ""}
                              onChange={(e) =>
                                setFin((prev) => {
                                  const n = Number(e.target.value) || 0;
                                  if (Math.abs(n) >= 1e12) return prev;
                                  const nx = {
                                    ...prev,
                                    [k]: [...prev[k]] as [number, number, number],
                                  };
                                  nx[k][i] = n;
                                  return nx;
                                })
                              }
                            />
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </Section>
            </>
          ) : null}

          {cur.k.startsWith("tpl:") ? (
            (() => {
              const tpl = templates.find((t) => `tpl:${t.id}` === cur.k)!;
              return (
                <Section title={tpl.name}>
                  {tpl.questions.map((q) => (
                    <div className="q" key={q.order}>
                      <div className="qt">
                        {q.label}
                        {q.required ? <span className="req"> *</span> : null}
                      </div>
                      {q.help ? <p className="hint">{q.help}</p> : null}
                      <input
                        value={answers[answerKey(tpl.id, q.order)] ?? ""}
                        onChange={(e) =>
                          setAnswers((a) => ({
                            ...a,
                            [answerKey(tpl.id, q.order)]: e.target.value,
                          }))
                        }
                      />
                    </div>
                  ))}
                </Section>
              );
            })()
          ) : null}

          {cur.k === "review" ? (
            <>
              {gaps.length === 0 ? (
                <Notice tone="success" icon="check">
                  Looks complete. Submit to route to SPSB procurement.
                </Notice>
              ) : (
                <Notice tone="warn" icon="flag">
                  {gaps.length} required {gaps.length === 1 ? "item is" : "items are"} still
                  missing. Use the list on the right to jump to each section.
                </Notice>
              )}
              <Section title="Summary">
                <div className="kv">
                  <span className="k">Vendor</span>
                  <span className="v">{company.name || "—"}</span>
                </div>
                <div className="kv">
                  <span className="k">Type</span>
                  <span className="v">{formatVendorType(draft.type)}</span>
                </div>
                <div className="kv">
                  <span className="k">Documents attached</span>
                  <span className="v">{docs.length}</span>
                </div>
                {!isSwec ? (
                  <div className="kv">
                    <span className="k">Financial band</span>
                    <span className="v">Band {fc.bd.band}</span>
                  </div>
                ) : null}
              </Section>
            </>
          ) : null}

          {cur.k !== "review" && !stepComplete(cur.k) ? (
            <Notice tone="warn" icon="flag" style={{ marginTop: 14 }}>
              This section still needs attention. You can continue and finish it later — required
              fields must be complete to submit.
            </Notice>
          ) : null}

          <div className="actionbar">
            <button type="button" className="btn btn-out" disabled={step === 0} onClick={() => goto(step - 1)}>
              Back
            </button>
            <div className="spacer" style={{ flex: 1 }} />
            {step < steps.length - 1 ? (
              <button type="button" className="btn btn-pri" onClick={next}>
                Next <Icon name="chev" size={14} />
              </button>
            ) : (
              <button
                type="button"
                className="btn btn-pri"
                disabled={submit.isPending}
                onClick={trySubmit}
              >
                <Icon name="check" size={15} /> Submit application
              </button>
            )}
          </div>
        </div>

        <aside
          className={`obcheck ${gaps.length === 0 ? "ok" : ""} ${showGaps && gaps.length > 0 ? "attn" : ""}`}
          aria-live="polite"
        >
          <div className="obcheck-h">
            {gaps.length === 0
              ? "Ready to submit"
              : `${gaps.length} required ${gaps.length === 1 ? "item" : "items"} left`}
          </div>
          {gaps.length === 0 ? (
            <p className="obcheck-hint">All required fields, documents, and questions are complete.</p>
          ) : (
            <>
              <p className="obcheck-hint">Click an item to open that section.</p>
              {gapsByStep.map((group) => (
                <div key={group.stepKey} className="obcheck-group">
                  <div className="obcheck-sec">{group.name}</div>
                  {group.items.map((item, i) => (
                    <button
                      type="button"
                      key={`${group.stepKey}:${i}:${item.label}`}
                      className="obcheck-item"
                      onClick={() => goto(steps.findIndex((s) => s.k === group.stepKey))}
                    >
                      {item.label}
                    </button>
                  ))}
                </div>
              ))}
            </>
          )}
        </aside>
      </div>

      {picking ? (
        <SwecPicker
          initial={cats}
          vendorName={company.name || "your company"}
          onCancel={() => setPicking(false)}
          onSave={(codes) => {
            setCats(codes);
            setPicking(false);
          }}
        />
      ) : null}
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card" style={{ marginBottom: 14 }}>
      <div className="chead">
        <h3>{title}</h3>
      </div>
      <div className="cbody">{children}</div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="field">
      <label>{label}</label>
      {children}
    </div>
  );
}
