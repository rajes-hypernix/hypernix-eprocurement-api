import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  appendExchangeRate,
  computePaymentSchedule,
  createCurrency,
  createIncoterm,
  createItem,
  createLocation,
  createPaymentTerm,
  createTaxCode,
  deleteItem,
  getLocation,
  getPaymentTerm,
  listCurrencies,
  listCurrentExchangeRates,
  listExchangeRateHistory,
  listIncoterms,
  listItems,
  listLocations,
  listNumberingSchemes,
  listPaymentTerms,
  listSettings,
  listTaxCodes,
  peekDocumentNumber,
  setCurrencyActive,
  setIncotermActive,
  setItemActive,
  setLocationActive,
  setPaymentTermActive,
  setTaxCodeActive,
  updateCurrency,
  updateIncoterm,
  updateItem,
  updateLocation,
  updateNumberingScheme,
  updatePaymentTerm,
  updateSetting,
  updateTaxCode,
  type CurrencyDto,
  type ExchangeRateCurrentDto,
  type ExchangeRateDto,
  type IncotermDto,
  type ItemDto,
  type LocationAddressDto,
  type LocationAddressInput,
  type LocationDto,
  type NumberingSchemeDto,
  type PaymentScheduleRowInput,
  type PaymentTermDto,
  type ScheduleInstalmentDto,
  type SettingDto,
  type TaxCodeDto,
} from "@/api/configuration";
import {
  applyCustomFieldToRecordType,
  createCustomFieldDef,
  listCustomFieldDefs,
  removeCustomFieldApplication,
  setCustomFieldDefActive,
  type CustomFieldDefDto,
} from "@/api/platform";
import { DataTable, type DataTableColumn } from "@/components/DataTable";
import { EntityChangeHistoryModal } from "@/components/EntityChangeHistoryModal";
import {
  ExcelImportModal,
  type ExcelImportProgress,
  type ExcelImportRunner,
} from "@/components/ExcelImportModal";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { AlertModal, ConfirmModal, EmptyState, Modal, Spinner } from "@/components/ui";
import { formatApiError, useErrorDialog } from "@/feedback/ErrorDialogContext";
import { activeLabel, cellActive, cellStr, exportRowsToExcel, parseExcelFile } from "@/lib/excel";
import { dateMY, dateTimeMY } from "@/lib/format";
import { FshPermissions } from "@/lib/fsh-permissions";

type Tab =
  | "settings"
  | "currencies"
  | "rates"
  | "tax"
  | "payment"
  | "incoterms"
  | "locations"
  | "items"
  | "numbering"
  | "customFields";

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: "settings", icon: "menu", label: "Settings" },
  { key: "currencies", icon: "clip", label: "Currencies" },
  { key: "rates", icon: "chart", label: "Exchange rates" },
  { key: "tax", icon: "hash", label: "Tax codes" },
  { key: "payment", icon: "send", label: "Payment terms" },
  { key: "incoterms", icon: "flag", label: "Incoterms" },
  { key: "locations", icon: "field", label: "Locations" },
  { key: "items", icon: "box", label: "Items" },
  { key: "numbering", icon: "list", label: "Numbering" },
  { key: "customFields", icon: "field", label: "Custom Fields" },
];

export const CONFIG_TAB_KEYS = TABS.map((t) => t.key);

function resolveTab(candidate: string | null | undefined): Tab {
  const hit = TABS.find((t) => t.key === candidate);
  return hit ? hit.key : "settings";
}

// ---- shared list helpers (copied from LookupsPage for parity) ----

function StatusBadge({ active }: { active: boolean }) {
  return <span className={`badge ${active ? "b-green" : "b-red"}`}>{active ? "Active" : "Inactive"}</span>;
}

function TableFooter({ total, active }: { total: number; active: number }) {
  return (
    <p className="hint" style={{ margin: "10px 0 0", textAlign: "right" }}>
      {total} total · {active} active
    </p>
  );
}

function IconButton({
  icon,
  label,
  onClick,
  danger,
}: {
  icon: string;
  label: string;
  onClick: () => void;
  danger?: boolean;
}) {
  return (
    <button
      type="button"
      className={`btn btn-out btn-sm btn-icon${danger ? " btn-danger-text" : ""}`}
      title={label}
      aria-label={label}
      onClick={onClick}
    >
      <Icon name={icon} size={14} />
    </button>
  );
}

function HistoryButton({
  label,
  entityId,
  onOpen,
}: {
  label: string;
  entityId: string;
  onOpen: (args: { title: string; entityId: string }) => void;
}) {
  return (
    <Gated permission={FshPermissions.auditTrails.view}>
      <IconButton icon="clock" label={`History · ${label}`} onClick={() => onOpen({ title: label, entityId })} />
    </Gated>
  );
}

function DetailHeader({
  title,
  subtitle,
  onBack,
  backLabel = "Back",
}: {
  title: string;
  subtitle?: string;
  onBack: () => void;
  backLabel?: string;
}) {
  return (
    <div className="detailhead">
      <button type="button" className="btn btn-out btn-sm" onClick={onBack}>
        <Icon name="back" size={14} /> {backLabel}
      </button>
      <div className="detailhead-text">
        <h2>{title}</h2>
        {subtitle ? <p className="hint">{subtitle}</p> : null}
      </div>
    </div>
  );
}

function ExcelToolbar({
  onExport,
  onImport,
  exportDisabled,
}: {
  onExport: () => void;
  onImport: () => void;
  exportDisabled?: boolean;
}) {
  return (
    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
      <button type="button" className="btn btn-out btn-sm" disabled={exportDisabled} onClick={onExport}>
        Export Excel
      </button>
      <button type="button" className="btn btn-out btn-sm" onClick={onImport}>
        Import Excel
      </button>
    </div>
  );
}

async function runExcelRows(
  file: File,
  onProgress: (p: ExcelImportProgress) => void,
  processRow: (row: Record<string, unknown>, line: number) => Promise<void>,
): Promise<ExcelImportProgress> {
  const rows = await parseExcelFile(file);
  const result: ExcelImportProgress = {
    total: rows.length,
    processed: 0,
    success: 0,
    errors: [],
  };
  onProgress({ ...result, errors: [] });

  for (let i = 0; i < rows.length; i++) {
    const line = i + 2;
    try {
      await processRow(rows[i]!, line);
      result.success += 1;
    } catch (e) {
      result.errors.push({ line, message: formatApiError(e) });
    }
    result.processed = i + 1;
    onProgress({
      total: result.total,
      processed: result.processed,
      success: result.success,
      errors: [...result.errors],
    });
  }

  return result;
}

function numOrNull(s: string): number | null {
  if (!s.trim()) return null;
  const n = Number(s);
  return Number.isNaN(n) ? null : n;
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="hint" style={{ fontSize: 11, textTransform: "uppercase", letterSpacing: 0.4 }}>
        {label}
      </div>
      <div style={{ fontWeight: 600 }}>{value}</div>
    </div>
  );
}

type HistoryOpen = (args: { title: string; entityId: string }) => void;

type PendingStatus = { id: string; name: string; activate: boolean };

// ---- main export ----

export function ConfigurationPage({
  initialTab,
  embedded = false,
}: {
  initialTab?: string;
  embedded?: boolean;
}) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [tab, setTab] = useState<Tab>(() => resolveTab(initialTab ?? searchParams.get("tab")));
  const [history, setHistory] = useState<{ title: string; entityId: string } | null>(null);

  useEffect(() => {
    setTab(resolveTab(initialTab ?? searchParams.get("tab")));
  }, [initialTab, searchParams]);

  const changeTab = (next: Tab) => {
    setTab(next);
    const params = new URLSearchParams(searchParams);
    params.set("tab", next);
    setSearchParams(params, { replace: true });
  };

  return (
    <>
      {!embedded ? (
        <div className="pagehead">
          <div>
            <h1>Masters</h1>
            <p>Platform settings, currencies, tax, payment terms, and reference masters.</p>
          </div>
          <div className="spacer" />
          <div className="viewtoggle">
            {TABS.map(({ key, icon, label }) => (
              <button key={key} type="button" className={tab === key ? "on" : ""} onClick={() => changeTab(key)}>
                <Icon name={icon} size={14} /> {label}
              </button>
            ))}
          </div>
        </div>
      ) : null}

      {tab === "settings" ? <SettingsTab onHistory={setHistory} /> : null}
      {tab === "currencies" ? <CurrenciesTab onHistory={setHistory} /> : null}
      {tab === "rates" ? <RatesTab /> : null}
      {tab === "tax" ? <TaxTab onHistory={setHistory} /> : null}
      {tab === "payment" ? <PaymentTab onHistory={setHistory} /> : null}
      {tab === "incoterms" ? <IncotermsTab onHistory={setHistory} /> : null}
      {tab === "locations" ? <LocationsTab onHistory={setHistory} /> : null}
      {tab === "items" ? <ItemsTab onHistory={setHistory} /> : null}
      {tab === "numbering" ? <NumberingTab onHistory={setHistory} /> : null}
      {tab === "customFields" ? <CustomFieldsTab /> : null}

      {history ? (
        <EntityChangeHistoryModal
          title={history.title}
          entityId={history.entityId}
          onClose={() => setHistory(null)}
        />
      ) : null}
    </>
  );
}

// ==================== SETTINGS ====================

function SettingsTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [editing, setEditing] = useState<SettingDto | null>(null);
  const [editValue, setEditValue] = useState("");

  const settingsQuery = useQuery({ queryKey: ["settings"], queryFn: listSettings });
  const settings = settingsQuery.data ?? [];

  const saveEdit = useMutation({
    mutationFn: () => updateSetting(editing!.key, editValue),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["settings"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save setting"),
  });

  const columns: DataTableColumn<SettingDto>[] = useMemo(
    () => [
      {
        id: "key",
        header: "Key",
        sortable: true,
        sortValue: (s) => s.key,
        render: (s) => <span style={{ fontWeight: 600 }}>{s.key}</span>,
      },
      { id: "label", header: "Label", sortable: true, sortValue: (s) => s.label, render: (s) => s.label },
      {
        id: "kind",
        header: "Type",
        sortable: true,
        sortValue: (s) => s.valueKind,
        render: (s) => <span className="hint">{s.valueKind}</span>,
      },
      { id: "value", header: "Value", render: (s) => <code>{s.value}</code> },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (s) => Date.parse(s.createdOnUtc) || 0,
        render: (s) => <span className="hint">{dateTimeMY(s.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (s) => (
          <div className="row-actions">
            <HistoryButton label={s.label} entityId={s.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(s);
                  setEditValue(s.value);
                }}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  return (
    <>
      <div className="card">
        <DataTable
          rows={settings}
          columns={columns}
          rowKey={(s) => s.id}
          loading={settingsQuery.isPending}
          loadingLabel="Loading settings…"
          empty={<EmptyState icon="menu">No settings configured.</EmptyState>}
          initialSort={{ id: "key", dir: "asc" }}
          footer={
            <p className="hint" style={{ margin: "10px 0 0", textAlign: "right" }}>
              {settings.length} total
            </p>
          }
        />
      </div>

      {editing ? (
        <Modal
          title={`Edit setting · ${editing.key}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending}
                onClick={() => saveEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="field" style={{ margin: 0 }}>
            <label>{editing.label}</label>
            {editing.valueKind.toLowerCase() === "bool" || editing.valueKind.toLowerCase() === "boolean" ? (
              <select value={editValue} onChange={(e) => setEditValue(e.target.value)} autoFocus>
                <option value="true">True</option>
                <option value="false">False</option>
              </select>
            ) : (
              <input value={editValue} onChange={(e) => setEditValue(e.target.value)} autoFocus />
            )}
            {editing.description ? <span className="hint">{editing.description}</span> : null}
          </div>
        </Modal>
      ) : null}
    </>
  );
}

// ==================== CURRENCIES ====================

function CurrenciesTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [symbol, setSymbol] = useState("");
  const [decimals, setDecimals] = useState("2");
  const [editing, setEditing] = useState<CurrencyDto | null>(null);
  const [editName, setEditName] = useState("");
  const [editSymbol, setEditSymbol] = useState("");
  const [editDecimals, setEditDecimals] = useState("2");
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);

  const currenciesQuery = useQuery({ queryKey: ["currencies", false], queryFn: () => listCurrencies(false) });
  const currencies = currenciesQuery.data ?? [];

  const addCurrency = useMutation({
    mutationFn: () =>
      createCurrency({
        code: code.trim().toUpperCase(),
        name: name.trim(),
        symbol: symbol.trim(),
        decimals: Number(decimals) || 0,
      }),
    onSuccess: () => {
      setCode("");
      setName("");
      setSymbol("");
      setDecimals("2");
      void qc.invalidateQueries({ queryKey: ["currencies"] });
    },
    onError: (e) => showErrorFrom(e, "Could not add currency"),
  });

  const saveEdit = useMutation({
    mutationFn: () =>
      updateCurrency(editing!.id, {
        name: editName.trim(),
        symbol: editSymbol.trim(),
        decimals: Number(editDecimals) || 0,
      }),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["currencies"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save currency"),
  });

  const toggleCurrency = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setCurrencyActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["currencies"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update currency"),
  });

  const runImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listCurrencies(false);
    const byCode = new Map(existing.map((c) => [c.code.toUpperCase(), c]));

    return runExcelRows(file, onProgress, async (row) => {
      const rowCode = cellStr(row, "Code").toUpperCase();
      const rowName = cellStr(row, "Name");
      const rowSymbol = cellStr(row, "Symbol");
      const decRaw = cellStr(row, "Decimals");
      const active = cellActive(row);
      if (!rowCode) throw new Error("Code is required.");
      if (!rowName) throw new Error("Name is required.");
      if (!rowSymbol) throw new Error("Symbol is required.");

      const hit = byCode.get(rowCode);
      const dec = decRaw ? Number(decRaw) : hit?.decimals ?? 2;
      if (Number.isNaN(dec)) throw new Error("Decimals must be a number.");

      if (hit) {
        await updateCurrency(hit.id, { name: rowName, symbol: rowSymbol, decimals: dec });
        if (active != null && active !== hit.isActive) await setCurrencyActive(hit.id, active);
        byCode.set(rowCode, { ...hit, name: rowName, symbol: rowSymbol, decimals: dec, isActive: active ?? hit.isActive });
      } else {
        const id = await createCurrency({ code: rowCode, name: rowName, symbol: rowSymbol, decimals: dec });
        if (active === false) await setCurrencyActive(id, false);
        byCode.set(rowCode, { id, code: rowCode, name: rowName, symbol: rowSymbol, decimals: dec, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const columns: DataTableColumn<CurrencyDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (c) => c.code,
        render: (c) => <span style={{ fontWeight: 600 }}>{c.code}</span>,
      },
      { id: "name", header: "Name", sortable: true, sortValue: (c) => c.name, render: (c) => c.name },
      {
        id: "symbol",
        header: "Symbol",
        sortable: true,
        sortValue: (c) => c.symbol,
        render: (c) => <span className="hint">{c.symbol}</span>,
      },
      {
        id: "decimals",
        header: "Decimals",
        align: "right",
        sortable: true,
        sortValue: (c) => c.decimals,
        render: (c) => c.decimals,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (c) => (c.isActive ? 1 : 0),
        render: (c) => <StatusBadge active={c.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (c) => Date.parse(c.createdOnUtc) || 0,
        render: (c) => <span className="hint">{dateTimeMY(c.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (c) => (
          <div className="row-actions">
            <HistoryButton label={c.name} entityId={c.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(c);
                  setEditName(c.name);
                  setEditSymbol(c.symbol);
                  setEditDecimals(String(c.decimals));
                }}
              />
              <IconButton
                icon={c.isActive ? "x" : "check"}
                label={c.isActive ? "Deactivate" : "Activate"}
                danger={c.isActive}
                onClick={() => setPendingStatus({ id: c.id, name: c.name, activate: !c.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new currency
              </p>
              <ExcelToolbar
                exportDisabled={currencies.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "currencies.xlsx",
                    "Currencies",
                    [
                      { header: "Code", key: "code", value: (c: CurrencyDto) => c.code },
                      { header: "Name", key: "name", value: (c: CurrencyDto) => c.name },
                      { header: "Symbol", key: "symbol", value: (c: CurrencyDto) => c.symbol },
                      { header: "Decimals", key: "decimals", value: (c: CurrencyDto) => c.decimals },
                      { header: "Active", key: "active", value: (c: CurrencyDto) => activeLabel(c.isActive) },
                    ],
                    currencies,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Code</label>
                <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="MYR" maxLength={3} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Malaysian Ringgit" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Symbol</label>
                <input value={symbol} onChange={(e) => setSymbol(e.target.value)} placeholder="RM" />
              </div>
              <div className="field" style={{ margin: 0, maxWidth: 100 }}>
                <label>Decimals</label>
                <input value={decimals} onChange={(e) => setDecimals(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={addCurrency.isPending || !code.trim() || !name.trim() || !symbol.trim()}
                  onClick={() => addCurrency.mutate()}
                >
                  Add currency
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={currencies}
          columns={columns}
          rowKey={(c) => c.id}
          loading={currenciesQuery.isPending}
          loadingLabel="Loading currencies…"
          empty={<EmptyState icon="clip">No currencies yet.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          footer={<TableFooter total={currencies.length} active={currencies.filter((c) => c.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import currencies"
          description="Download the template, fill codes/names, then upload. Matching Code rows are updated."
          templateFileName="currencies-import-template.xlsx"
          templateSheetName="Currencies"
          templateHeaders={["Code", "Name", "Symbol", "Decimals", "Active"]}
          templateSampleRows={[
            { Code: "MYR", Name: "Malaysian Ringgit", Symbol: "RM", Decimals: 2, Active: "Yes" },
            { Code: "USD", Name: "US Dollar", Symbol: "$", Decimals: 2, Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>, <code>Symbol</code>
              <span className="hint"> · Optional: </span>
              <code>Decimals</code>, <code>Active</code> (Yes/No)
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["currencies"] })}
        />
      ) : null}

      {editing ? (
        <Modal
          title={`Edit currency · ${editing.code}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending || !editName.trim() || !editSymbol.trim()}
                onClick={() => saveEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>Name</label>
              <input value={editName} onChange={(e) => setEditName(e.target.value)} autoFocus />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Symbol</label>
              <input value={editSymbol} onChange={(e) => setEditSymbol(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0, maxWidth: 120 }}>
              <label>Decimals</label>
              <input value={editDecimals} onChange={(e) => setEditDecimals(e.target.value)} />
            </div>
          </div>
        </Modal>
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate currency" : "Deactivate currency"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggleCurrency.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} currency{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggleCurrency.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

// ==================== EXCHANGE RATES ====================

function RatesTab() {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [currencyCode, setCurrencyCode] = useState("");
  const [rate, setRate] = useState("");
  const [effectiveDate, setEffectiveDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [historyCode, setHistoryCode] = useState<string | null>(null);

  const ratesQuery = useQuery({ queryKey: ["exchange-rates-current"], queryFn: listCurrentExchangeRates });
  const currenciesQuery = useQuery({ queryKey: ["currencies", true], queryFn: () => listCurrencies(true) });
  const rates = ratesQuery.data ?? [];
  const currencies = currenciesQuery.data ?? [];

  const append = useMutation({
    mutationFn: () =>
      appendExchangeRate({
        currencyCode: currencyCode.trim().toUpperCase(),
        rateToBase: Number(rate),
        effectiveDate,
      }),
    onSuccess: () => {
      setRate("");
      void qc.invalidateQueries({ queryKey: ["exchange-rates-current"] });
    },
    onError: (e) => showErrorFrom(e, "Could not append rate"),
  });

  const runImport: ExcelImportRunner = async (file, onProgress) =>
    runExcelRows(file, onProgress, async (row) => {
      const cc = cellStr(row, "Currency", "CurrencyCode", "Code").toUpperCase();
      const rateRaw = cellStr(row, "RateToBase", "Rate");
      const dateRaw = cellStr(row, "EffectiveDate", "Date");
      if (!cc) throw new Error("Currency is required.");
      const rateVal = Number(rateRaw);
      if (!rateRaw || Number.isNaN(rateVal)) throw new Error("RateToBase must be a number.");
      if (!dateRaw) throw new Error("EffectiveDate is required.");
      await appendExchangeRate({ currencyCode: cc, rateToBase: rateVal, effectiveDate: dateRaw });
    });

  const columns: DataTableColumn<ExchangeRateCurrentDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Currency",
        sortable: true,
        sortValue: (r) => r.currencyCode,
        render: (r) => <span style={{ fontWeight: 600 }}>{r.currencyCode}</span>,
      },
      { id: "name", header: "Name", sortable: true, sortValue: (r) => r.currencyName, render: (r) => r.currencyName },
      {
        id: "rate",
        header: "Rate to base",
        align: "right",
        sortable: true,
        sortValue: (r) => r.rateToBase ?? 0,
        render: (r) => (r.isBaseCurrency ? <span className="hint">Base</span> : r.rateToBase != null ? r.rateToBase.toFixed(6) : "—"),
      },
      {
        id: "effective",
        header: "Effective",
        sortable: true,
        sortValue: (r) => r.effectiveDate ?? "",
        render: (r) => (r.isBaseCurrency ? "—" : dateMY(r.effectiveDate)),
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (r) => (
          <div className="row-actions">
            <IconButton icon="clock" label={`History · ${r.currencyCode}`} onClick={() => setHistoryCode(r.currencyCode)} />
          </div>
        ),
      },
    ],
    [],
  );

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Append exchange rate
              </p>
              <ExcelToolbar
                exportDisabled={rates.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "exchange-rates-current.xlsx",
                    "Rates",
                    [
                      { header: "Currency", key: "code", value: (r: ExchangeRateCurrentDto) => r.currencyCode },
                      { header: "Name", key: "name", value: (r: ExchangeRateCurrentDto) => r.currencyName },
                      { header: "RateToBase", key: "rate", value: (r: ExchangeRateCurrentDto) => r.rateToBase ?? "" },
                      { header: "EffectiveDate", key: "effective", value: (r: ExchangeRateCurrentDto) => r.effectiveDate ?? "" },
                      { header: "IsBase", key: "base", value: (r: ExchangeRateCurrentDto) => activeLabel(r.isBaseCurrency) },
                    ],
                    rates,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Currency</label>
                <select value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value)}>
                  <option value="">Select…</option>
                  {currencies.map((c) => (
                    <option key={c.code} value={c.code}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Rate to base</label>
                <input value={rate} onChange={(e) => setRate(e.target.value)} placeholder="4.250000" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Effective date</label>
                <input type="date" value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={append.isPending || !currencyCode.trim() || !rate.trim() || !effectiveDate}
                  onClick={() => append.mutate()}
                >
                  Append rate
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={rates}
          columns={columns}
          rowKey={(r) => r.currencyCode}
          loading={ratesQuery.isPending}
          loadingLabel="Loading exchange rates…"
          empty={<EmptyState icon="chart">No currencies configured.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          footer={
            <p className="hint" style={{ margin: "10px 0 0", textAlign: "right" }}>
              {rates.length} currencies
            </p>
          }
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import exchange rates"
          description="Download the template, fill currency/rate/date rows, then upload. Each row appends a new rate entry — history is preserved."
          templateFileName="exchange-rates-import-template.xlsx"
          templateSheetName="Rates"
          templateHeaders={["Currency", "RateToBase", "EffectiveDate"]}
          templateSampleRows={[
            { Currency: "USD", RateToBase: 0.235, EffectiveDate: "2026-07-01" },
            { Currency: "SGD", RateToBase: 0.317, EffectiveDate: "2026-07-01" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Currency</code>, <code>RateToBase</code>, <code>EffectiveDate</code> (YYYY-MM-DD)
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["exchange-rates-current"] })}
        />
      ) : null}

      {historyCode ? <RateHistoryModal currencyCode={historyCode} onClose={() => setHistoryCode(null)} /> : null}
    </>
  );
}

function RateHistoryModal({ currencyCode, onClose }: { currencyCode: string; onClose: () => void }) {
  const historyQuery = useQuery({
    queryKey: ["exchange-rate-history", currencyCode],
    queryFn: () => listExchangeRateHistory(currencyCode),
  });
  const rows: ExchangeRateDto[] = historyQuery.data ?? [];
  const sorted = useMemo(() => [...rows].sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate)), [rows]);

  return (
    <Modal
      title={`Rate history · ${currencyCode}`}
      icon="clock"
      footer={
        <button type="button" className="btn btn-out" onClick={onClose}>
          Close
        </button>
      }
    >
      {historyQuery.isPending ? <Spinner label="Loading history…" /> : null}
      {!historyQuery.isPending && sorted.length === 0 ? (
        <EmptyState icon="clock">No rate history for {currencyCode} yet.</EmptyState>
      ) : null}
      {sorted.length > 0 ? (
        <div className="table-scroll" style={{ maxHeight: "55vh" }}>
          <table>
            <thead>
              <tr>
                <th>Rate to base</th>
                <th>Effective</th>
                <th>Recorded</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((r) => (
                <tr key={r.id}>
                  <td>{r.rateToBase.toFixed(6)}</td>
                  <td>{dateMY(r.effectiveDate)}</td>
                  <td className="hint">{dateTimeMY(r.createdOnUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </Modal>
  );
}

// ==================== TAX CODES ====================

function TaxTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [ratePct, setRatePct] = useState("");
  const [editing, setEditing] = useState<TaxCodeDto | null>(null);
  const [editName, setEditName] = useState("");
  const [editRatePct, setEditRatePct] = useState("");

  const query = useQuery({ queryKey: ["tax-codes", false], queryFn: () => listTaxCodes(false) });
  const rowsData = query.data ?? [];

  const add = useMutation({
    mutationFn: () => createTaxCode({ code: code.trim().toUpperCase(), name: name.trim(), ratePct: Number(ratePct) || 0 }),
    onSuccess: () => {
      setCode("");
      setName("");
      setRatePct("");
      void qc.invalidateQueries({ queryKey: ["tax-codes"] });
    },
    onError: (e) => showErrorFrom(e, "Could not add tax code"),
  });

  const saveEdit = useMutation({
    mutationFn: () => updateTaxCode(editing!.id, { name: editName.trim(), ratePct: Number(editRatePct) || 0 }),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["tax-codes"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save tax code"),
  });

  const toggle = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setTaxCodeActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["tax-codes"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update tax code"),
  });

  const runImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listTaxCodes(false);
    const byCode = new Map(existing.map((t) => [t.code.toUpperCase(), t]));

    return runExcelRows(file, onProgress, async (row) => {
      const rowCode = cellStr(row, "Code").toUpperCase();
      const rowName = cellStr(row, "Name");
      const rateRaw = cellStr(row, "RatePct", "Rate");
      const active = cellActive(row);
      if (!rowCode) throw new Error("Code is required.");
      if (!rowName) throw new Error("Name is required.");

      const hit = byCode.get(rowCode);
      const rateVal = rateRaw ? Number(rateRaw) : hit?.ratePct ?? 0;
      if (Number.isNaN(rateVal)) throw new Error("RatePct must be a number.");

      if (hit) {
        await updateTaxCode(hit.id, { name: rowName, ratePct: rateVal });
        if (active != null && active !== hit.isActive) await setTaxCodeActive(hit.id, active);
        byCode.set(rowCode, { ...hit, name: rowName, ratePct: rateVal, isActive: active ?? hit.isActive });
      } else {
        const id = await createTaxCode({ code: rowCode, name: rowName, ratePct: rateVal });
        if (active === false) await setTaxCodeActive(id, false);
        byCode.set(rowCode, { id, code: rowCode, name: rowName, ratePct: rateVal, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const columns: DataTableColumn<TaxCodeDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (t) => t.code,
        render: (t) => <span style={{ fontWeight: 600 }}>{t.code}</span>,
      },
      { id: "name", header: "Name", sortable: true, sortValue: (t) => t.name, render: (t) => t.name },
      {
        id: "rate",
        header: "Rate %",
        align: "right",
        sortable: true,
        sortValue: (t) => t.ratePct,
        render: (t) => `${t.ratePct}%`,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (t) => (t.isActive ? 1 : 0),
        render: (t) => <StatusBadge active={t.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (t) => Date.parse(t.createdOnUtc) || 0,
        render: (t) => <span className="hint">{dateTimeMY(t.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (t) => (
          <div className="row-actions">
            <HistoryButton label={t.name} entityId={t.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(t);
                  setEditName(t.name);
                  setEditRatePct(String(t.ratePct));
                }}
              />
              <IconButton
                icon={t.isActive ? "x" : "check"}
                label={t.isActive ? "Deactivate" : "Activate"}
                danger={t.isActive}
                onClick={() => setPendingStatus({ id: t.id, name: t.name, activate: !t.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new tax code
              </p>
              <ExcelToolbar
                exportDisabled={rowsData.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "tax-codes.xlsx",
                    "TaxCodes",
                    [
                      { header: "Code", key: "code", value: (t: TaxCodeDto) => t.code },
                      { header: "Name", key: "name", value: (t: TaxCodeDto) => t.name },
                      { header: "RatePct", key: "rate", value: (t: TaxCodeDto) => t.ratePct },
                      { header: "Active", key: "active", value: (t: TaxCodeDto) => activeLabel(t.isActive) },
                    ],
                    rowsData,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Code</label>
                <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="SST" maxLength={10} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Sales & Service Tax" />
              </div>
              <div className="field" style={{ margin: 0, maxWidth: 120 }}>
                <label>Rate %</label>
                <input value={ratePct} onChange={(e) => setRatePct(e.target.value)} placeholder="8" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={add.isPending || !code.trim() || !name.trim() || !ratePct.trim()}
                  onClick={() => add.mutate()}
                >
                  Add tax code
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={rowsData}
          columns={columns}
          rowKey={(t) => t.id}
          loading={query.isPending}
          loadingLabel="Loading tax codes…"
          empty={<EmptyState icon="hash">No tax codes yet.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          footer={<TableFooter total={rowsData.length} active={rowsData.filter((t) => t.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import tax codes"
          description="Download the template, fill codes/rates, then upload. Matching Code rows are updated."
          templateFileName="tax-codes-import-template.xlsx"
          templateSheetName="TaxCodes"
          templateHeaders={["Code", "Name", "RatePct", "Active"]}
          templateSampleRows={[
            { Code: "SST", Name: "Sales & Service Tax", RatePct: 8, Active: "Yes" },
            { Code: "ZR", Name: "Zero-rated", RatePct: 0, Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>
              <span className="hint"> · Optional: </span>
              <code>RatePct</code>, <code>Active</code> (Yes/No)
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["tax-codes"] })}
        />
      ) : null}

      {editing ? (
        <Modal
          title={`Edit tax code · ${editing.code}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending || !editName.trim() || !editRatePct.trim()}
                onClick={() => saveEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>Name</label>
              <input value={editName} onChange={(e) => setEditName(e.target.value)} autoFocus />
            </div>
            <div className="field" style={{ margin: 0, maxWidth: 120 }}>
              <label>Rate %</label>
              <input value={editRatePct} onChange={(e) => setEditRatePct(e.target.value)} />
            </div>
          </div>
        </Modal>
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate tax code" : "Deactivate tax code"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggle.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} tax code{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggle.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

// ==================== PAYMENT TERMS ====================

const KIND_OPTIONS = ["Net", "DateDriven", "Schedule"] as const;
const BASIS_OPTIONS = ["Advance", "DaysFromDoc", "MilestoneLabel"] as const;

type PaymentRowForm = { seq: string; percent: string; basis: string; days: string; label: string };

function blankPaymentRow(seq: number): PaymentRowForm {
  return { seq: String(seq), percent: "", basis: "Advance", days: "", label: "" };
}

function PaymentTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState<"create" | PaymentTermDto | null>(null);
  const [previewBaseDate, setPreviewBaseDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [preview, setPreview] = useState<ScheduleInstalmentDto[] | null>(null);

  const listQuery = useQuery({ queryKey: ["payment-terms", false], queryFn: () => listPaymentTerms(false) });
  const terms = listQuery.data ?? [];

  const detailQuery = useQuery({
    queryKey: ["payment-term", selectedId],
    queryFn: () => getPaymentTerm(selectedId!),
    enabled: !!selectedId,
  });

  const toggle = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setPaymentTermActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["payment-terms"] });
      void qc.invalidateQueries({ queryKey: ["payment-term"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update payment term"),
  });

  const previewMut = useMutation({
    mutationFn: () => computePaymentSchedule(selectedId!, previewBaseDate),
    onSuccess: (rows) => setPreview(rows),
    onError: (e) => {
      setPreview(null);
      showErrorFrom(e, "Could not compute schedule");
    },
  });

  const runImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listPaymentTerms(false);
    const byCode = new Map(existing.map((t) => [t.code.toUpperCase(), t]));

    return runExcelRows(file, onProgress, async (row) => {
      const code = cellStr(row, "Code").toUpperCase();
      const name = cellStr(row, "Name");
      const kind = cellStr(row, "Kind");
      const active = cellActive(row);
      if (!code) throw new Error("Code is required.");
      if (!name) throw new Error("Name is required.");
      if (!(KIND_OPTIONS as readonly string[]).includes(kind)) {
        throw new Error(`Kind must be one of ${KIND_OPTIONS.join(", ")}.`);
      }
      if (kind === "Schedule") throw new Error("Schedule terms must be created in UI.");

      const dueDays = numOrNull(cellStr(row, "DueDays"));
      const dayOfMonth = numOrNull(cellStr(row, "DayOfMonth"));
      const monthsAhead = numOrNull(cellStr(row, "MonthsAhead"));
      const minimumDaysBeforeDue = numOrNull(cellStr(row, "MinimumDaysBeforeDue"));
      const discountPct = numOrNull(cellStr(row, "DiscountPct"));
      const discountDays = numOrNull(cellStr(row, "DiscountDays"));

      const hit = byCode.get(code);
      const shared = { name, kind, dueDays, dayOfMonth, monthsAhead, minimumDaysBeforeDue, discountPct, discountDays, rows: null };
      if (hit) {
        await updatePaymentTerm(hit.id, shared);
        if (active != null && active !== hit.isActive) await setPaymentTermActive(hit.id, active);
      } else {
        const id = await createPaymentTerm({ code, ...shared });
        if (active === false) await setPaymentTermActive(id, false);
      }
    });
  };

  const columns: DataTableColumn<PaymentTermDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (t) => t.code,
        render: (t) => <span style={{ fontWeight: 600 }}>{t.code}</span>,
      },
      { id: "name", header: "Name", sortable: true, sortValue: (t) => t.name, render: (t) => t.name },
      {
        id: "kind",
        header: "Kind",
        sortable: true,
        sortValue: (t) => t.kind,
        render: (t) => <span className="hint">{t.kind}</span>,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (t) => (t.isActive ? 1 : 0),
        render: (t) => <StatusBadge active={t.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (t) => Date.parse(t.createdOnUtc) || 0,
        render: (t) => <span className="hint">{dateTimeMY(t.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (t) => (
          <div className="row-actions">
            <IconButton
              icon="chev"
              label={`Open ${t.name}`}
              onClick={() => {
                setSelectedId(t.id);
                setPreview(null);
              }}
            />
            <HistoryButton label={t.name} entityId={t.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton icon="edit" label="Edit" onClick={() => setFormOpen(t)} />
              <IconButton
                icon={t.isActive ? "x" : "check"}
                label={t.isActive ? "Deactivate" : "Activate"}
                danger={t.isActive}
                onClick={() => setPendingStatus({ id: t.id, name: t.name, activate: !t.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  const formModal = formOpen ? (
    <PaymentTermFormModal
      initial={formOpen === "create" ? null : formOpen}
      onClose={() => setFormOpen(null)}
      onSaved={() => {
        setFormOpen(null);
        void qc.invalidateQueries({ queryKey: ["payment-terms"] });
        void qc.invalidateQueries({ queryKey: ["payment-term", selectedId] });
      }}
    />
  ) : null;

  if (selectedId) {
    const term = detailQuery.data;
    return (
      <>
        <DetailHeader
          title={term?.name ?? "…"}
          subtitle={term ? `Payment term · ${term.code} · ${term.kind}` : undefined}
          onBack={() => {
            setSelectedId(null);
            setPreview(null);
          }}
          backLabel="All payment terms"
        />

        {detailQuery.isPending ? <Spinner label="Loading payment term…" /> : null}

        {term ? (
          <>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="cbody">
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                  <p className="hint" style={{ margin: 0, fontWeight: 600 }}>
                    Term configuration
                  </p>
                  <Gated permission={FshPermissions.configuration.manage}>
                    <button type="button" className="btn btn-out btn-sm" onClick={() => setFormOpen(term)}>
                      <Icon name="edit" size={14} /> Edit
                    </button>
                  </Gated>
                </div>
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 12 }}>
                  <Field label="Kind" value={term.kind} />
                  <Field label="Status" value={term.isActive ? "Active" : "Inactive"} />
                  {term.kind === "Net" ? <Field label="Due days" value={String(term.dueDays ?? "—")} /> : null}
                  {term.kind === "DateDriven" ? (
                    <>
                      <Field label="Day of month" value={String(term.dayOfMonth ?? "—")} />
                      <Field label="Months ahead" value={String(term.monthsAhead ?? "—")} />
                      <Field label="Min days before due" value={String(term.minimumDaysBeforeDue ?? "—")} />
                    </>
                  ) : null}
                  {term.discountPct != null ? (
                    <Field label="Early-pay discount" value={`${term.discountPct}% if paid within ${term.discountDays} days`} />
                  ) : null}
                </div>
              </div>
            </div>

            {term.kind === "Schedule" ? (
              <div className="card" style={{ marginBottom: 14 }}>
                <div className="cbody">
                  <p className="hint" style={{ margin: "0 0 10px", fontWeight: 600 }}>
                    Schedule rows
                  </p>
                  <div className="table-scroll">
                    <table>
                      <thead>
                        <tr>
                          <th>Seq</th>
                          <th>Percent</th>
                          <th>Basis</th>
                          <th>Days</th>
                          <th>Label</th>
                        </tr>
                      </thead>
                      <tbody>
                        {[...term.rows]
                          .sort((a, b) => a.seq - b.seq)
                          .map((r) => (
                            <tr key={r.id}>
                              <td>{r.seq}</td>
                              <td className="amt">{r.percent}%</td>
                              <td>{r.basis}</td>
                              <td>{r.days ?? "—"}</td>
                              <td>{r.label ?? "—"}</td>
                            </tr>
                          ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            ) : null}

            <div className="card">
              <div className="cbody">
                <p className="hint" style={{ margin: "0 0 10px", fontWeight: 600 }}>
                  Preview schedule
                </p>
                <div className="filterbar filterbar-auto">
                  <div className="field" style={{ margin: 0 }}>
                    <label>Base date</label>
                    <input type="date" value={previewBaseDate} onChange={(e) => setPreviewBaseDate(e.target.value)} />
                  </div>
                  <div className="field" style={{ margin: 0 }}>
                    <label aria-hidden="true">&nbsp;</label>
                    <button
                      type="button"
                      className="btn btn-pri btn-sm"
                      disabled={previewMut.isPending || !previewBaseDate}
                      onClick={() => previewMut.mutate()}
                    >
                      Preview schedule
                    </button>
                  </div>
                </div>

                {preview ? (
                  <div className="table-scroll" style={{ marginTop: 12 }}>
                    <table>
                      <thead>
                        <tr>
                          <th>Due date</th>
                          <th>Percent</th>
                          <th>Discount date</th>
                          <th>Discount %</th>
                          <th>Label</th>
                        </tr>
                      </thead>
                      <tbody>
                        {preview.map((inst, i) => (
                          <tr key={i}>
                            <td>{inst.dueDate ? dateMY(inst.dueDate) : "—"}</td>
                            <td className="amt">{inst.percent}%</td>
                            <td>{inst.discountDate ? dateMY(inst.discountDate) : "—"}</td>
                            <td className="amt">{inst.discountPct != null ? `${inst.discountPct}%` : "—"}</td>
                            <td>{inst.label ?? "—"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : null}
              </div>
            </div>
          </>
        ) : null}

        {formModal}
      </>
    );
  }

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Manage payment terms
              </p>
              <ExcelToolbar
                exportDisabled={terms.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "payment-terms.xlsx",
                    "PaymentTerms",
                    [
                      { header: "Code", key: "code", value: (t: PaymentTermDto) => t.code },
                      { header: "Name", key: "name", value: (t: PaymentTermDto) => t.name },
                      { header: "Kind", key: "kind", value: (t: PaymentTermDto) => t.kind },
                      { header: "DueDays", key: "dueDays", value: (t: PaymentTermDto) => t.dueDays ?? "" },
                      { header: "DayOfMonth", key: "dom", value: (t: PaymentTermDto) => t.dayOfMonth ?? "" },
                      { header: "MonthsAhead", key: "ma", value: (t: PaymentTermDto) => t.monthsAhead ?? "" },
                      { header: "MinimumDaysBeforeDue", key: "mindays", value: (t: PaymentTermDto) => t.minimumDaysBeforeDue ?? "" },
                      { header: "DiscountPct", key: "dpct", value: (t: PaymentTermDto) => t.discountPct ?? "" },
                      { header: "DiscountDays", key: "ddays", value: (t: PaymentTermDto) => t.discountDays ?? "" },
                      { header: "Active", key: "active", value: (t: PaymentTermDto) => activeLabel(t.isActive) },
                    ],
                    terms,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
              <button type="button" className="btn btn-pri btn-sm" onClick={() => setFormOpen("create")}>
                <Icon name="plus" size={14} /> New payment term
              </button>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={terms}
          columns={columns}
          rowKey={(t) => t.id}
          loading={listQuery.isPending}
          loadingLabel="Loading payment terms…"
          empty={<EmptyState icon="send">No payment terms yet.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          onRowClick={(t) => {
            setSelectedId(t.id);
            setPreview(null);
          }}
          footer={<TableFooter total={terms.length} active={terms.filter((t) => t.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import payment terms"
          description="Download the template, fill Net/DateDriven terms, then upload. Matching Code rows are updated. Schedule-kind terms must be created in the UI."
          templateFileName="payment-terms-import-template.xlsx"
          templateSheetName="PaymentTerms"
          templateHeaders={[
            "Code",
            "Name",
            "Kind",
            "DueDays",
            "DayOfMonth",
            "MonthsAhead",
            "MinimumDaysBeforeDue",
            "DiscountPct",
            "DiscountDays",
            "Active",
          ]}
          templateSampleRows={[
            {
              Code: "NET45",
              Name: "Net 45 days",
              Kind: "Net",
              DueDays: 45,
              DayOfMonth: "",
              MonthsAhead: "",
              MinimumDaysBeforeDue: "",
              DiscountPct: "",
              DiscountDays: "",
              Active: "Yes",
            },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>, <code>Kind</code> (Net/DateDriven)
              <span className="hint"> · Optional: </span>
              <code>DueDays</code>, <code>DayOfMonth</code>, <code>MonthsAhead</code>, <code>MinimumDaysBeforeDue</code>,{" "}
              <code>DiscountPct</code>, <code>DiscountDays</code>, <code>Active</code>
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["payment-terms"] })}
        />
      ) : null}

      {formModal}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate payment term" : "Deactivate payment term"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggle.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} payment term{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggle.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

function PaymentTermFormModal({
  initial,
  onClose,
  onSaved,
}: {
  initial: PaymentTermDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { showErrorFrom } = useErrorDialog();
  const isEdit = !!initial;
  const [code, setCode] = useState(initial?.code ?? "");
  const [name, setName] = useState(initial?.name ?? "");
  const [kind, setKind] = useState<string>(initial?.kind ?? "Net");
  const [dueDays, setDueDays] = useState(initial?.dueDays != null ? String(initial.dueDays) : "");
  const [dayOfMonth, setDayOfMonth] = useState(initial?.dayOfMonth != null ? String(initial.dayOfMonth) : "");
  const [monthsAhead, setMonthsAhead] = useState(initial?.monthsAhead != null ? String(initial.monthsAhead) : "");
  const [minimumDaysBeforeDue, setMinimumDaysBeforeDue] = useState(
    initial?.minimumDaysBeforeDue != null ? String(initial.minimumDaysBeforeDue) : "",
  );
  const [discountPct, setDiscountPct] = useState(initial?.discountPct != null ? String(initial.discountPct) : "");
  const [discountDays, setDiscountDays] = useState(initial?.discountDays != null ? String(initial.discountDays) : "");
  const [rows, setRows] = useState<PaymentRowForm[]>(
    initial && initial.rows.length > 0
      ? initial.rows.map((r) => ({
          seq: String(r.seq),
          percent: String(r.percent),
          basis: r.basis,
          days: r.days != null ? String(r.days) : "",
          label: r.label ?? "",
        }))
      : [blankPaymentRow(1)],
  );

  const save = useMutation({
    mutationFn: () => {
      const rowsInput: PaymentScheduleRowInput[] | null =
        kind === "Schedule"
          ? rows.map((r) => ({
              seq: Number(r.seq) || 0,
              percent: Number(r.percent) || 0,
              basis: r.basis,
              days: r.basis === "DaysFromDoc" ? numOrNull(r.days) : null,
              label: r.basis === "MilestoneLabel" ? r.label.trim() || null : null,
            }))
          : null;
      const shared = {
        name: name.trim(),
        kind,
        dueDays: kind === "Net" ? numOrNull(dueDays) : null,
        dayOfMonth: kind === "DateDriven" ? numOrNull(dayOfMonth) : null,
        monthsAhead: kind === "DateDriven" ? numOrNull(monthsAhead) : null,
        minimumDaysBeforeDue: kind === "DateDriven" ? numOrNull(minimumDaysBeforeDue) : null,
        discountPct: kind !== "Schedule" ? numOrNull(discountPct) : null,
        discountDays: kind !== "Schedule" ? numOrNull(discountDays) : null,
        rows: rowsInput,
      };
      return isEdit ? updatePaymentTerm(initial!.id, shared) : createPaymentTerm({ code: code.trim().toUpperCase(), ...shared });
    },
    onSuccess: onSaved,
    onError: (e) => showErrorFrom(e, isEdit ? "Could not save payment term" : "Could not create payment term"),
  });

  const rowsValid =
    kind !== "Schedule" ||
    rows.every(
      (r) =>
        r.percent.trim() !== "" &&
        (r.basis !== "DaysFromDoc" || r.days.trim() !== "") &&
        (r.basis !== "MilestoneLabel" || r.label.trim() !== ""),
    );

  const canSave =
    name.trim().length > 0 &&
    (isEdit || code.trim().length > 0) &&
    (kind !== "Net" || dueDays.trim() !== "") &&
    (kind !== "DateDriven" || (dayOfMonth.trim() !== "" && monthsAhead.trim() !== "")) &&
    rowsValid;

  return (
    <Modal
      title={isEdit ? `Edit payment term · ${initial!.code}` : "New payment term"}
      icon="edit"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn-pri" disabled={save.isPending || !canSave} onClick={() => save.mutate()}>
            Save
          </button>
        </>
      }
    >
      <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
        <div className="field" style={{ margin: 0 }}>
          <label>Code</label>
          <input
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            disabled={isEdit}
            maxLength={20}
            placeholder="NET45"
          />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Net 45 days" />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label>Kind</label>
          <select value={kind} onChange={(e) => setKind(e.target.value)}>
            {KIND_OPTIONS.map((k) => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
          </select>
        </div>
      </div>

      {kind === "Net" ? (
        <div className="filterbar filterbar-auto" style={{ gap: 12, marginTop: 12 }}>
          <div className="field" style={{ margin: 0 }}>
            <label>Due days</label>
            <input value={dueDays} onChange={(e) => setDueDays(e.target.value)} placeholder="30" />
          </div>
          <div className="field" style={{ margin: 0 }}>
            <label>Discount % (optional)</label>
            <input value={discountPct} onChange={(e) => setDiscountPct(e.target.value)} placeholder="2" />
          </div>
          <div className="field" style={{ margin: 0 }}>
            <label>Discount days (optional)</label>
            <input value={discountDays} onChange={(e) => setDiscountDays(e.target.value)} placeholder="10" />
          </div>
        </div>
      ) : null}

      {kind === "DateDriven" ? (
        <div className="filterbar filterbar-auto" style={{ gap: 12, marginTop: 12 }}>
          <div className="field" style={{ margin: 0 }}>
            <label>Day of month</label>
            <input value={dayOfMonth} onChange={(e) => setDayOfMonth(e.target.value)} placeholder="31" />
          </div>
          <div className="field" style={{ margin: 0 }}>
            <label>Months ahead</label>
            <input value={monthsAhead} onChange={(e) => setMonthsAhead(e.target.value)} placeholder="1" />
          </div>
          <div className="field" style={{ margin: 0 }}>
            <label>Min days before due (optional)</label>
            <input value={minimumDaysBeforeDue} onChange={(e) => setMinimumDaysBeforeDue(e.target.value)} placeholder="5" />
          </div>
          <div className="field" style={{ margin: 0 }}>
            <label>Discount % (optional)</label>
            <input value={discountPct} onChange={(e) => setDiscountPct(e.target.value)} />
          </div>
          <div className="field" style={{ margin: 0 }}>
            <label>Discount days (optional)</label>
            <input value={discountDays} onChange={(e) => setDiscountDays(e.target.value)} />
          </div>
        </div>
      ) : null}

      {kind === "Schedule" ? (
        <div style={{ marginTop: 12 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
            <label style={{ fontWeight: 600, fontSize: 13 }}>Schedule rows (percents must total 100)</label>
            <button
              type="button"
              className="btn btn-out btn-sm"
              onClick={() => setRows((rs) => [...rs, blankPaymentRow(rs.length + 1)])}
            >
              <Icon name="plus" size={12} /> Add row
            </button>
          </div>
          <div style={{ display: "grid", gap: 8 }}>
            {rows.map((r, i) => (
              <div key={i} style={{ display: "flex", gap: 8, alignItems: "flex-end", flexWrap: "wrap" }}>
                <div className="field" style={{ margin: 0, maxWidth: 70 }}>
                  <label>Seq</label>
                  <input value={r.seq} onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, seq: e.target.value } : x)))} />
                </div>
                <div className="field" style={{ margin: 0, maxWidth: 100 }}>
                  <label>Percent</label>
                  <input
                    value={r.percent}
                    onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, percent: e.target.value } : x)))}
                  />
                </div>
                <div className="field" style={{ margin: 0, maxWidth: 160 }}>
                  <label>Basis</label>
                  <select value={r.basis} onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, basis: e.target.value } : x)))}>
                    {BASIS_OPTIONS.map((b) => (
                      <option key={b} value={b}>
                        {b}
                      </option>
                    ))}
                  </select>
                </div>
                {r.basis === "DaysFromDoc" ? (
                  <div className="field" style={{ margin: 0, maxWidth: 100 }}>
                    <label>Days</label>
                    <input value={r.days} onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, days: e.target.value } : x)))} />
                  </div>
                ) : null}
                {r.basis === "MilestoneLabel" ? (
                  <div className="field" style={{ margin: 0 }}>
                    <label>Label</label>
                    <input value={r.label} onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, label: e.target.value } : x)))} />
                  </div>
                ) : null}
                {rows.length > 1 ? (
                  <button
                    type="button"
                    className="btn btn-out btn-sm btn-icon btn-danger-text"
                    title="Remove row"
                    onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))}
                  >
                    <Icon name="trash" size={14} />
                  </button>
                ) : null}
              </div>
            ))}
          </div>
          <p className="hint" style={{ marginTop: 8 }}>
            Total: {rows.reduce((sum, r) => sum + (Number(r.percent) || 0), 0)}%
          </p>
        </div>
      ) : null}
    </Modal>
  );
}

// ==================== INCOTERMS ====================

function IncotermsTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<IncotermDto | null>(null);
  const [editName, setEditName] = useState("");

  const query = useQuery({ queryKey: ["incoterms", false], queryFn: () => listIncoterms(false) });
  const rowsData = query.data ?? [];

  const add = useMutation({
    mutationFn: () => createIncoterm({ code: code.trim().toUpperCase(), name: name.trim() }),
    onSuccess: () => {
      setCode("");
      setName("");
      void qc.invalidateQueries({ queryKey: ["incoterms"] });
    },
    onError: (e) => showErrorFrom(e, "Could not add incoterm"),
  });

  const saveEdit = useMutation({
    mutationFn: () => updateIncoterm(editing!.id, { name: editName.trim() }),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["incoterms"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save incoterm"),
  });

  const toggle = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setIncotermActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["incoterms"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update incoterm"),
  });

  const runImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listIncoterms(false);
    const byCode = new Map(existing.map((i) => [i.code.toUpperCase(), i]));

    return runExcelRows(file, onProgress, async (row) => {
      const rowCode = cellStr(row, "Code").toUpperCase();
      const rowName = cellStr(row, "Name");
      const active = cellActive(row);
      if (!rowCode) throw new Error("Code is required.");
      if (!rowName) throw new Error("Name is required.");

      const hit = byCode.get(rowCode);
      if (hit) {
        await updateIncoterm(hit.id, { name: rowName });
        if (active != null && active !== hit.isActive) await setIncotermActive(hit.id, active);
        byCode.set(rowCode, { ...hit, name: rowName, isActive: active ?? hit.isActive });
      } else {
        const id = await createIncoterm({ code: rowCode, name: rowName });
        if (active === false) await setIncotermActive(id, false);
        byCode.set(rowCode, { id, code: rowCode, name: rowName, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const columns: DataTableColumn<IncotermDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (i) => i.code,
        render: (i) => <span style={{ fontWeight: 600 }}>{i.code}</span>,
      },
      { id: "name", header: "Name", sortable: true, sortValue: (i) => i.name, render: (i) => i.name },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (i) => (i.isActive ? 1 : 0),
        render: (i) => <StatusBadge active={i.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (i) => Date.parse(i.createdOnUtc) || 0,
        render: (i) => <span className="hint">{dateTimeMY(i.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (i) => (
          <div className="row-actions">
            <HistoryButton label={i.name} entityId={i.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(i);
                  setEditName(i.name);
                }}
              />
              <IconButton
                icon={i.isActive ? "x" : "check"}
                label={i.isActive ? "Deactivate" : "Activate"}
                danger={i.isActive}
                onClick={() => setPendingStatus({ id: i.id, name: i.name, activate: !i.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new incoterm
              </p>
              <ExcelToolbar
                exportDisabled={rowsData.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "incoterms.xlsx",
                    "Incoterms",
                    [
                      { header: "Code", key: "code", value: (i: IncotermDto) => i.code },
                      { header: "Name", key: "name", value: (i: IncotermDto) => i.name },
                      { header: "Active", key: "active", value: (i: IncotermDto) => activeLabel(i.isActive) },
                    ],
                    rowsData,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Code</label>
                <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="FOB" maxLength={5} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Free On Board" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={add.isPending || !code.trim() || !name.trim()}
                  onClick={() => add.mutate()}
                >
                  Add incoterm
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={rowsData}
          columns={columns}
          rowKey={(i) => i.id}
          loading={query.isPending}
          loadingLabel="Loading incoterms…"
          empty={<EmptyState icon="flag">No incoterms yet.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          footer={<TableFooter total={rowsData.length} active={rowsData.filter((i) => i.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import incoterms"
          description="Download the template, fill codes/names, then upload. Matching Code rows are updated."
          templateFileName="incoterms-import-template.xlsx"
          templateSheetName="Incoterms"
          templateHeaders={["Code", "Name", "Active"]}
          templateSampleRows={[
            { Code: "FOB", Name: "Free On Board", Active: "Yes" },
            { Code: "CIF", Name: "Cost, Insurance and Freight", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>
              <span className="hint"> · Optional: </span>
              <code>Active</code> (Yes/No)
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["incoterms"] })}
        />
      ) : null}

      {editing ? (
        <Modal
          title={`Edit incoterm · ${editing.code}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending || !editName.trim()}
                onClick={() => saveEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="field" style={{ margin: 0 }}>
            <label>Name</label>
            <input value={editName} onChange={(e) => setEditName(e.target.value)} autoFocus />
          </div>
        </Modal>
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate incoterm" : "Deactivate incoterm"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggle.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} incoterm{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggle.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

// ==================== LOCATIONS ====================

type AddressFormValues = {
  label: string;
  line1: string;
  line2: string;
  city: string;
  state: string;
  postcode: string;
  country: string;
  isDefault: boolean;
};

function blankAddressForm(isDefault: boolean): AddressFormValues {
  return { label: "", line1: "", line2: "", city: "", state: "", postcode: "", country: "MY", isDefault };
}

function toAddressInput(a: LocationAddressDto): LocationAddressInput {
  return {
    label: a.label,
    line1: a.line1,
    line2: a.line2 ?? null,
    city: a.city,
    state: a.state,
    postcode: a.postcode,
    country: a.country,
    isDefault: a.isDefault,
    sort: a.sort,
  };
}

function AddressFormModal({
  title,
  initial,
  busy,
  onClose,
  onSave,
}: {
  title: string;
  initial: AddressFormValues;
  busy: boolean;
  onClose: () => void;
  onSave: (values: AddressFormValues) => void;
}) {
  const [values, setValues] = useState<AddressFormValues>(initial);
  const set = <K extends keyof AddressFormValues>(key: K, value: AddressFormValues[K]) =>
    setValues((prev) => ({ ...prev, [key]: value }));
  const canSave = !!(values.label.trim() && values.line1.trim() && values.city.trim() && values.state.trim() && values.postcode.trim());

  return (
    <Modal
      title={title}
      icon="field"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn-pri" disabled={busy || !canSave} onClick={() => onSave(values)}>
            Save
          </button>
        </>
      }
    >
      <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
        <div className="field" style={{ margin: 0 }}>
          <label>Label</label>
          <input value={values.label} onChange={(e) => set("label", e.target.value)} placeholder="Head office" autoFocus />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label>Line 1</label>
          <input value={values.line1} onChange={(e) => set("line1", e.target.value)} />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label>Line 2 (optional)</label>
          <input value={values.line2} onChange={(e) => set("line2", e.target.value)} />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label>City</label>
          <input value={values.city} onChange={(e) => set("city", e.target.value)} />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label>State</label>
          <input value={values.state} onChange={(e) => set("state", e.target.value)} />
        </div>
        <div className="field" style={{ margin: 0, maxWidth: 120 }}>
          <label>Postcode</label>
          <input value={values.postcode} onChange={(e) => set("postcode", e.target.value)} />
        </div>
        <div className="field" style={{ margin: 0, maxWidth: 100 }}>
          <label>Country</label>
          <input value={values.country} onChange={(e) => set("country", e.target.value.toUpperCase())} maxLength={2} />
        </div>
        <div className="field" style={{ margin: 0 }}>
          <label aria-hidden="true">&nbsp;</label>
          <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <input type="checkbox" checked={values.isDefault} onChange={(e) => set("isDefault", e.target.checked)} style={{ width: "auto" }} />
            Default address
          </label>
        </div>
      </div>
    </Modal>
  );
}

function LocationsTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [address, setAddress] = useState<AddressFormValues>(() => blankAddressForm(true));
  const [editing, setEditing] = useState<LocationDto | null>(null);
  const [editName, setEditName] = useState("");
  const [addressModal, setAddressModal] = useState<{ mode: "add" } | { mode: "edit"; address: LocationAddressDto } | null>(null);
  const [pendingRemoveAddress, setPendingRemoveAddress] = useState<LocationAddressDto | null>(null);

  const listQuery = useQuery({ queryKey: ["locations", false], queryFn: () => listLocations(false) });
  const locations = listQuery.data ?? [];

  const detailQuery = useQuery({
    queryKey: ["location", selectedId],
    queryFn: () => getLocation(selectedId!),
    enabled: !!selectedId,
  });
  const location = detailQuery.data;

  const invalidateAll = () => {
    void qc.invalidateQueries({ queryKey: ["locations"] });
    void qc.invalidateQueries({ queryKey: ["location", selectedId] });
  };

  const create = useMutation({
    mutationFn: () =>
      createLocation({
        code: code.trim().toUpperCase(),
        name: name.trim(),
        addresses: [
          {
            label: address.label.trim(),
            line1: address.line1.trim(),
            line2: address.line2.trim() || null,
            city: address.city.trim(),
            state: address.state.trim(),
            postcode: address.postcode.trim(),
            country: address.country.trim() || "MY",
            isDefault: true,
            sort: 0,
          },
        ],
      }),
    onSuccess: () => {
      setCode("");
      setName("");
      setAddress(blankAddressForm(true));
      void qc.invalidateQueries({ queryKey: ["locations"] });
    },
    onError: (e) => showErrorFrom(e, "Could not create location"),
  });

  const saveNameEdit = useMutation({
    mutationFn: () => updateLocation(editing!.id, { name: editName.trim(), addresses: editing!.addresses.map(toAddressInput) }),
    onSuccess: () => {
      setEditing(null);
      invalidateAll();
    },
    onError: (e) => showErrorFrom(e, "Could not save location"),
  });

  const toggle = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setLocationActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      invalidateAll();
    },
    onError: (e) => showErrorFrom(e, "Could not update location"),
  });

  const saveAddresses = useMutation({
    mutationFn: (addresses: LocationAddressInput[]) => updateLocation(location!.id, { name: location!.name, addresses }),
    onSuccess: () => {
      setAddressModal(null);
      setPendingRemoveAddress(null);
      invalidateAll();
    },
    onError: (e) => showErrorFrom(e, "Could not save addresses"),
  });

  const runImport: ExcelImportRunner = async (file, onProgress) => {
    const rows = await parseExcelFile(file);
    const existing = await listLocations(false);
    const byCode = new Map(existing.map((l) => [l.code.toUpperCase(), l]));

    type GroupRow = { line: number; row: Record<string, unknown> };
    const groups = new Map<string, GroupRow[]>();
    const result: ExcelImportProgress = { total: rows.length, processed: 0, success: 0, errors: [] };
    const report = () => onProgress({ ...result, errors: [...result.errors] });
    report();

    rows.forEach((row, i) => {
      const line = i + 2;
      const rowCode = cellStr(row, "Code").toUpperCase();
      if (!rowCode) {
        result.errors.push({ line, message: "Code is required." });
        result.processed += 1;
        return;
      }
      const arr = groups.get(rowCode) ?? [];
      arr.push({ line, row });
      groups.set(rowCode, arr);
    });
    report();

    for (const [rowCode, groupRows] of groups) {
      try {
        const first = groupRows[0]!.row;
        const locName = cellStr(first, "Name");
        const active = cellActive(first);
        if (!locName) throw new Error("Name is required.");

        const addresses: LocationAddressInput[] = groupRows.map((gr, idx) => {
          const r = gr.row;
          const label = cellStr(r, "AddressLabel", "Label") || `Address ${idx + 1}`;
          const line1 = cellStr(r, "Line1");
          const line2 = cellStr(r, "Line2");
          const city = cellStr(r, "City");
          const state = cellStr(r, "State");
          const postcode = cellStr(r, "Postcode");
          const country = cellStr(r, "Country") || "MY";
          const isDefaultRaw = cellActive(r, "IsDefault");
          if (!line1) throw new Error(`Line1 is required for address "${label}".`);
          if (!city) throw new Error(`City is required for address "${label}".`);
          if (!state) throw new Error(`State is required for address "${label}".`);
          if (!postcode) throw new Error(`Postcode is required for address "${label}".`);
          return {
            label,
            line1,
            line2: line2 || null,
            city,
            state,
            postcode,
            country,
            isDefault: isDefaultRaw ?? idx === 0,
            sort: idx,
          };
        });
        if (!addresses.some((a) => a.isDefault)) addresses[0]!.isDefault = true;

        const hit = byCode.get(rowCode);
        if (hit) {
          await updateLocation(hit.id, { name: locName, addresses });
          if (active != null && active !== hit.isActive) await setLocationActive(hit.id, active);
        } else {
          const id = await createLocation({ code: rowCode, name: locName, addresses });
          if (active === false) await setLocationActive(id, false);
        }
        result.success += groupRows.length;
      } catch (e) {
        const message = formatApiError(e);
        for (const gr of groupRows) result.errors.push({ line: gr.line, message });
      }
      result.processed += groupRows.length;
      report();
    }

    return result;
  };

  const columns: DataTableColumn<LocationDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (l) => l.code,
        render: (l) => <span style={{ fontWeight: 600 }}>{l.code}</span>,
      },
      { id: "name", header: "Name", sortable: true, sortValue: (l) => l.name, render: (l) => l.name },
      {
        id: "addrs",
        header: "Addresses",
        align: "right",
        sortable: true,
        sortValue: (l) => l.addresses.length,
        render: (l) => l.addresses.length,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (l) => (l.isActive ? 1 : 0),
        render: (l) => <StatusBadge active={l.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (l) => Date.parse(l.createdOnUtc) || 0,
        render: (l) => <span className="hint">{dateTimeMY(l.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (l) => (
          <div className="row-actions">
            <IconButton icon="chev" label={`Open ${l.name}`} onClick={() => setSelectedId(l.id)} />
            <HistoryButton label={l.name} entityId={l.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(l);
                  setEditName(l.name);
                }}
              />
              <IconButton
                icon={l.isActive ? "x" : "check"}
                label={l.isActive ? "Deactivate" : "Activate"}
                danger={l.isActive}
                onClick={() => setPendingStatus({ id: l.id, name: l.name, activate: !l.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  if (selectedId) {
    const addrColumns: DataTableColumn<LocationAddressDto>[] = [
      { id: "label", header: "Label", render: (a) => <span style={{ fontWeight: 600 }}>{a.label}</span> },
      {
        id: "address",
        header: "Address",
        render: (a) => `${a.line1}${a.line2 ? ", " + a.line2 : ""}, ${a.city}, ${a.state} ${a.postcode}, ${a.country}`,
      },
      {
        id: "default",
        header: "Default",
        interactive: true,
        render: (a) =>
          a.isDefault ? (
            <span className="badge b-green">Default</span>
          ) : (
            <Gated permission={FshPermissions.configuration.manage}>
              <button
                type="button"
                className="btn btn-out btn-sm"
                onClick={() =>
                  location &&
                  saveAddresses.mutate(location.addresses.map((x) => toAddressInput({ ...x, isDefault: x.id === a.id })))
                }
              >
                Set default
              </button>
            </Gated>
          ),
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (a) => (
          <Gated permission={FshPermissions.configuration.manage}>
            <div className="row-actions">
              <IconButton icon="edit" label="Edit address" onClick={() => setAddressModal({ mode: "edit", address: a })} />
              <IconButton icon="trash" label="Remove address" danger onClick={() => setPendingRemoveAddress(a)} />
            </div>
          </Gated>
        ),
      },
    ];

    return (
      <>
        <DetailHeader
          title={location?.name ?? "…"}
          subtitle={location ? `Location · ${location.code}` : undefined}
          onBack={() => setSelectedId(null)}
          backLabel="All locations"
        />

        {detailQuery.isPending ? <Spinner label="Loading location…" /> : null}

        {location ? (
          <>
            <Gated permission={FshPermissions.configuration.manage}>
              <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: 12 }}>
                <button type="button" className="btn btn-pri btn-sm" onClick={() => setAddressModal({ mode: "add" })}>
                  <Icon name="plus" size={14} /> Add address
                </button>
              </div>
            </Gated>
            <div className="card">
              <DataTable
                rows={location.addresses}
                columns={addrColumns}
                rowKey={(a) => a.id}
                empty={<EmptyState icon="field">No addresses yet — add the first address above.</EmptyState>}
                initialSort={{ id: "label", dir: "asc" }}
              />
            </div>
          </>
        ) : null}

        {addressModal ? (
          <AddressFormModal
            title={addressModal.mode === "add" ? "Add address" : `Edit address · ${addressModal.address.label}`}
            initial={
              addressModal.mode === "add"
                ? blankAddressForm(!location || location.addresses.length === 0)
                : {
                    label: addressModal.address.label,
                    line1: addressModal.address.line1,
                    line2: addressModal.address.line2 ?? "",
                    city: addressModal.address.city,
                    state: addressModal.address.state,
                    postcode: addressModal.address.postcode,
                    country: addressModal.address.country,
                    isDefault: addressModal.address.isDefault,
                  }
            }
            busy={saveAddresses.isPending}
            onClose={() => setAddressModal(null)}
            onSave={(values) => {
              if (!location) return;
              const modal = addressModal;
              const input: LocationAddressInput = {
                label: values.label.trim(),
                line1: values.line1.trim(),
                line2: values.line2.trim() || null,
                city: values.city.trim(),
                state: values.state.trim(),
                postcode: values.postcode.trim(),
                country: values.country.trim() || "MY",
                isDefault: values.isDefault,
                sort: modal.mode === "edit" ? modal.address.sort : location.addresses.length,
              };
              let addresses: LocationAddressInput[] =
                modal.mode === "add"
                  ? [...location.addresses.map(toAddressInput), input]
                  : location.addresses.map((a) => (a.id === modal.address.id ? input : toAddressInput(a)));
              if (input.isDefault) addresses = addresses.map((a) => ({ ...a, isDefault: a === input }));
              if (!addresses.some((a) => a.isDefault)) addresses[0]!.isDefault = true;
              saveAddresses.mutate(addresses);
            }}
          />
        ) : null}

        {pendingRemoveAddress ? (
          <ConfirmModal
            title="Remove address"
            icon="x"
            danger
            busy={saveAddresses.isPending}
            confirmLabel="Remove"
            body={
              <p className="hint" style={{ marginTop: 0 }}>
                Remove address <strong>{pendingRemoveAddress.label}</strong> from {location?.name}?
              </p>
            }
            onCancel={() => setPendingRemoveAddress(null)}
            onConfirm={() => {
              if (!location) return;
              const remaining = location.addresses.filter((a) => a.id !== pendingRemoveAddress.id).map(toAddressInput);
              if (remaining.length > 0 && !remaining.some((a) => a.isDefault)) remaining[0]!.isDefault = true;
              saveAddresses.mutate(remaining);
            }}
          />
        ) : null}
      </>
    );
  }

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new location
              </p>
              <ExcelToolbar
                exportDisabled={locations.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "locations.xlsx",
                    "Locations",
                    [
                      { header: "Code", key: "code", value: (r: { loc: LocationDto; addr: LocationAddressDto }) => r.loc.code },
                      { header: "Name", key: "name", value: (r) => r.loc.name },
                      { header: "Active", key: "active", value: (r) => activeLabel(r.loc.isActive) },
                      { header: "AddressLabel", key: "label", value: (r) => r.addr.label },
                      { header: "Line1", key: "line1", value: (r) => r.addr.line1 },
                      { header: "Line2", key: "line2", value: (r) => r.addr.line2 ?? "" },
                      { header: "City", key: "city", value: (r) => r.addr.city },
                      { header: "State", key: "state", value: (r) => r.addr.state },
                      { header: "Postcode", key: "postcode", value: (r) => r.addr.postcode },
                      { header: "Country", key: "country", value: (r) => r.addr.country },
                      { header: "IsDefault", key: "isDefault", value: (r) => activeLabel(r.addr.isDefault) },
                    ],
                    locations.flatMap((loc) => loc.addresses.map((addr) => ({ loc, addr }))),
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Code</label>
                <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="WH-KL" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="KL Warehouse" />
              </div>
            </div>
            <p className="hint" style={{ margin: "10px 0 6px", fontWeight: 600 }}>
              First address
            </p>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Label</label>
                <input value={address.label} onChange={(e) => setAddress((a) => ({ ...a, label: e.target.value }))} placeholder="Main warehouse" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Line 1</label>
                <input value={address.line1} onChange={(e) => setAddress((a) => ({ ...a, line1: e.target.value }))} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>City</label>
                <input value={address.city} onChange={(e) => setAddress((a) => ({ ...a, city: e.target.value }))} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>State</label>
                <input value={address.state} onChange={(e) => setAddress((a) => ({ ...a, state: e.target.value }))} />
              </div>
              <div className="field" style={{ margin: 0, maxWidth: 120 }}>
                <label>Postcode</label>
                <input value={address.postcode} onChange={(e) => setAddress((a) => ({ ...a, postcode: e.target.value }))} />
              </div>
              <div className="field" style={{ margin: 0, maxWidth: 100 }}>
                <label>Country</label>
                <input value={address.country} onChange={(e) => setAddress((a) => ({ ...a, country: e.target.value.toUpperCase() }))} maxLength={2} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={
                    create.isPending ||
                    !code.trim() ||
                    !name.trim() ||
                    !address.label.trim() ||
                    !address.line1.trim() ||
                    !address.city.trim() ||
                    !address.state.trim() ||
                    !address.postcode.trim()
                  }
                  onClick={() => create.mutate()}
                >
                  Add location
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={locations}
          columns={columns}
          rowKey={(l) => l.id}
          loading={listQuery.isPending}
          loadingLabel="Loading locations…"
          empty={<EmptyState icon="field">No locations yet.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          onRowClick={(l) => setSelectedId(l.id)}
          footer={<TableFooter total={locations.length} active={locations.filter((l) => l.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import locations"
          description="Download the template, fill one row per address (repeat Code/Name for multiple addresses per location), then upload. Matching Code rows replace all addresses for that location."
          templateFileName="locations-import-template.xlsx"
          templateSheetName="Locations"
          templateHeaders={["Code", "Name", "Active", "AddressLabel", "Line1", "Line2", "City", "State", "Postcode", "Country", "IsDefault"]}
          templateSampleRows={[
            {
              Code: "WH-KL",
              Name: "KL Warehouse",
              Active: "Yes",
              AddressLabel: "Main dock",
              Line1: "12 Jalan Industri",
              Line2: "",
              City: "Shah Alam",
              State: "Selangor",
              Postcode: "40000",
              Country: "MY",
              IsDefault: "Yes",
            },
            {
              Code: "WH-KL",
              Name: "KL Warehouse",
              Active: "Yes",
              AddressLabel: "Overflow yard",
              Line1: "14 Jalan Industri",
              Line2: "",
              City: "Shah Alam",
              State: "Selangor",
              Postcode: "40000",
              Country: "MY",
              IsDefault: "No",
            },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>, <code>Line1</code>, <code>City</code>, <code>State</code>, <code>Postcode</code>
              <span className="hint"> · Optional: </span>
              <code>Line2</code>, <code>Country</code>, <code>AddressLabel</code>, <code>IsDefault</code>, <code>Active</code>
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["locations"] })}
        />
      ) : null}

      {editing ? (
        <Modal
          title={`Edit location · ${editing.code}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveNameEdit.isPending || !editName.trim()}
                onClick={() => saveNameEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="field" style={{ margin: 0 }}>
            <label>Name</label>
            <input value={editName} onChange={(e) => setEditName(e.target.value)} autoFocus />
          </div>
        </Modal>
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate location" : "Deactivate location"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggle.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} location{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggle.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

// ==================== ITEMS ====================

function ItemsTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [itemCode, setItemCode] = useState("");
  const [description, setDescription] = useState("");
  const [uom, setUom] = useState("");
  const [editing, setEditing] = useState<ItemDto | null>(null);
  const [editCode, setEditCode] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editUom, setEditUom] = useState("");
  const [pendingDelete, setPendingDelete] = useState<ItemDto | null>(null);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);

  const query = useQuery({ queryKey: ["items", false], queryFn: () => listItems(false) });
  const rowsData = query.data ?? [];

  const add = useMutation({
    mutationFn: () => createItem({ itemCode: itemCode.trim().toUpperCase(), description: description.trim(), uom: uom.trim() || null }),
    onSuccess: () => {
      setItemCode("");
      setDescription("");
      setUom("");
      void qc.invalidateQueries({ queryKey: ["items"] });
    },
    onError: (e) => showErrorFrom(e, "Could not add item"),
  });

  const saveEdit = useMutation({
    mutationFn: () =>
      updateItem(editing!.id, { itemCode: editCode.trim().toUpperCase(), description: editDescription.trim(), uom: editUom.trim() }),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["items"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save item"),
  });

  const toggle = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setItemActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["items"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update item"),
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteItem(id),
    onSuccess: () => {
      setPendingDelete(null);
      void qc.invalidateQueries({ queryKey: ["items"] });
    },
    onError: (e) => showErrorFrom(e, "Could not delete item"),
  });

  const runImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listItems(false);
    const byCode = new Map(existing.map((i) => [i.itemCode.toUpperCase(), i]));

    return runExcelRows(file, onProgress, async (row) => {
      const rowCode = cellStr(row, "ItemCode", "Code").toUpperCase();
      const desc = cellStr(row, "Description", "Name");
      const rowUom = cellStr(row, "Uom", "UOM", "Unit");
      const active = cellActive(row);
      if (!rowCode) throw new Error("ItemCode is required.");
      if (!desc) throw new Error("Description is required.");

      const hit = byCode.get(rowCode);
      if (hit) {
        await updateItem(hit.id, { itemCode: rowCode, description: desc, uom: rowUom || hit.uom });
        if (active != null && active !== hit.isActive) await setItemActive(hit.id, active);
        byCode.set(rowCode, { ...hit, itemCode: rowCode, description: desc, uom: rowUom || hit.uom, isActive: active ?? hit.isActive });
      } else {
        const id = await createItem({ itemCode: rowCode, description: desc, uom: rowUom || null });
        if (active === false) await setItemActive(id, false);
        byCode.set(rowCode, { id, itemCode: rowCode, description: desc, uom: rowUom, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const columns: DataTableColumn<ItemDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Item code",
        sortable: true,
        sortValue: (i) => i.itemCode,
        render: (i) => <span style={{ fontWeight: 600 }}>{i.itemCode}</span>,
      },
      { id: "desc", header: "Description", sortable: true, sortValue: (i) => i.description, render: (i) => i.description },
      {
        id: "uom",
        header: "UOM",
        sortable: true,
        sortValue: (i) => i.uom,
        render: (i) => <span className="hint">{i.uom || "—"}</span>,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (i) => (i.isActive ? 1 : 0),
        render: (i) => <StatusBadge active={i.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (i) => Date.parse(i.createdOnUtc) || 0,
        render: (i) => <span className="hint">{dateTimeMY(i.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (i) => (
          <div className="row-actions">
            <HistoryButton label={i.itemCode} entityId={i.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(i);
                  setEditCode(i.itemCode);
                  setEditDescription(i.description);
                  setEditUom(i.uom);
                }}
              />
              <IconButton
                icon={i.isActive ? "x" : "check"}
                label={i.isActive ? "Deactivate" : "Activate"}
                danger={i.isActive}
                onClick={() => setPendingStatus({ id: i.id, name: i.itemCode, activate: !i.isActive })}
              />
              <IconButton icon="trash" label="Delete" danger onClick={() => setPendingDelete(i)} />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  return (
    <>
      <Gated permission={FshPermissions.configuration.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new item
              </p>
              <ExcelToolbar
                exportDisabled={rowsData.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "items.xlsx",
                    "Items",
                    [
                      { header: "ItemCode", key: "code", value: (i: ItemDto) => i.itemCode },
                      { header: "Description", key: "desc", value: (i: ItemDto) => i.description },
                      { header: "Uom", key: "uom", value: (i: ItemDto) => i.uom },
                      { header: "Active", key: "active", value: (i: ItemDto) => activeLabel(i.isActive) },
                    ],
                    rowsData,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Item code</label>
                <input value={itemCode} onChange={(e) => setItemCode(e.target.value.toUpperCase())} placeholder="e.g. RM-001" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Description</label>
                <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Description" />
              </div>
              <div className="field" style={{ margin: 0, maxWidth: 120 }}>
                <label>UOM</label>
                <input value={uom} onChange={(e) => setUom(e.target.value)} placeholder="EA" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={add.isPending || !itemCode.trim() || !description.trim()}
                  onClick={() => add.mutate()}
                >
                  Add item
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={rowsData}
          columns={columns}
          rowKey={(i) => i.id}
          loading={query.isPending}
          loadingLabel="Loading items…"
          empty={<EmptyState icon="box">No items yet.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          footer={<TableFooter total={rowsData.length} active={rowsData.filter((i) => i.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import items"
          description="Download the template, fill item codes/descriptions, then upload. Matching ItemCode rows are updated."
          templateFileName="items-import-template.xlsx"
          templateSheetName="Items"
          templateHeaders={["ItemCode", "Description", "Uom", "Active"]}
          templateSampleRows={[
            { ItemCode: "RM-001", Description: "Steel rod 10mm", Uom: "KG", Active: "Yes" },
            { ItemCode: "RM-002", Description: "Cement bag 50kg", Uom: "BAG", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>ItemCode</code>, <code>Description</code>
              <span className="hint"> · Optional: </span>
              <code>Uom</code>, <code>Active</code> (Yes/No)
            </>
          }
          runImport={runImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["items"] })}
        />
      ) : null}

      {editing ? (
        <Modal
          title={`Edit item · ${editing.itemCode}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending || !editCode.trim() || !editDescription.trim()}
                onClick={() => saveEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>Item code</label>
              <input value={editCode} onChange={(e) => setEditCode(e.target.value.toUpperCase())} />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Description</label>
              <input value={editDescription} onChange={(e) => setEditDescription(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0, maxWidth: 120 }}>
              <label>UOM</label>
              <input value={editUom} onChange={(e) => setEditUom(e.target.value)} />
            </div>
          </div>
        </Modal>
      ) : null}

      {pendingDelete ? (
        <ConfirmModal
          title="Delete item"
          icon="x"
          danger
          busy={remove.isPending}
          confirmLabel="Delete"
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              Delete item <strong>{pendingDelete.itemCode}</strong>? This soft-deletes the record; it will no longer appear in
              lists.
            </p>
          }
          onCancel={() => setPendingDelete(null)}
          onConfirm={() => remove.mutate(pendingDelete.id)}
        />
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate item" : "Deactivate item"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggle.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} item{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggle.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

// ==================== NUMBERING ====================

function NumberingTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [editing, setEditing] = useState<NumberingSchemeDto | null>(null);
  const [editPrefix, setEditPrefix] = useState("");
  const [editYearSegment, setEditYearSegment] = useState(true);
  const [editDigits, setEditDigits] = useState("4");
  const [peeked, setPeeked] = useState<{ recordType: string; value: string } | null>(null);

  const query = useQuery({ queryKey: ["numbering-schemes"], queryFn: listNumberingSchemes });
  const rowsData = query.data ?? [];

  const saveEdit = useMutation({
    mutationFn: () =>
      updateNumberingScheme(editing!.recordType, {
        prefix: editPrefix.trim(),
        yearSegment: editYearSegment,
        digits: Number(editDigits) || 1,
      }),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["numbering-schemes"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save numbering scheme"),
  });

  const peek = useMutation({
    mutationFn: (recordType: string) => peekDocumentNumber(recordType),
    onSuccess: (value, recordType) => setPeeked({ recordType, value }),
    onError: (e) => showErrorFrom(e, "Could not peek next number"),
  });

  const columns: DataTableColumn<NumberingSchemeDto>[] = useMemo(
    () => [
      {
        id: "type",
        header: "Record type",
        sortable: true,
        sortValue: (s) => s.recordType,
        render: (s) => <span style={{ fontWeight: 600 }}>{s.recordType}</span>,
      },
      { id: "prefix", header: "Prefix", sortable: true, sortValue: (s) => s.prefix, render: (s) => s.prefix },
      {
        id: "year",
        header: "Year segment",
        sortable: true,
        sortValue: (s) => (s.yearSegment ? 1 : 0),
        render: (s) => <span className={`badge ${s.yearSegment ? "b-green" : "b-grey"}`}>{s.yearSegment ? "Yes" : "No"}</span>,
      },
      { id: "digits", header: "Digits", align: "right", sortable: true, sortValue: (s) => s.digits, render: (s) => s.digits },
      { id: "preview", header: "Preview", render: (s) => <code>{s.previewExample}</code> },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (s) => Date.parse(s.createdOnUtc) || 0,
        render: (s) => <span className="hint">{dateTimeMY(s.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (s) => (
          <div className="row-actions">
            <IconButton icon="eye" label="Peek next number" onClick={() => peek.mutate(s.recordType)} />
            <HistoryButton label={s.recordType} entityId={s.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.configuration.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditing(s);
                  setEditPrefix(s.prefix);
                  setEditYearSegment(s.yearSegment);
                  setEditDigits(String(s.digits));
                }}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory, peek],
  );

  return (
    <>
      <div className="card">
        <DataTable
          rows={rowsData}
          columns={columns}
          rowKey={(s) => s.id}
          loading={query.isPending}
          loadingLabel="Loading numbering schemes…"
          empty={<EmptyState icon="list">No numbering schemes configured.</EmptyState>}
          initialSort={{ id: "type", dir: "asc" }}
          footer={
            <p className="hint" style={{ margin: "10px 0 0", textAlign: "right" }}>
              {rowsData.length} record types
            </p>
          }
        />
      </div>

      {editing ? (
        <Modal
          title={`Edit numbering · ${editing.recordType}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending || !editPrefix.trim()}
                onClick={() => saveEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>Prefix</label>
              <input value={editPrefix} onChange={(e) => setEditPrefix(e.target.value.toUpperCase())} autoFocus />
            </div>
            <div className="field" style={{ margin: 0, maxWidth: 100 }}>
              <label>Digits</label>
              <input value={editDigits} onChange={(e) => setEditDigits(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label aria-hidden="true">&nbsp;</label>
              <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <input
                  type="checkbox"
                  checked={editYearSegment}
                  onChange={(e) => setEditYearSegment(e.target.checked)}
                  style={{ width: "auto" }}
                />
                Include year segment
              </label>
            </div>
          </div>
        </Modal>
      ) : null}

      {peeked ? (
        <AlertModal title="Next document number" icon="eye" body={`${peeked.recordType}: ${peeked.value}`} onClose={() => setPeeked(null)} />
      ) : null}
    </>
  );
}

// ---- Custom Fields (Phase 5/6) ----

const CUSTOM_FIELD_DATA_TYPES = [
  "Text", "LongText", "Int", "Decimal", "Money", "Date", "DateTime", "Bool",
  "ListValue", "Percent", "Email", "Telephone", "Hyperlink", "RecordRef",
];
const CUSTOM_FIELD_RECORD_TYPES = ["Requisition", "PurchaseOrder", "Vendor", "Rfq"];

function CustomFieldsTab() {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);
  const [code, setCode] = useState("");
  const [label, setLabel] = useState("");
  const [dataType, setDataType] = useState("Text");
  const [scope, setScope] = useState("Header");
  const [listKey, setListKey] = useState("");
  const [refEntity, setRefEntity] = useState("Vendor");
  const [isRequired, setIsRequired] = useState(false);

  const query = useQuery({ queryKey: ["custom-field-defs"], queryFn: () => listCustomFieldDefs() });
  const rows = query.data ?? [];

  const invalidate = () => void qc.invalidateQueries({ queryKey: ["custom-field-defs"] });

  const add = useMutation({
    mutationFn: () =>
      createCustomFieldDef({
        code: code.trim().toUpperCase(),
        label: label.trim(),
        dataType,
        scope,
        listKey: dataType === "ListValue" ? listKey.trim() : null,
        refEntity: dataType === "RecordRef" ? refEntity : null,
        isRequired,
      }),
    onSuccess: () => {
      setCode("");
      setLabel("");
      setListKey("");
      setIsRequired(false);
      invalidate();
    },
    onError: (e) => showErrorFrom(e, "Could not create custom field"),
  });

  const toggle = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setCustomFieldDefActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      invalidate();
    },
    onError: (e) => showErrorFrom(e, "Could not update custom field"),
  });

  const applyTo = useMutation({
    mutationFn: ({ id, recordType }: { id: string; recordType: string }) => applyCustomFieldToRecordType(id, recordType),
    onSuccess: invalidate,
    onError: (e) => showErrorFrom(e, "Could not apply custom field"),
  });

  const removeFrom = useMutation({
    mutationFn: ({ id, recordType }: { id: string; recordType: string }) => removeCustomFieldApplication(id, recordType),
    onSuccess: invalidate,
    onError: (e) => showErrorFrom(e, "Could not remove custom field application"),
  });

  const columns: DataTableColumn<CustomFieldDefDto>[] = useMemo(
    () => [
      { id: "label", header: "Label", sortable: true, sortValue: (d) => d.label, render: (d) => <span style={{ fontWeight: 600 }}>{d.label}</span> },
      { id: "code", header: "Code", sortable: true, sortValue: (d) => d.code, render: (d) => d.code },
      { id: "dataType", header: "Type", sortable: true, sortValue: (d) => d.dataType, render: (d) => d.dataType },
      { id: "scope", header: "Scope", render: (d) => d.scope },
      {
        id: "appliesTo",
        header: "Applies to",
        render: (d) => (
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            {CUSTOM_FIELD_RECORD_TYPES.map((rt) => {
              const applied = d.appliesTo.includes(rt);
              return (
                <button
                  key={rt}
                  type="button"
                  className={`badge ${applied ? "b-green" : "b-grey"}`}
                  style={{ cursor: "pointer", border: "none" }}
                  onClick={() => (applied ? removeFrom.mutate({ id: d.id, recordType: rt }) : applyTo.mutate({ id: d.id, recordType: rt }))}
                >
                  {rt}
                </button>
              );
            })}
          </div>
        ),
      },
      { id: "status", header: "Status", render: (d) => <StatusBadge active={d.isActive} /> },
      {
        id: "actions",
        header: "",
        align: "right",
        render: (d) => (
          <button
            type="button"
            className="btn btn-out btn-sm"
            onClick={() => setPendingStatus({ id: d.id, name: d.label, activate: !d.isActive })}
          >
            {d.isActive ? "Deactivate" : "Activate"}
          </button>
        ),
      },
    ],
    [applyTo, removeFrom],
  );

  return (
    <>
      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>New custom field</h3>
        </div>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Code</label>
              <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="PROJECT_CODE" />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Label</label>
              <input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="Project Code" />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Data type</label>
              <select value={dataType} onChange={(e) => setDataType(e.target.value)}>
                {CUSTOM_FIELD_DATA_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Scope</label>
              <select value={scope} onChange={(e) => setScope(e.target.value)}>
                <option value="Header">Header</option>
                <option value="Line">Line</option>
              </select>
            </div>
            {dataType === "ListValue" ? (
              <div className="field" style={{ margin: 0 }}>
                <label>Custom list key</label>
                <input value={listKey} onChange={(e) => setListKey(e.target.value)} placeholder="RFQRescind" />
              </div>
            ) : null}
            {dataType === "RecordRef" ? (
              <div className="field" style={{ margin: 0 }}>
                <label>Reference entity</label>
                <select value={refEntity} onChange={(e) => setRefEntity(e.target.value)}>
                  <option value="Vendor">Vendor</option>
                  <option value="User">User</option>
                  <option value="Item">Item</option>
                  <option value="Transaction">Transaction</option>
                </select>
              </div>
            ) : null}
            <div className="field" style={{ margin: 0 }}>
              <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <input type="checkbox" checked={isRequired} onChange={(e) => setIsRequired(e.target.checked)} style={{ width: "auto" }} />
                Required
              </label>
            </div>
            <button
              type="button"
              className="btn btn-pri btn-sm"
              disabled={!code.trim() || !label.trim() || add.isPending}
              onClick={() => add.mutate()}
            >
              <Icon name="plus" size={14} /> Add
            </button>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="cbody">
          {query.isPending ? (
            <Spinner label="Loading custom fields…" />
          ) : rows.length === 0 ? (
            <EmptyState icon="field">No custom fields yet.</EmptyState>
          ) : (
            <DataTable rows={rows} columns={columns} rowKey={(d) => d.id} />
          )}
          <TableFooter total={rows.length} active={rows.filter((d) => d.isActive).length} />
        </div>
      </div>

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate custom field" : "Deactivate custom field"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggle.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} custom field{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggle.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}
