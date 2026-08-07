import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import {
  createBank,
  createCity,
  createCountry,
  createCustomList,
  createOrgUnit,
  createState,
  deleteBank,
  deleteCity,
  deleteCountry,
  deleteState,
  listBanks,
  listCities,
  listCountries,
  listCustomListItems,
  listCustomLists,
  listOrgUnits,
  listStates,
  setBankActive,
  setCityActive,
  setCountryActive,
  setCustomListActive,
  setOrgUnitActive,
  setStateActive,
  updateBank,
  updateCity,
  updateCountry,
  updateCustomList,
  updateState,
  upsertCustomListItem,
  type BankDto,
  type CityDto,
  type CountryDto,
  type CustomListDto,
  type CustomListItemDto,
  type OrgUnitDto,
  type StateDto,
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
import { ConfirmModal, EmptyState, Modal } from "@/components/ui";
import { formatApiError, useErrorDialog } from "@/feedback/ErrorDialogContext";
import { activeLabel, cellActive, cellStr, exportRowsToExcel, parseExcelFile } from "@/lib/excel";
import { dateTimeMY } from "@/lib/format";
import { FshPermissions } from "@/lib/fsh-permissions";

type Tab = "lists" | "countries" | "banks" | "org";

export const LOOKUP_TAB_KEYS = ["lists", "countries", "banks", "org"] as const;

const LOOKUP_TABS: { key: Tab; icon: string; label: string }[] = [
  { key: "lists", icon: "list", label: "Lists" },
  { key: "countries", icon: "field", label: "Countries" },
  { key: "banks", icon: "clip", label: "Banks" },
  { key: "org", icon: "users", label: "Org" },
];

function resolveLookupTab(candidate: string | null | undefined): Tab {
  const hit = LOOKUP_TABS.find((t) => t.key === candidate);
  return hit ? hit.key : "banks";
}

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

export function LookupsPage({
  initialTab,
  embedded = false,
}: { initialTab?: string; embedded?: boolean } = {}) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [tab, setTab] = useState<Tab>(() => resolveLookupTab(initialTab ?? searchParams.get("tab")));
  const [history, setHistory] = useState<{ title: string; entityId: string } | null>(null);

  useEffect(() => {
    setTab(resolveLookupTab(initialTab ?? searchParams.get("tab")));
  }, [initialTab, searchParams]);

  const selectTab = (key: Tab) => {
    setTab(key);
    setSearchParams({ tab: key }, { replace: true });
  };

  return (
    <>
      {!embedded ? (
        <div className="pagehead">
          <div>
            <h1>Masters</h1>
            <p>Lookups, lists, and organisation reference data.</p>
          </div>
          <div className="spacer" />
          <div className="viewtoggle">
            {LOOKUP_TABS.map(({ key, icon, label }) => (
              <button
                key={key}
                type="button"
                className={tab === key ? "on" : ""}
                onClick={() => selectTab(key)}
              >
                <Icon name={icon} size={14} /> {label}
              </button>
            ))}
          </div>
        </div>
      ) : null}

      {tab === "lists" ? <ListsTab onHistory={setHistory} /> : null}
      {tab === "countries" ? <CountriesTab onHistory={setHistory} /> : null}
      {tab === "banks" ? <BanksTab onHistory={setHistory} /> : null}
      {tab === "org" ? <OrgTab onHistory={setHistory} /> : null}

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

type HistoryOpen = (args: { title: string; entityId: string }) => void;

type PendingStatus = { id: string; name: string; activate: boolean };

function ListsTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [selected, setSelected] = useState<CustomListDto | null>(null);
  const [focusAddItem, setFocusAddItem] = useState(false);
  const [newListKey, setNewListKey] = useState("");
  const [newListName, setNewListName] = useState("");
  const [createdShortcut, setCreatedShortcut] = useState<{ key: string; name: string } | null>(null);
  const [editingList, setEditingList] = useState<CustomListDto | null>(null);
  const [editListName, setEditListName] = useState("");
  const [itemCode, setItemCode] = useState("");
  const [itemLabel, setItemLabel] = useState("");
  const [itemSort, setItemSort] = useState("0");
  const [editingItem, setEditingItem] = useState<CustomListItemDto | null>(null);
  const [importListsOpen, setImportListsOpen] = useState(false);
  const [importItemsOpen, setImportItemsOpen] = useState(false);
  const [pendingListStatus, setPendingListStatus] = useState<PendingStatus | null>(null);
  const [pendingItemStatus, setPendingItemStatus] = useState<PendingStatus | null>(null);

  const listsQuery = useQuery({
    queryKey: ["custom-lists", false],
    queryFn: () => listCustomLists(false),
  });

  const selectedKey = selected?.key ?? null;
  const itemsQuery = useQuery({
    queryKey: ["custom-list-items", selectedKey],
    queryFn: () => listCustomListItems(selectedKey!, false),
    enabled: !!selectedKey,
  });

  const openList = (list: CustomListDto, addItem = false) => {
    setSelected(list);
    setFocusAddItem(addItem);
    setCreatedShortcut(null);
    setEditingItem(null);
    setItemCode("");
    setItemLabel("");
    setItemSort("0");
  };

  const createList = useMutation({
    mutationFn: () => createCustomList({ key: newListKey.trim(), name: newListName.trim() }),
    onSuccess: async () => {
      const key = newListKey.trim();
      const name = newListName.trim();
      setNewListKey("");
      setNewListName("");
      await qc.invalidateQueries({ queryKey: ["custom-lists"] });
      setCreatedShortcut({ key, name });
    },
    onError: (e) => showErrorFrom(e, "Could not create list"),
  });

  const saveListEdit = useMutation({
    mutationFn: () => updateCustomList(editingList!.id, { name: editListName.trim() }),
    onSuccess: async () => {
      const id = editingList!.id;
      setEditingList(null);
      await qc.invalidateQueries({ queryKey: ["custom-lists"] });
      const refreshed = await listCustomLists(false);
      const hit = refreshed.find((l) => l.id === id);
      if (hit && selected?.id === id) setSelected(hit);
    },
    onError: (e) => showErrorFrom(e, "Could not save list"),
  });

  const toggleList = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setCustomListActive(id, active),
    onSuccess: async () => {
      setPendingListStatus(null);
      await qc.invalidateQueries({ queryKey: ["custom-lists"] });
      if (selected) {
        const refreshed = await listCustomLists(false);
        const hit = refreshed.find((l) => l.id === selected.id);
        if (hit) setSelected(hit);
      }
    },
    onError: (e) => showErrorFrom(e, "Could not update list"),
  });

  const upsertItem = useMutation({
    mutationFn: () =>
      upsertCustomListItem(selectedKey!, {
        code: itemCode.trim(),
        label: itemLabel.trim(),
        sortOrder: Number(itemSort) || 0,
        isActive: editingItem?.isActive ?? true,
      }),
    onSuccess: () => {
      setItemCode("");
      setItemLabel("");
      setItemSort("0");
      setEditingItem(null);
      setFocusAddItem(false);
      void qc.invalidateQueries({ queryKey: ["custom-list-items", selectedKey] });
    },
    onError: (e) => showErrorFrom(e, "Could not save item"),
  });

  const toggleItem = useMutation({
    mutationFn: (item: CustomListItemDto) =>
      upsertCustomListItem(selectedKey!, {
        code: item.code,
        label: item.label,
        sortOrder: item.sortOrder,
        isActive: !item.isActive,
      }),
    onSuccess: () => {
      setPendingItemStatus(null);
      void qc.invalidateQueries({ queryKey: ["custom-list-items", selectedKey] });
    },
    onError: (e) => showErrorFrom(e, "Could not update item"),
  });

  const lists = listsQuery.data ?? [];
  const items = itemsQuery.data ?? [];

  const runListsImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listCustomLists(false);
    const byKey = new Map(existing.map((l) => [l.key.toLowerCase(), l]));

    return runExcelRows(file, onProgress, async (row) => {
      const key = cellStr(row, "Key", "List key", "ListKey");
      const name = cellStr(row, "Name", "List name", "ListName");
      const active = cellActive(row);
      if (!key) throw new Error("Key is required.");
      if (!name) throw new Error("Name is required.");

      const hit = byKey.get(key.toLowerCase());
      if (hit) {
        await updateCustomList(hit.id, { name });
        if (active != null && active !== hit.isActive) {
          await setCustomListActive(hit.id, active);
        }
        byKey.set(key.toLowerCase(), { ...hit, name, isActive: active ?? hit.isActive });
      } else {
        const id = await createCustomList({ key, name });
        if (active === false) await setCustomListActive(id, false);
        byKey.set(key.toLowerCase(), {
          id,
          key,
          name,
          isActive: active ?? true,
          createdOnUtc: new Date().toISOString(),
        });
      }
    });
  };

  const runItemsImport: ExcelImportRunner = async (file, onProgress) => {
    const listKey = selected!.key;
    const existing = await listCustomListItems(listKey, false);
    const byCode = new Map(existing.map((i) => [i.code.toLowerCase(), i]));

    return runExcelRows(file, onProgress, async (row) => {
      const code = cellStr(row, "Code", "Item code", "ItemCode");
      const label = cellStr(row, "Label", "Name");
      const sortRaw = cellStr(row, "Sort", "Sort order", "SortOrder");
      const active = cellActive(row);
      if (!code) throw new Error("Code is required.");
      if (!label) throw new Error("Label is required.");
      const sortOrder = sortRaw ? Number(sortRaw) : (byCode.get(code.toLowerCase())?.sortOrder ?? 0);
      if (Number.isNaN(sortOrder)) throw new Error("Sort must be a number.");

      const hit = byCode.get(code.toLowerCase());
      await upsertCustomListItem(listKey, {
        code,
        label,
        sortOrder,
        isActive: active ?? hit?.isActive ?? true,
      });
      byCode.set(code.toLowerCase(), {
        id: hit?.id ?? code,
        listId: hit?.listId ?? selected!.id,
        code,
        label,
        sortOrder,
        isActive: active ?? hit?.isActive ?? true,
        createdOnUtc: hit?.createdOnUtc ?? new Date().toISOString(),
      });
    });
  };

  const startEditItem = (item: CustomListItemDto) => {
    setEditingItem(item);
    setItemCode(item.code);
    setItemLabel(item.label);
    setItemSort(String(item.sortOrder));
    setFocusAddItem(true);
  };

  const listColumns: DataTableColumn<CustomListDto>[] = useMemo(
    () => [
      {
        id: "key",
        header: "Key",
        sortable: true,
        sortValue: (l) => l.key,
        render: (l) => <span style={{ fontWeight: 600 }}>{l.key}</span>,
      },
      {
        id: "name",
        header: "Name",
        sortable: true,
        sortValue: (l) => l.name,
        render: (l) => l.name,
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
            <IconButton icon="chev" label={`Open ${l.name}`} onClick={() => openList(l)} />
            <HistoryButton label={l.name} entityId={l.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.customLists.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditingList(l);
                  setEditListName(l.name);
                }}
              />
              <IconButton
                icon={l.isActive ? "x" : "check"}
                label={l.isActive ? "Deactivate" : "Activate"}
                danger={l.isActive}
                onClick={() => setPendingListStatus({ id: l.id, name: l.name, activate: !l.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  const itemColumns: DataTableColumn<CustomListItemDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (i) => i.code,
        render: (i) => <span style={{ fontWeight: 600 }}>{i.code}</span>,
      },
      {
        id: "label",
        header: "Label",
        sortable: true,
        sortValue: (i) => i.label,
        render: (i) => i.label,
      },
      {
        id: "sort",
        header: "Sort",
        sortable: true,
        sortValue: (i) => i.sortOrder,
        render: (i) => i.sortOrder,
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
            <HistoryButton label={i.label} entityId={i.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.customLists.manage}>
              <IconButton icon="edit" label="Edit" onClick={() => startEditItem(i)} />
              <IconButton
                icon={i.isActive ? "x" : "check"}
                label={i.isActive ? "Deactivate" : "Activate"}
                danger={i.isActive}
                onClick={() => setPendingItemStatus({ id: i.id, name: i.label, activate: !i.isActive })}
              />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  if (selected) {
    return (
      <>
        <DetailHeader
          title={selected.name}
          subtitle={`List key · ${selected.key}`}
          onBack={() => {
            setSelected(null);
            setFocusAddItem(false);
            setEditingItem(null);
          }}
          backLabel="All lists"
        />

        <Gated permission={FshPermissions.customLists.manage}>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="cbody">
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
                <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                  {editingItem ? `Edit item · ${editingItem.code}` : "Add list item"}
                </p>
                <ExcelToolbar
                  exportDisabled={items.length === 0}
                  onExport={() =>
                    exportRowsToExcel(
                      `${selected.key}-items.xlsx`,
                      "Items",
                      [
                        { header: "Code", key: "code", value: (i: CustomListItemDto) => i.code },
                        { header: "Label", key: "label", value: (i: CustomListItemDto) => i.label },
                        { header: "Sort", key: "sort", value: (i: CustomListItemDto) => i.sortOrder },
                        { header: "Active", key: "active", value: (i: CustomListItemDto) => activeLabel(i.isActive) },
                      ],
                      items,
                    )
                  }
                  onImport={() => setImportItemsOpen(true)}
                />
              </div>
              <div className="filterbar filterbar-auto">
                <div className="field" style={{ margin: 0 }}>
                  <label>Code</label>
                  <input
                    value={itemCode}
                    onChange={(e) => setItemCode(e.target.value)}
                    disabled={!!editingItem}
                    autoFocus={focusAddItem}
                    placeholder="e.g. PRICE"
                  />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label>Label</label>
                  <input value={itemLabel} onChange={(e) => setItemLabel(e.target.value)} placeholder="Display label" />
                </div>
                <div className="field" style={{ margin: 0, maxWidth: 100 }}>
                  <label>Sort</label>
                  <input value={itemSort} onChange={(e) => setItemSort(e.target.value)} />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label aria-hidden="true">&nbsp;</label>
                  <div style={{ display: "flex", gap: 8 }}>
                    {editingItem ? (
                      <button
                        type="button"
                        className="btn btn-out btn-sm"
                        onClick={() => {
                          setEditingItem(null);
                          setItemCode("");
                          setItemLabel("");
                          setItemSort("0");
                        }}
                      >
                        Cancel
                      </button>
                    ) : null}
                    <button
                      type="button"
                      className="btn btn-pri btn-sm"
                      disabled={upsertItem.isPending || !itemCode.trim() || !itemLabel.trim()}
                      onClick={() => upsertItem.mutate()}
                    >
                      {editingItem ? "Save item" : "Add item"}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </Gated>

        <div className="card">
          <DataTable
            rows={items}
            columns={itemColumns}
            rowKey={(i) => i.id}
            loading={itemsQuery.isPending}
            empty={<EmptyState icon="list">No items yet — add the first reason/code above.</EmptyState>}
            initialSort={{ id: "sort", dir: "asc" }}
            footer={<TableFooter total={items.length} active={items.filter((i) => i.isActive).length} />}
          />
        </div>

        {importItemsOpen ? (
          <ExcelImportModal
            title={`Import items · ${selected.key}`}
            description="Download the template, fill codes/labels, then upload. Matching Code rows are updated."
            templateFileName={`${selected.key}-items-import-template.xlsx`}
            templateSheetName="Items"
            templateHeaders={["Code", "Label", "Sort", "Active"]}
            templateSampleRows={[
              { Code: "PRICE", Label: "Price too high", Sort: 10, Active: "Yes" },
              { Code: "SCOPE", Label: "Out of scope", Sort: 20, Active: "Yes" },
            ]}
            columnsHint={
              <>
                <span className="hint">Required: </span>
                <code>Code</code>, <code>Label</code>
                <span className="hint"> · Optional: </span>
                <code>Sort</code>, <code>Active</code> (Yes/No)
              </>
            }
            runImport={runItemsImport}
            onClose={() => setImportItemsOpen(false)}
            onImported={() => void qc.invalidateQueries({ queryKey: ["custom-list-items", selected.key] })}
          />
        ) : null}

        {pendingItemStatus ? (
        <ConfirmModal
          title={pendingItemStatus.activate ? "Activate item" : "Deactivate item"}
          icon={pendingItemStatus.activate ? "check" : "x"}
          danger={!pendingItemStatus.activate}
          busy={toggleItem.isPending}
          confirmLabel={pendingItemStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingItemStatus.activate ? "Activate" : "Deactivate"} item{" "}
              <strong>{pendingItemStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingItemStatus(null)}
          onConfirm={() => {
              const item = items.find((x) => x.id === pendingItemStatus.id);
              if (item) toggleItem.mutate(item);
            }}
        />
      ) : null}
      </>
    );
  }

  return (
    <>
      <Gated permission={FshPermissions.customLists.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new list
              </p>
              <ExcelToolbar
                exportDisabled={lists.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "custom-lists.xlsx",
                    "Lists",
                    [
                      { header: "Key", key: "key", value: (l: CustomListDto) => l.key },
                      { header: "Name", key: "name", value: (l: CustomListDto) => l.name },
                      { header: "Active", key: "active", value: (l: CustomListDto) => activeLabel(l.isActive) },
                    ],
                    lists,
                  )
                }
                onImport={() => setImportListsOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>List key</label>
                <input
                  value={newListKey}
                  onChange={(e) => setNewListKey(e.target.value)}
                  placeholder="e.g. BidDecline"
                />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input
                  value={newListName}
                  onChange={(e) => setNewListName(e.target.value)}
                  placeholder="Bid decline reasons"
                />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={createList.isPending || !newListKey.trim() || !newListName.trim()}
                  onClick={() => createList.mutate()}
                >
                  Create list
                </button>
              </div>
            </div>
            {createdShortcut ? (
              <div className="shortcut-banner">
                <span>
                  Created <strong>{createdShortcut.name}</strong> ({createdShortcut.key}).
                </span>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  onClick={() => {
                    const hit = lists.find((l) => l.key === createdShortcut.key);
                    if (hit) openList(hit, true);
                    else {
                      void qc.invalidateQueries({ queryKey: ["custom-lists"] }).then(async () => {
                        const refreshed = await listCustomLists(false);
                        const found = refreshed.find((l) => l.key === createdShortcut.key);
                        if (found) openList(found, true);
                      });
                    }
                  }}
                >
                  Open &amp; add items
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={lists}
          columns={listColumns}
          rowKey={(l) => l.id}
          loading={listsQuery.isPending}
          loadingLabel="Loading lists…"
          empty={<EmptyState icon="list">No custom lists yet.</EmptyState>}
          initialSort={{ id: "key", dir: "asc" }}
          onRowClick={(l) => openList(l)}
          footer={<TableFooter total={lists.length} active={lists.filter((l) => l.isActive).length} />}
        />
      </div>

      {importListsOpen ? (
        <ExcelImportModal
          title="Import custom lists"
          description="Download the template, fill list keys/names, then upload. Matching Key rows update the name/status."
          templateFileName="custom-lists-import-template.xlsx"
          templateSheetName="Lists"
          templateHeaders={["Key", "Name", "Active"]}
          templateSampleRows={[
            { Key: "BidDecline", Name: "Bid decline reasons", Active: "Yes" },
            { Key: "RFQRescind", Name: "RFQ rescind reasons", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Key</code>, <code>Name</code>
              <span className="hint"> · Optional: </span>
              <code>Active</code> (Yes/No)
            </>
          }
          runImport={runListsImport}
          onClose={() => setImportListsOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["custom-lists"] })}
        />
      ) : null}

      {editingList ? (
        <Modal
          title={`Edit list · ${editingList.key}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditingList(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveListEdit.isPending || !editListName.trim()}
                onClick={() => saveListEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="field" style={{ margin: 0 }}>
            <label>Name</label>
            <input value={editListName} onChange={(e) => setEditListName(e.target.value)} autoFocus />
          </div>
        </Modal>
      ) : null}

      {pendingListStatus ? (
        <ConfirmModal
          title={pendingListStatus.activate ? "Activate list" : "Deactivate list"}
          icon={pendingListStatus.activate ? "check" : "x"}
          danger={!pendingListStatus.activate}
          busy={toggleList.isPending}
          confirmLabel={pendingListStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingListStatus.activate ? "Activate" : "Deactivate"} list{" "}
              <strong>{pendingListStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingListStatus(null)}
          onConfirm={() => toggleList.mutate({ id: pendingListStatus.id, active: pendingListStatus.activate })}
        />
      ) : null}
    </>
  );
}

function CountriesTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showError, showErrorFrom } = useErrorDialog();
  const [country, setCountry] = useState<CountryDto | null>(null);
  const [state, setState] = useState<StateDto | null>(null);
  const [focusAddState, setFocusAddState] = useState(false);
  const [focusAddCity, setFocusAddCity] = useState(false);
  const [countryCode, setCountryCode] = useState("");
  const [countryName, setCountryName] = useState("");
  const [createdCountryShortcut, setCreatedCountryShortcut] = useState<{ code: string; name: string } | null>(null);
  const [createdStateShortcut, setCreatedStateShortcut] = useState<{ id: string; name: string } | null>(null);
  const [stateCode, setStateCode] = useState("");
  const [stateName, setStateName] = useState("");
  const [cityName, setCityName] = useState("");
  const [editingCountry, setEditingCountry] = useState<CountryDto | null>(null);
  const [editCountryCode, setEditCountryCode] = useState("");
  const [editCountryName, setEditCountryName] = useState("");
  const [editingState, setEditingState] = useState<StateDto | null>(null);
  const [editStateCode, setEditStateCode] = useState("");
  const [editStateName, setEditStateName] = useState("");
  const [editingCity, setEditingCity] = useState<CityDto | null>(null);
  const [editCityName, setEditCityName] = useState("");
  const [pendingDeleteCountry, setPendingDeleteCountry] = useState<CountryDto | null>(null);
  const [pendingDeleteState, setPendingDeleteState] = useState<StateDto | null>(null);
  const [pendingDeleteCity, setPendingDeleteCity] = useState<CityDto | null>(null);
  const [pendingCountryStatus, setPendingCountryStatus] = useState<PendingStatus | null>(null);
  const [pendingStateStatus, setPendingStateStatus] = useState<PendingStatus | null>(null);
  const [pendingCityStatus, setPendingCityStatus] = useState<PendingStatus | null>(null);
  const [importCountriesOpen, setImportCountriesOpen] = useState(false);
  const [importStatesOpen, setImportStatesOpen] = useState(false);
  const [importCitiesOpen, setImportCitiesOpen] = useState(false);

  const countriesQuery = useQuery({
    queryKey: ["countries", false],
    queryFn: () => listCountries(false),
  });

  const statesQuery = useQuery({
    queryKey: ["states", country?.id],
    queryFn: () => listStates(country!.id, false),
    enabled: !!country,
  });

  const citiesQuery = useQuery({
    queryKey: ["cities", state?.id],
    queryFn: () => listCities(state!.id, false),
    enabled: !!state,
  });

  const countries = countriesQuery.data ?? [];
  const states = statesQuery.data ?? [];
  const cities = citiesQuery.data ?? [];

  const openCountry = (c: CountryDto, addState = false) => {
    setCountry(c);
    setState(null);
    setFocusAddState(addState);
    setFocusAddCity(false);
    setCreatedCountryShortcut(null);
    setCreatedStateShortcut(null);
  };

  const openState = (s: StateDto, addCity = false) => {
    setState(s);
    setFocusAddCity(addCity);
    setCreatedStateShortcut(null);
    setCityName("");
  };

  const addCountry = useMutation({
    mutationFn: () => createCountry({ code: countryCode.trim(), name: countryName.trim() }),
    onSuccess: async () => {
      const code = countryCode.trim().toUpperCase();
      const name = countryName.trim();
      setCountryCode("");
      setCountryName("");
      await qc.invalidateQueries({ queryKey: ["countries"] });
      setCreatedCountryShortcut({ code, name });
    },
    onError: (e) => showErrorFrom(e, "Could not add country"),
  });

  const saveCountryEdit = useMutation({
    mutationFn: () =>
      updateCountry(editingCountry!.id, { code: editCountryCode.trim(), name: editCountryName.trim() }),
    onSuccess: async () => {
      const id = editingCountry!.id;
      setEditingCountry(null);
      await qc.invalidateQueries({ queryKey: ["countries"] });
      const refreshed = await listCountries(false);
      const hit = refreshed.find((c) => c.id === id);
      if (hit && country?.id === id) setCountry(hit);
    },
    onError: (e) => showErrorFrom(e, "Could not save country"),
  });

  const toggleCountry = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setCountryActive(id, active),
    onSuccess: async () => {
      setPendingCountryStatus(null);
      await qc.invalidateQueries({ queryKey: ["countries"] });
      if (country) {
        const refreshed = await listCountries(false);
        const hit = refreshed.find((c) => c.id === country.id);
        if (hit) setCountry(hit);
      }
    },
    onError: (e) => showErrorFrom(e, "Could not update country"),
  });

  const removeCountry = useMutation({
    mutationFn: (id: string) => deleteCountry(id),
    onSuccess: () => {
      setPendingDeleteCountry(null);
      setCountry(null);
      setState(null);
      void qc.invalidateQueries({ queryKey: ["countries"] });
    },
    onError: (e) => showErrorFrom(e, "Could not delete country"),
  });

  const addStateMut = useMutation({
    mutationFn: () =>
      createState({
        countryId: country!.id,
        code: stateCode.trim(),
        name: stateName.trim(),
      }),
    onSuccess: async (id) => {
      const name = stateName.trim();
      setStateCode("");
      setStateName("");
      setFocusAddState(false);
      await qc.invalidateQueries({ queryKey: ["states", country!.id] });
      setCreatedStateShortcut({ id, name });
    },
    onError: (e) => showErrorFrom(e, "Could not add state"),
  });

  const saveStateEdit = useMutation({
    mutationFn: () =>
      updateState(editingState!.id, { code: editStateCode.trim(), name: editStateName.trim() }),
    onSuccess: async () => {
      const id = editingState!.id;
      setEditingState(null);
      await qc.invalidateQueries({ queryKey: ["states", country!.id] });
      const refreshed = await listStates(country!.id, false);
      const hit = refreshed.find((s) => s.id === id);
      if (hit && state?.id === id) setState(hit);
    },
    onError: (e) => showErrorFrom(e, "Could not save state"),
  });

  const toggleState = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setStateActive(id, active),
    onSuccess: async () => {
      setPendingStateStatus(null);
      await qc.invalidateQueries({ queryKey: ["states", country!.id] });
      if (state) {
        const refreshed = await listStates(country!.id, false);
        const hit = refreshed.find((s) => s.id === state.id);
        if (hit) setState(hit);
      }
    },
    onError: (e) => showErrorFrom(e, "Could not update state"),
  });

  const removeState = useMutation({
    mutationFn: (id: string) => deleteState(id),
    onSuccess: () => {
      setPendingDeleteState(null);
      setState(null);
      void qc.invalidateQueries({ queryKey: ["states", country!.id] });
    },
    onError: (e) => showErrorFrom(e, "Could not delete state"),
  });

  const addCityMut = useMutation({
    mutationFn: () => createCity({ stateId: state!.id, name: cityName.trim() }),
    onSuccess: () => {
      setCityName("");
      setFocusAddCity(false);
      void qc.invalidateQueries({ queryKey: ["cities", state!.id] });
    },
    onError: (e) => showErrorFrom(e, "Could not add city"),
  });

  const saveCityEdit = useMutation({
    mutationFn: () => updateCity(editingCity!.id, { name: editCityName.trim() }),
    onSuccess: () => {
      setEditingCity(null);
      void qc.invalidateQueries({ queryKey: ["cities", state!.id] });
    },
    onError: (e) => showErrorFrom(e, "Could not save city"),
  });

  const toggleCity = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setCityActive(id, active),
    onSuccess: () => {
      setPendingCityStatus(null);
      void qc.invalidateQueries({ queryKey: ["cities", state!.id] });
    },
    onError: (e) => showErrorFrom(e, "Could not update city"),
  });

  const removeCity = useMutation({
    mutationFn: (id: string) => deleteCity(id),
    onSuccess: () => {
      setPendingDeleteCity(null);
      void qc.invalidateQueries({ queryKey: ["cities", state!.id] });
    },
    onError: (e) => showErrorFrom(e, "Could not delete city"),
  });

  const runCountriesImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listCountries(false);
    const byCode = new Map(existing.map((c) => [c.code.toUpperCase(), c]));

    return runExcelRows(file, onProgress, async (row) => {
      const code = cellStr(row, "Code", "Country", "Country code", "Country Code").toUpperCase();
      const name = cellStr(row, "Name", "Country name", "Country Name");
      const active = cellActive(row);
      if (code.length !== 2) throw new Error("Code must be a 2-letter ISO code (e.g. MY).");
      if (!name) throw new Error("Name is required.");

      const hit = byCode.get(code);
      if (hit) {
        await updateCountry(hit.id, { code, name });
        if (active != null && active !== hit.isActive) await setCountryActive(hit.id, active);
        byCode.set(code, { ...hit, code, name, isActive: active ?? hit.isActive });
      } else {
        const id = await createCountry({ code, name });
        if (active === false) await setCountryActive(id, false);
        byCode.set(code, { id, code, name, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const runStatesImport: ExcelImportRunner = async (file, onProgress) => {
    const countryId = country!.id;
    const existing = await listStates(countryId, false);
    const byCode = new Map(existing.map((s) => [s.code.toUpperCase(), s]));

    return runExcelRows(file, onProgress, async (row) => {
      const code = cellStr(row, "Code", "State code", "State Code").toUpperCase();
      const name = cellStr(row, "Name", "State name", "State Name");
      const active = cellActive(row);
      if (!code) throw new Error("Code is required.");
      if (!name) throw new Error("Name is required.");

      const hit = byCode.get(code);
      if (hit) {
        await updateState(hit.id, { code, name });
        if (active != null && active !== hit.isActive) await setStateActive(hit.id, active);
        byCode.set(code, { ...hit, code, name, isActive: active ?? hit.isActive });
      } else {
        const id = await createState({ countryId, code, name });
        if (active === false) await setStateActive(id, false);
        byCode.set(code, { id, countryId, code, name, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const runCitiesImport: ExcelImportRunner = async (file, onProgress) => {
    const stateId = state!.id;
    const existing = await listCities(stateId, false);
    const byName = new Map(existing.map((c) => [c.name.toLowerCase(), c]));

    return runExcelRows(file, onProgress, async (row) => {
      const name = cellStr(row, "Name", "City", "City name", "City Name");
      const active = cellActive(row);
      if (!name) throw new Error("Name is required.");

      const hit = byName.get(name.toLowerCase());
      if (hit) {
        await updateCity(hit.id, { name });
        if (active != null && active !== hit.isActive) await setCityActive(hit.id, active);
        byName.set(name.toLowerCase(), { ...hit, name, isActive: active ?? hit.isActive });
      } else {
        const id = await createCity({ stateId, name });
        if (active === false) await setCityActive(id, false);
        byName.set(name.toLowerCase(), { id, stateId, name, isActive: active ?? true, createdOnUtc: new Date().toISOString() });
      }
    });
  };

  const countryColumns: DataTableColumn<CountryDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (c) => c.code,
        render: (c) => <span style={{ fontWeight: 600 }}>{c.code}</span>,
      },
      {
        id: "name",
        header: "Name",
        sortable: true,
        sortValue: (c) => c.name,
        render: (c) => c.name,
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
            <IconButton icon="chev" label={`Open ${c.name}`} onClick={() => openCountry(c)} />
            <HistoryButton label={c.name} entityId={c.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.lookups.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditingCountry(c);
                  setEditCountryCode(c.code);
                  setEditCountryName(c.name);
                }}
              />
              <IconButton
                icon={c.isActive ? "x" : "check"}
                label={c.isActive ? "Deactivate" : "Activate"}
                danger={c.isActive}
                onClick={() => setPendingCountryStatus({ id: c.id, name: c.name, activate: !c.isActive })}
              />
              <IconButton icon="trash" label="Delete" danger onClick={() => setPendingDeleteCountry(c)} />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  const stateColumns: DataTableColumn<StateDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (s) => s.code,
        render: (s) => <span style={{ fontWeight: 600 }}>{s.code}</span>,
      },
      {
        id: "name",
        header: "Name",
        sortable: true,
        sortValue: (s) => s.name,
        render: (s) => s.name,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (s) => (s.isActive ? 1 : 0),
        render: (s) => <StatusBadge active={s.isActive} />,
      },
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
            <IconButton icon="chev" label={`Open ${s.name}`} onClick={() => openState(s)} />
            <HistoryButton label={s.name} entityId={s.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.lookups.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditingState(s);
                  setEditStateCode(s.code);
                  setEditStateName(s.name);
                }}
              />
              <IconButton
                icon={s.isActive ? "x" : "check"}
                label={s.isActive ? "Deactivate" : "Activate"}
                danger={s.isActive}
                onClick={() => setPendingStateStatus({ id: s.id, name: s.name, activate: !s.isActive })}
              />
              <IconButton icon="trash" label="Delete" danger onClick={() => setPendingDeleteState(s)} />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  const cityColumns: DataTableColumn<CityDto>[] = useMemo(
    () => [
      {
        id: "name",
        header: "Name",
        sortable: true,
        sortValue: (c) => c.name,
        render: (c) => <span style={{ fontWeight: 600 }}>{c.name}</span>,
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
            <Gated permission={FshPermissions.lookups.manage}>
              <IconButton
                icon="edit"
                label="Edit"
                onClick={() => {
                  setEditingCity(c);
                  setEditCityName(c.name);
                }}
              />
              <IconButton
                icon={c.isActive ? "x" : "check"}
                label={c.isActive ? "Deactivate" : "Activate"}
                danger={c.isActive}
                onClick={() => setPendingCityStatus({ id: c.id, name: c.name, activate: !c.isActive })}
              />
              <IconButton icon="trash" label="Delete" danger onClick={() => setPendingDeleteCity(c)} />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  const editModals = (
    <>
      {editingCountry ? (
        <Modal
          title={`Edit country · ${editingCountry.code}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditingCountry(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={
                  saveCountryEdit.isPending || editCountryCode.trim().length !== 2 || !editCountryName.trim()
                }
                onClick={() => {
                  if (editCountryCode.trim().length !== 2) {
                    showError("Country code must be exactly 2 letters (e.g. MY).");
                    return;
                  }
                  saveCountryEdit.mutate();
                }}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>Country code (2 letters)</label>
              <input
                value={editCountryCode}
                onChange={(e) => setEditCountryCode(e.target.value.toUpperCase())}
                maxLength={2}
              />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Country name</label>
              <input value={editCountryName} onChange={(e) => setEditCountryName(e.target.value)} />
            </div>
          </div>
        </Modal>
      ) : null}

      {editingState ? (
        <Modal
          title={`Edit state · ${editingState.code}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditingState(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveStateEdit.isPending || !editStateCode.trim() || !editStateName.trim()}
                onClick={() => saveStateEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>State code</label>
              <input value={editStateCode} onChange={(e) => setEditStateCode(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>State name</label>
              <input value={editStateName} onChange={(e) => setEditStateName(e.target.value)} />
            </div>
          </div>
        </Modal>
      ) : null}

      {editingCity ? (
        <Modal
          title={`Edit city · ${editingCity.name}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditingCity(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveCityEdit.isPending || !editCityName.trim()}
                onClick={() => saveCityEdit.mutate()}
              >
                Save
              </button>
            </>
          }
        >
          <div className="field" style={{ margin: 0 }}>
            <label>City name</label>
            <input value={editCityName} onChange={(e) => setEditCityName(e.target.value)} autoFocus />
          </div>
        </Modal>
      ) : null}

      {pendingDeleteCountry ? (
        <ConfirmModal
          title="Delete country"
          icon="x"
          danger
          busy={removeCountry.isPending}
          confirmLabel="Delete"
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              Delete country <strong>{pendingDeleteCountry.name}</strong> ({pendingDeleteCountry.code})? This
              soft-deletes the record; it will no longer appear in lists.
            </p>
          }
          onCancel={() => setPendingDeleteCountry(null)}
          onConfirm={() => removeCountry.mutate(pendingDeleteCountry.id)}
        />
      ) : null}

      {pendingDeleteState ? (
        <ConfirmModal
          title="Delete state"
          icon="x"
          danger
          busy={removeState.isPending}
          confirmLabel="Delete"
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              Delete state <strong>{pendingDeleteState.name}</strong> ({pendingDeleteState.code})? This
              soft-deletes the record.
            </p>
          }
          onCancel={() => setPendingDeleteState(null)}
          onConfirm={() => removeState.mutate(pendingDeleteState.id)}
        />
      ) : null}

      {pendingDeleteCity ? (
        <ConfirmModal
          title="Delete city"
          icon="x"
          danger
          busy={removeCity.isPending}
          confirmLabel="Delete"
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              Delete city <strong>{pendingDeleteCity.name}</strong>? This soft-deletes the record.
            </p>
          }
          onCancel={() => setPendingDeleteCity(null)}
          onConfirm={() => removeCity.mutate(pendingDeleteCity.id)}
        />
      ) : null}


      {pendingCountryStatus ? (
        <ConfirmModal
          title={pendingCountryStatus.activate ? "Activate country" : "Deactivate country"}
          icon={pendingCountryStatus.activate ? "check" : "x"}
          danger={!pendingCountryStatus.activate}
          busy={toggleCountry.isPending}
          confirmLabel={pendingCountryStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingCountryStatus.activate ? "Activate" : "Deactivate"} country{" "}
              <strong>{pendingCountryStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingCountryStatus(null)}
          onConfirm={() => toggleCountry.mutate({ id: pendingCountryStatus.id, active: pendingCountryStatus.activate })}
        />
      ) : null}

      {pendingStateStatus ? (
        <ConfirmModal
          title={pendingStateStatus.activate ? "Activate state" : "Deactivate state"}
          icon={pendingStateStatus.activate ? "check" : "x"}
          danger={!pendingStateStatus.activate}
          busy={toggleState.isPending}
          confirmLabel={pendingStateStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStateStatus.activate ? "Activate" : "Deactivate"} state{" "}
              <strong>{pendingStateStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStateStatus(null)}
          onConfirm={() => toggleState.mutate({ id: pendingStateStatus.id, active: pendingStateStatus.activate })}
        />
      ) : null}

      {pendingCityStatus ? (
        <ConfirmModal
          title={pendingCityStatus.activate ? "Activate city" : "Deactivate city"}
          icon={pendingCityStatus.activate ? "check" : "x"}
          danger={!pendingCityStatus.activate}
          busy={toggleCity.isPending}
          confirmLabel={pendingCityStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingCityStatus.activate ? "Activate" : "Deactivate"} city{" "}
              <strong>{pendingCityStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingCityStatus(null)}
          onConfirm={() => toggleCity.mutate({ id: pendingCityStatus.id, active: pendingCityStatus.activate })}
        />
      ) : null}


      {importCountriesOpen ? (
        <ExcelImportModal
          title="Import countries"
          description="Download the template, fill country codes/names, then upload. Matching Code rows are updated."
          templateFileName="countries-import-template.xlsx"
          templateSheetName="Countries"
          templateHeaders={["Code", "Name", "Active"]}
          templateSampleRows={[
            { Code: "MY", Name: "Malaysia", Active: "Yes" },
            { Code: "SG", Name: "Singapore", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code> (2 letters), <code>Name</code>
              <span className="hint"> · Optional: </span>
              <code>Active</code> (Yes/No)
            </>
          }
          runImport={runCountriesImport}
          onClose={() => setImportCountriesOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["countries"] })}
        />
      ) : null}

      {importStatesOpen && country ? (
        <ExcelImportModal
          title={`Import states · ${country.code}`}
          description="Download the template, fill state codes/names for this country, then upload. Matching Code rows are updated."
          templateFileName={`${country.code}-states-import-template.xlsx`}
          templateSheetName="States"
          templateHeaders={["Code", "Name", "Active"]}
          templateSampleRows={[
            { Code: "SEL", Name: "Selangor", Active: "Yes" },
            { Code: "KUL", Name: "Kuala Lumpur", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>
              <span className="hint"> · Optional: </span>
              <code>Active</code> (Yes/No)
            </>
          }
          runImport={runStatesImport}
          onClose={() => setImportStatesOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["states", country.id] })}
        />
      ) : null}

      {importCitiesOpen && state ? (
        <ExcelImportModal
          title={`Import cities · ${state.name}`}
          description="Download the template, fill city names for this state, then upload. Matching Name rows are updated."
          templateFileName={`${state.code}-cities-import-template.xlsx`}
          templateSheetName="Cities"
          templateHeaders={["Name", "Active"]}
          templateSampleRows={[
            { Name: "Petaling Jaya", Active: "Yes" },
            { Name: "Shah Alam", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Name</code>
              <span className="hint"> · Optional: </span>
              <code>Active</code> (Yes/No)
            </>
          }
          runImport={runCitiesImport}
          onClose={() => setImportCitiesOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["cities", state.id] })}
        />
      ) : null}
    </>
  );

  if (state && country) {
    return (
      <>
        <DetailHeader
          title={state.name}
          subtitle={`${country.name} (${country.code}) · state ${state.code}`}
          onBack={() => {
            setState(null);
            setFocusAddCity(false);
          }}
          backLabel="States"
        />

        <Gated permission={FshPermissions.lookups.manage}>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="cbody">
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
                <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                  Add city
                </p>
                <ExcelToolbar
                  exportDisabled={cities.length === 0}
                  onExport={() =>
                    exportRowsToExcel(
                      `${state.code}-cities.xlsx`,
                      "Cities",
                      [
                        { header: "Name", key: "name", value: (c: CityDto) => c.name },
                        { header: "Active", key: "active", value: (c: CityDto) => activeLabel(c.isActive) },
                      ],
                      cities,
                    )
                  }
                  onImport={() => setImportCitiesOpen(true)}
                />
              </div>
              <div className="filterbar filterbar-auto">
                <div className="field" style={{ margin: 0 }}>
                  <label>City name</label>
                  <input
                    value={cityName}
                    onChange={(e) => setCityName(e.target.value)}
                    autoFocus={focusAddCity}
                    placeholder="e.g. Petaling Jaya"
                  />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label aria-hidden="true">&nbsp;</label>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    disabled={addCityMut.isPending || !cityName.trim()}
                    onClick={() => addCityMut.mutate()}
                  >
                    Add city
                  </button>
                </div>
              </div>
            </div>
          </div>
        </Gated>

        <div className="card">
          <DataTable
            rows={cities}
            columns={cityColumns}
            rowKey={(c) => c.id}
            loading={citiesQuery.isPending}
            empty={<EmptyState icon="list">No cities yet — add the first city above.</EmptyState>}
            initialSort={{ id: "name", dir: "asc" }}
            footer={<TableFooter total={cities.length} active={cities.filter((c) => c.isActive).length} />}
          />
        </div>
        {editModals}
      </>
    );
  }

  if (country) {
    return (
      <>
        <DetailHeader
          title={country.name}
          subtitle={`Country · ${country.code}`}
          onBack={() => {
            setCountry(null);
            setFocusAddState(false);
            setCreatedStateShortcut(null);
          }}
          backLabel="All countries"
        />

        <Gated permission={FshPermissions.lookups.manage}>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="cbody">
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
                <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                  Add state / province
                </p>
                <ExcelToolbar
                  exportDisabled={states.length === 0}
                  onExport={() =>
                    exportRowsToExcel(
                      `${country.code}-states.xlsx`,
                      "States",
                      [
                        { header: "Code", key: "code", value: (s: StateDto) => s.code },
                        { header: "Name", key: "name", value: (s: StateDto) => s.name },
                        { header: "Active", key: "active", value: (s: StateDto) => activeLabel(s.isActive) },
                      ],
                      states,
                    )
                  }
                  onImport={() => setImportStatesOpen(true)}
                />
              </div>
              <div className="filterbar filterbar-auto">
                <div className="field" style={{ margin: 0 }}>
                  <label>State code</label>
                  <input
                    value={stateCode}
                    onChange={(e) => setStateCode(e.target.value)}
                    autoFocus={focusAddState}
                    placeholder="e.g. SEL"
                  />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label>State name</label>
                  <input
                    value={stateName}
                    onChange={(e) => setStateName(e.target.value)}
                    placeholder="Selangor"
                  />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label aria-hidden="true">&nbsp;</label>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    disabled={addStateMut.isPending || !stateCode.trim() || !stateName.trim()}
                    onClick={() => addStateMut.mutate()}
                  >
                    Add state
                  </button>
                </div>
              </div>
              {createdStateShortcut ? (
                <div className="shortcut-banner">
                  <span>
                    Created <strong>{createdStateShortcut.name}</strong>.
                  </span>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    onClick={async () => {
                      const refreshed = await listStates(country.id, false);
                      const hit = refreshed.find((s) => s.id === createdStateShortcut.id);
                      if (hit) openState(hit, true);
                    }}
                  >
                    Open &amp; add cities
                  </button>
                </div>
              ) : null}
            </div>
          </div>
        </Gated>

        <div className="card">
          <DataTable
            rows={states}
            columns={stateColumns}
            rowKey={(s) => s.id}
            loading={statesQuery.isPending}
            empty={<EmptyState icon="list">No states yet — add the first state above.</EmptyState>}
            initialSort={{ id: "name", dir: "asc" }}
            onRowClick={(s) => openState(s)}
            footer={<TableFooter total={states.length} active={states.filter((s) => s.isActive).length} />}
          />
        </div>
        {editModals}
      </>
    );
  }

  return (
    <>
      <Gated permission={FshPermissions.lookups.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new country
              </p>
              <ExcelToolbar
                exportDisabled={countries.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "countries.xlsx",
                    "Countries",
                    [
                      { header: "Code", key: "code", value: (c: CountryDto) => c.code },
                      { header: "Name", key: "name", value: (c: CountryDto) => c.name },
                      { header: "Active", key: "active", value: (c: CountryDto) => activeLabel(c.isActive) },
                    ],
                    countries,
                  )
                }
                onImport={() => setImportCountriesOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Country code (2 letters)</label>
                <input
                  value={countryCode}
                  onChange={(e) => setCountryCode(e.target.value.toUpperCase())}
                  placeholder="MY"
                  maxLength={2}
                />
                <span className="hint">ISO 3166-1 alpha-2 — e.g. MY, SG, US</span>
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Country name</label>
                <input
                  value={countryName}
                  onChange={(e) => setCountryName(e.target.value)}
                  placeholder="Malaysia"
                />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={addCountry.isPending || countryCode.trim().length !== 2 || !countryName.trim()}
                  onClick={() => {
                    if (countryCode.trim().length !== 2) {
                      showError("Country code must be exactly 2 letters (e.g. MY).");
                      return;
                    }
                    addCountry.mutate();
                  }}
                >
                  Add country
                </button>
              </div>
            </div>
            {createdCountryShortcut ? (
              <div className="shortcut-banner">
                <span>
                  Created <strong>{createdCountryShortcut.name}</strong> ({createdCountryShortcut.code}).
                </span>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  onClick={async () => {
                    const refreshed = await listCountries(false);
                    const hit = refreshed.find((c) => c.code === createdCountryShortcut.code);
                    if (hit) openCountry(hit, true);
                  }}
                >
                  Open &amp; add states
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={countries}
          columns={countryColumns}
          rowKey={(c) => c.id}
          loading={countriesQuery.isPending}
          loadingLabel="Loading countries…"
          empty={<EmptyState icon="list">No countries yet.</EmptyState>}
          initialSort={{ id: "name", dir: "asc" }}
          onRowClick={(c) => openCountry(c)}
          footer={<TableFooter total={countries.length} active={countries.filter((c) => c.isActive).length} />}
        />
      </div>
      {editModals}
    </>
  );
}

function BanksTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showError, showErrorFrom } = useErrorDialog();
  const [importOpen, setImportOpen] = useState(false);
  const [name, setName] = useState("");
  const [countryCode, setCountryCode] = useState("");
  const [swiftCode, setSwiftCode] = useState("");
  const [editing, setEditing] = useState<BankDto | null>(null);
  const [editName, setEditName] = useState("");
  const [editCountry, setEditCountry] = useState("");
  const [editSwift, setEditSwift] = useState("");
  const [pendingDelete, setPendingDelete] = useState<BankDto | null>(null);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);

  const banksQuery = useQuery({
    queryKey: ["banks", false],
    queryFn: () => listBanks(undefined, false),
  });

  const addBank = useMutation({
    mutationFn: () =>
      createBank({
        name: name.trim(),
        countryCode: countryCode.trim(),
        swiftCode: swiftCode.trim() || undefined,
      }),
    onSuccess: () => {
      setName("");
      setCountryCode("");
      setSwiftCode("");
      void qc.invalidateQueries({ queryKey: ["banks"] });
    },
    onError: (e) => showErrorFrom(e, "Could not add bank"),
  });

  const saveEdit = useMutation({
    mutationFn: () =>
      updateBank(editing!.id, {
        name: editName.trim(),
        countryCode: editCountry.trim(),
        swiftCode: editSwift.trim() || undefined,
      }),
    onSuccess: () => {
      setEditing(null);
      void qc.invalidateQueries({ queryKey: ["banks"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save bank"),
  });

  const toggleBank = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setBankActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["banks"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update bank"),
  });

  const removeBank = useMutation({
    mutationFn: (id: string) => deleteBank(id),
    onSuccess: () => {
      setPendingDelete(null);
      void qc.invalidateQueries({ queryKey: ["banks"] });
    },
    onError: (e) => showErrorFrom(e, "Could not delete bank"),
  });

  const runBankImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listBanks(undefined, false);
    const byKey = new Map(
      existing.map((b) => [`${b.countryCode.toUpperCase()}|${b.name.toUpperCase()}`, b]),
    );

    return runExcelRows(file, onProgress, async (row) => {
      const bankName = cellStr(row, "Name", "Bank name", "Bank Name");
      const cc = cellStr(row, "Country", "Country code", "Country Code").toUpperCase();
      const swiftRaw = cellStr(row, "SWIFT", "Swift", "SwiftCode", "Swift code");
      const swift = swiftRaw || undefined;
      const active = cellActive(row);

      if (!bankName) throw new Error("Name is required.");
      if (cc.length !== 2) throw new Error("Country must be a 2-letter ISO code (e.g. MY).");
      if (swift && swift.length > 20) throw new Error("SWIFT must be at most 20 characters.");

      const key = `${cc}|${bankName.toUpperCase()}`;
      const hit = byKey.get(key);
      if (hit) {
        await updateBank(hit.id, { name: bankName, countryCode: cc, swiftCode: swift });
        if (active != null && active !== hit.isActive) await setBankActive(hit.id, active);
        byKey.set(key, {
          ...hit,
          name: bankName,
          countryCode: cc,
          swiftCode: swift ?? null,
          isActive: active ?? hit.isActive,
        });
      } else {
        const id = await createBank({ name: bankName, countryCode: cc, swiftCode: swift });
        if (active === false) await setBankActive(id, false);
        byKey.set(key, {
          id,
          name: bankName,
          countryCode: cc,
          swiftCode: swift ?? null,
          isActive: active ?? true,
          createdOnUtc: new Date().toISOString(),
        });
      }
    });
  };

  const banks = banksQuery.data ?? [];

  const openEdit = (b: BankDto) => {
    setEditing(b);
    setEditName(b.name);
    setEditCountry(b.countryCode);
    setEditSwift(b.swiftCode ?? "");
  };

  const columns: DataTableColumn<BankDto>[] = useMemo(
    () => [
      {
        id: "name",
        header: "Name",
        sortable: true,
        sortValue: (b) => b.name,
        render: (b) => <div style={{ fontWeight: 600 }}>{b.name}</div>,
      },
      {
        id: "country",
        header: "Country",
        sortable: true,
        sortValue: (b) => b.countryCode,
        render: (b) => <span className="hint">{b.countryCode}</span>,
      },
      {
        id: "swift",
        header: "SWIFT",
        sortable: true,
        sortValue: (b) => b.swiftCode ?? "",
        render: (b) => (
          <span className="hint" style={{ fontFamily: "ui-monospace, monospace", fontSize: 12 }}>
            {b.swiftCode ?? "—"}
          </span>
        ),
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (b) => (b.isActive ? 1 : 0),
        render: (b) => <StatusBadge active={b.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (b) => Date.parse(b.createdOnUtc) || 0,
        render: (b) => <span className="hint">{dateTimeMY(b.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (b) => (
          <div className="row-actions">
            <HistoryButton label={b.name} entityId={b.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.lookups.manage}>
              <IconButton icon="edit" label="Edit" onClick={() => openEdit(b)} />
              <IconButton
                icon={b.isActive ? "x" : "check"}
                label={b.isActive ? "Deactivate" : "Activate"}
                danger={b.isActive}
                onClick={() => setPendingStatus({ id: b.id, name: b.name, activate: !b.isActive })}
              />
              <IconButton icon="trash" label="Delete" danger onClick={() => setPendingDelete(b)} />
            </Gated>
          </div>
        ),
      },
    ],
    [onHistory],
  );

  return (
    <>
      <Gated permission={FshPermissions.lookups.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add new bank
              </p>
              <ExcelToolbar
                exportDisabled={banks.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "banks.xlsx",
                    "Banks",
                    [
                      { header: "Name", key: "name", value: (b: BankDto) => b.name },
                      { header: "Country", key: "country", value: (b: BankDto) => b.countryCode },
                      { header: "SWIFT", key: "swift", value: (b: BankDto) => b.swiftCode ?? "" },
                      { header: "Active", key: "active", value: (b: BankDto) => activeLabel(b.isActive) },
                    ],
                    banks,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar filterbar-auto">
              <div className="field" style={{ margin: 0 }}>
                <label>Bank name</label>
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="e.g. Maybank"
                />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Country code (2 letters)</label>
                <input
                  value={countryCode}
                  onChange={(e) => setCountryCode(e.target.value.toUpperCase())}
                  placeholder="MY"
                  maxLength={2}
                />
                <span className="hint">ISO alpha-2 — e.g. MY</span>
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>SWIFT (optional)</label>
                <input
                  value={swiftCode}
                  onChange={(e) => setSwiftCode(e.target.value.toUpperCase())}
                  placeholder="MBBEMYKL"
                  maxLength={20}
                />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label aria-hidden="true">&nbsp;</label>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={addBank.isPending || !name.trim() || countryCode.trim().length !== 2}
                  onClick={() => {
                    const cc = countryCode.trim();
                    if (cc.length !== 2) {
                      showError("Country code must be exactly 2 letters (e.g. MY).");
                      return;
                    }
                    addBank.mutate();
                  }}
                >
                  Add bank
                </button>
              </div>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={banks}
          columns={columns}
          rowKey={(b) => b.id}
          loading={banksQuery.isPending}
          loadingLabel="Loading banks…"
          empty={<EmptyState icon="clip">No banks yet.</EmptyState>}
          initialSort={{ id: "created", dir: "desc" }}
          footer={<TableFooter total={banks.length} active={banks.filter((b) => b.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import banks from Excel"
          description="Download the empty template, fill in your banks, then upload the file. Matching Country + Name rows are updated; new rows are created."
          templateFileName="banks-import-template.xlsx"
          templateSheetName="Banks"
          templateHeaders={["Name", "Country", "SWIFT", "Active"]}
          templateSampleRows={[
            { Name: "Maybank", Country: "MY", SWIFT: "MBBEMYKL", Active: "Yes" },
            { Name: "CIMB Bank", Country: "MY", SWIFT: "CIBBMYKL", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Name</code>, <code>Country</code> (2 letters)
              <span className="hint"> · Optional: </span>
              <code>SWIFT</code>, <code>Active</code> (Yes/No)
            </>
          }
          runImport={runBankImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["banks"] })}
        />
      ) : null}

      {editing ? (
        <Modal
          title={`Edit bank · ${editing.name}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending || !editName.trim() || editCountry.trim().length !== 2}
                onClick={() => {
                  if (editCountry.trim().length !== 2) {
                    showError("Country code must be exactly 2 letters (e.g. MY).");
                    return;
                  }
                  saveEdit.mutate();
                }}
              >
                Save
              </button>
            </>
          }
        >
          <div className="filterbar filterbar-auto" style={{ gap: 12 }}>
            <div className="field" style={{ margin: 0 }}>
              <label>Bank name</label>
              <input value={editName} onChange={(e) => setEditName(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Country code (2 letters)</label>
              <input
                value={editCountry}
                onChange={(e) => setEditCountry(e.target.value.toUpperCase())}
                maxLength={2}
              />
              <span className="hint">ISO alpha-2 — e.g. MY</span>
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>SWIFT (optional)</label>
              <input
                value={editSwift}
                onChange={(e) => setEditSwift(e.target.value.toUpperCase())}
                maxLength={20}
              />
            </div>
          </div>
        </Modal>
      ) : null}

      {pendingDelete ? (
        <ConfirmModal
          title="Delete bank"
          icon="x"
          danger
          busy={removeBank.isPending}
          confirmLabel="Delete"
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              Delete bank <strong>{pendingDelete.name}</strong> ({pendingDelete.countryCode})? This soft-deletes
              the record; it will no longer appear in lists.
            </p>
          }
          onCancel={() => setPendingDelete(null)}
          onConfirm={() => removeBank.mutate(pendingDelete.id)}
        />
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate bank" : "Deactivate bank"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggleBank.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} bank{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggleBank.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}

function OrgTab({ onHistory }: { onHistory: HistoryOpen }) {
  const qc = useQueryClient();
  const { showErrorFrom } = useErrorDialog();
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [type, setType] = useState("");
  const [parentId, setParentId] = useState("");
  const [importOpen, setImportOpen] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<PendingStatus | null>(null);

  const orgQuery = useQuery({
    queryKey: ["org-units", false],
    queryFn: () => listOrgUnits(undefined, false),
  });

  const addOrg = useMutation({
    mutationFn: () =>
      createOrgUnit({
        code: code.trim(),
        name: name.trim(),
        type: type.trim(),
        parentId: parentId.trim() || null,
      }),
    onSuccess: () => {
      setCode("");
      setName("");
      setType("");
      setParentId("");
      void qc.invalidateQueries({ queryKey: ["org-units"] });
    },
    onError: (e) => showErrorFrom(e, "Could not add organisation unit"),
  });

  const toggleOrg = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setOrgUnitActive(id, active),
    onSuccess: () => {
      setPendingStatus(null);
      void qc.invalidateQueries({ queryKey: ["org-units"] });
    },
    onError: (e) => showErrorFrom(e, "Could not update organisation unit"),
  });

  const units = orgQuery.data ?? [];

  const runOrgImport: ExcelImportRunner = async (file, onProgress) => {
    const existing = await listOrgUnits(undefined, false);
    const byCode = new Map(existing.map((u) => [u.code.toLowerCase(), u]));

    return runExcelRows(file, onProgress, async (row) => {
      const unitCode = cellStr(row, "Code");
      const unitName = cellStr(row, "Name");
      const unitType = cellStr(row, "Type");
      const parentCode = cellStr(row, "ParentCode", "Parent code", "Parent");
      const active = cellActive(row);
      if (!unitCode) throw new Error("Code is required.");
      if (!unitName) throw new Error("Name is required.");
      if (!unitType) throw new Error("Type is required.");

      let parent: string | null = null;
      if (parentCode) {
        const parentUnit = byCode.get(parentCode.toLowerCase());
        if (!parentUnit) throw new Error(`Parent code "${parentCode}" was not found.`);
        parent = parentUnit.id;
      }

      const hit = byCode.get(unitCode.toLowerCase());
      if (hit) {
        if (active != null && active !== hit.isActive) await setOrgUnitActive(hit.id, active);
        byCode.set(unitCode.toLowerCase(), {
          ...hit,
          isActive: active ?? hit.isActive,
        });
      } else {
        const id = await createOrgUnit({
          code: unitCode,
          name: unitName,
          type: unitType,
          parentId: parent,
        });
        if (active === false) await setOrgUnitActive(id, false);
        byCode.set(unitCode.toLowerCase(), {
          id,
          code: unitCode,
          name: unitName,
          type: unitType,
          parentId: parent,
          isActive: active ?? true,
          createdOnUtc: new Date().toISOString(),
        });
      }
    });
  };

  const columns: DataTableColumn<OrgUnitDto>[] = useMemo(
    () => [
      {
        id: "code",
        header: "Code",
        sortable: true,
        sortValue: (u) => u.code,
        render: (u) => <span style={{ fontWeight: 600 }}>{u.code}</span>,
      },
      {
        id: "name",
        header: "Name",
        sortable: true,
        sortValue: (u) => u.name,
        render: (u) => u.name,
      },
      {
        id: "type",
        header: "Type",
        sortable: true,
        sortValue: (u) => u.type,
        render: (u) => <span className="hint">{u.type}</span>,
      },
      {
        id: "status",
        header: "Status",
        sortable: true,
        sortValue: (u) => (u.isActive ? 1 : 0),
        render: (u) => <StatusBadge active={u.isActive} />,
      },
      {
        id: "created",
        header: "Created",
        sortable: true,
        sortValue: (u) => Date.parse(u.createdOnUtc) || 0,
        render: (u) => <span className="hint">{dateTimeMY(u.createdOnUtc)}</span>,
      },
      {
        id: "actions",
        header: "",
        align: "right",
        interactive: true,
        render: (u) => (
          <div className="row-actions">
            <HistoryButton label={u.name} entityId={u.id} onOpen={onHistory} />
            <Gated permission={FshPermissions.org.manage}>
              <IconButton
                icon={u.isActive ? "x" : "check"}
                label={u.isActive ? "Deactivate" : "Activate"}
                danger={u.isActive}
                onClick={() => setPendingStatus({ id: u.id, name: u.name, activate: !u.isActive })}
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
      <Gated permission={FshPermissions.org.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
              <p className="hint" style={{ margin: 0, fontWeight: 600, flex: 1 }}>
                Add organisation unit
              </p>
              <ExcelToolbar
                exportDisabled={units.length === 0}
                onExport={() =>
                  exportRowsToExcel(
                    "org-units.xlsx",
                    "OrgUnits",
                    [
                      { header: "Code", key: "code", value: (u: OrgUnitDto) => u.code },
                      { header: "Name", key: "name", value: (u: OrgUnitDto) => u.name },
                      { header: "Type", key: "type", value: (u: OrgUnitDto) => u.type },
                      {
                        header: "ParentCode",
                        key: "parent",
                        value: (u: OrgUnitDto) => units.find((p) => p.id === u.parentId)?.code ?? "",
                      },
                      { header: "Active", key: "active", value: (u: OrgUnitDto) => activeLabel(u.isActive) },
                    ],
                    units,
                  )
                }
                onImport={() => setImportOpen(true)}
              />
            </div>
            <div className="filterbar2">
              <div className="field" style={{ margin: 0, minWidth: 120 }}>
                <label>Code</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0, minWidth: 160 }}>
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0, minWidth: 140 }}>
                <label>Type</label>
                <input value={type} onChange={(e) => setType(e.target.value)} placeholder="e.g. Department" />
              </div>
              <div className="field" style={{ margin: 0, minWidth: 180 }}>
                <label>Parent (optional)</label>
                <select value={parentId} onChange={(e) => setParentId(e.target.value)}>
                  <option value="">None</option>
                  {units.map((u: OrgUnitDto) => (
                    <option key={u.id} value={u.id}>
                      {u.code} — {u.name}
                    </option>
                  ))}
                </select>
              </div>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                disabled={addOrg.isPending || !code.trim() || !name.trim() || !type.trim()}
                onClick={() => addOrg.mutate()}
              >
                Add org unit
              </button>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        <DataTable
          rows={units}
          columns={columns}
          rowKey={(u) => u.id}
          loading={orgQuery.isPending}
          loadingLabel="Loading org units…"
          empty={<EmptyState icon="users">No organisation units.</EmptyState>}
          initialSort={{ id: "code", dir: "asc" }}
          footer={<TableFooter total={units.length} active={units.filter((u) => u.isActive).length} />}
        />
      </div>

      {importOpen ? (
        <ExcelImportModal
          title="Import organisation units"
          description="Download the template, fill codes/names/types, then upload. Existing Code rows only update Active; new codes are created. Put parent rows above children (or import parents first)."
          templateFileName="org-units-import-template.xlsx"
          templateSheetName="OrgUnits"
          templateHeaders={["Code", "Name", "Type", "ParentCode", "Active"]}
          templateSampleRows={[
            { Code: "HQ", Name: "Head Office", Type: "Company", ParentCode: "", Active: "Yes" },
            { Code: "FIN", Name: "Finance", Type: "Department", ParentCode: "HQ", Active: "Yes" },
          ]}
          columnsHint={
            <>
              <span className="hint">Required: </span>
              <code>Code</code>, <code>Name</code>, <code>Type</code>
              <span className="hint"> · Optional: </span>
              <code>ParentCode</code>, <code>Active</code> (Yes/No)
            </>
          }
          runImport={runOrgImport}
          onClose={() => setImportOpen(false)}
          onImported={() => void qc.invalidateQueries({ queryKey: ["org-units"] })}
        />
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate org unit" : "Deactivate org unit"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggleOrg.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            <p className="hint" style={{ marginTop: 0 }}>
              {pendingStatus.activate ? "Activate" : "Deactivate"} org unit{" "}
              <strong>{pendingStatus.name}</strong>?
            </p>
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggleOrg.mutate({ id: pendingStatus.id, active: pendingStatus.activate })}
        />
      ) : null}
    </>
  );
}
