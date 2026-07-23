import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createView,
  getViewFields,
  shareView,
  updateView,
  type SaveViewRequest,
  type SavedViewColumnDto,
  type SavedViewDto,
  type SavedViewFilterDto,
  type ViewFieldDto,
  type ViewOperator,
} from "@/api/views";
import { useAuth } from "@/auth/use-auth";
import { FshPermissions } from "@/lib/fsh-permissions";
import { ApiRequestError } from "@/lib/api-client";
import { Icon } from "@/components/Icon";
import { Modal, Notice } from "@/components/ui";

/**
 * Saved-views UI (Phase 5). `ViewPicker` groups System / Shared / My views for a single
 * record type's list screen; `ViewBuilder` is the criteria + columns editor used both from
 * the Saved Views home and from each list's "Edit"/"New view" affordance.
 */

/** Operator set per registry DataType — mirrors `SavedViewFilterExecutor`'s wave-1 support. */
const OPERATORS_FOR: Record<string, ViewOperator[]> = {
  Code: ["Eq", "Neq", "In", "Contains", "StartsWith", "IsEmpty", "IsNotEmpty"],
  Text: ["Eq", "Neq", "In", "Contains", "StartsWith", "IsEmpty", "IsNotEmpty"],
  Enum: ["Eq", "Neq", "In", "Contains", "StartsWith", "IsEmpty", "IsNotEmpty"],
  Tags: ["Eq", "Neq", "In", "Contains", "StartsWith", "IsEmpty", "IsNotEmpty"],
  Date: ["Eq", "Neq", "Gte", "Lte", "Between", "IsEmpty", "IsNotEmpty"],
  Instant: ["Eq", "Neq", "Gte", "Lte", "Between", "IsEmpty", "IsNotEmpty"],
  Money: ["Eq", "Neq", "Gte", "Lte", "Between", "IsEmpty", "IsNotEmpty"],
  Number: ["Eq", "Neq", "Gte", "Lte", "Between", "IsEmpty", "IsNotEmpty"],
  Bool: ["Eq"],
};

const OPERATOR_LABEL: Record<ViewOperator, string> = {
  Eq: "is",
  Neq: "is not",
  In: "is one of",
  Contains: "contains",
  StartsWith: "starts with",
  IsEmpty: "is empty",
  IsNotEmpty: "is not empty",
  Gte: "≥",
  Lte: "≤",
  Between: "between",
};

function errMsg(e: unknown): string {
  if (e instanceof ApiRequestError) return e.problem?.detail ?? e.message;
  if (e instanceof Error) return e.message;
  return "Could not save the view.";
}

export function ViewPicker({
  views,
  selectedId,
  onSelect,
  onClear,
  onNew,
  onEdit,
  clearLabel = "Standard list",
}: {
  views: SavedViewDto[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  /** Called when the caller picks the "standard list" option — omit to disable clearing. */
  onClear?: () => void;
  onNew?: () => void;
  onEdit?: (view: SavedViewDto) => void;
  clearLabel?: string;
}) {
  const { user } = useAuth();
  const myId = user?.id;

  const selected = views.find((v) => v.id === selectedId) ?? null;
  const systemViews = views.filter((v) => v.isSystem);
  const sharedViews = views.filter((v) => !v.isSystem && v.isShared && v.ownerUserId !== myId);
  const myViews = views.filter((v) => !v.isSystem && v.ownerUserId === myId);
  const canEditSelected = !!selected && !selected.isSystem && selected.ownerUserId === myId;

  return (
    <div className="filterbar" style={{ marginBottom: 0 }}>
      <div className="field" style={{ margin: 0, minWidth: 220 }}>
        <label htmlFor="saved-view-picker">Saved view</label>
        <select
          id="saved-view-picker"
          value={selectedId ?? ""}
          onChange={(e) => {
            const v = e.target.value;
            if (v) onSelect(v);
            else onClear?.();
          }}
        >
          {onClear ? <option value="">{clearLabel}</option> : null}
          {systemViews.length > 0 ? (
            <optgroup label="System">
              {systemViews.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </optgroup>
          ) : null}
          {sharedViews.length > 0 ? (
            <optgroup label="Shared">
              {sharedViews.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </optgroup>
          ) : null}
          {myViews.length > 0 ? (
            <optgroup label="My views">
              {myViews.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </optgroup>
          ) : null}
        </select>
      </div>
      {onNew ? (
        <button type="button" className="btn btn-ghost btn-sm" style={{ alignSelf: "flex-end" }} onClick={onNew}>
          <Icon name="plus" size={14} /> View…
        </button>
      ) : null}
      {onEdit && canEditSelected && selected ? (
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          style={{ alignSelf: "flex-end" }}
          onClick={() => onEdit(selected)}
        >
          <Icon name="edit" size={14} /> Edit
        </button>
      ) : null}
    </div>
  );
}

function CriterionValueInput({
  field,
  operator,
  value,
  onChange,
}: {
  field: ViewFieldDto;
  operator: string;
  value: string;
  onChange: (v: string) => void;
}) {
  if (operator === "IsEmpty" || operator === "IsNotEmpty") return null;
  if (operator === "In") {
    return (
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Comma-separated values"
      />
    );
  }
  switch (field.dataType) {
    case "Bool":
      return (
        <select value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">—</option>
          <option value="true">True</option>
          <option value="false">False</option>
        </select>
      );
    case "Date":
      return <input type="date" value={value} onChange={(e) => onChange(e.target.value)} />;
    case "Instant":
      return <input type="datetime-local" value={value} onChange={(e) => onChange(e.target.value)} />;
    case "Money":
    case "Number":
      return <input type="number" step="any" value={value} onChange={(e) => onChange(e.target.value)} />;
    default:
      return <input type="text" value={value} onChange={(e) => onChange(e.target.value)} />;
  }
}

interface CriterionRow {
  fieldKey: string;
  operator: string;
  value: string;
  value2: string;
}

export function ViewBuilder({
  recordType,
  existing,
  defaultColumns,
  onClose,
  onSaved,
}: {
  recordType: string;
  existing: SavedViewDto | null;
  /** Column seed for a brand-new view. */
  defaultColumns: string[];
  onClose: () => void;
  onSaved: (view: SavedViewDto) => void;
}) {
  const qc = useQueryClient();
  const { user } = useAuth();
  const canShare = (user?.permissions ?? []).includes(FshPermissions.views.manageShared);

  const { data: fields = [] } = useQuery({
    queryKey: ["view-fields", recordType],
    queryFn: () => getViewFields(recordType),
    staleTime: Infinity,
  });
  const byKey = useMemo(() => new Map(fields.map((f) => [f.fieldKey, f])), [fields]);

  const [name, setName] = useState(existing?.name ?? "");
  const [shared, setShared] = useState(existing?.isShared ?? false);
  const [criteria, setCriteria] = useState<CriterionRow[]>(
    existing?.filters.map((f) => ({
      fieldKey: f.fieldKey,
      operator: f.operator,
      value: f.value ?? "",
      value2: f.value2 ?? "",
    })) ?? [],
  );
  const [columns, setColumns] = useState<string[]>(existing?.columns.map((c) => c.fieldKey) ?? defaultColumns);
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: async () => {
      const filters: SavedViewFilterDto[] = criteria
        .filter((c) => c.fieldKey)
        .map((c, i) => ({
          fieldKey: c.fieldKey,
          operator: c.operator,
          groupIndex: 0,
          value: c.operator === "IsEmpty" || c.operator === "IsNotEmpty" ? "" : c.value,
          value2: c.operator === "Between" ? c.value2 : null,
          sort: i,
        }));
      const cols: SavedViewColumnDto[] = columns.map((k, i) => ({ fieldKey: k, label: null, sort: i, sortDirection: null }));
      const request: SaveViewRequest = { name: name.trim(), recordType, filters, columns: cols };
      const saved = existing ? await updateView(existing.id, request) : await createView(request);
      if (canShare && shared !== saved.isShared) {
        return shareView(saved.id, shared);
      }
      return saved;
    },
    onSuccess: (view) => {
      void qc.invalidateQueries({ queryKey: ["views"] });
      onSaved(view);
    },
    onError: (e) => setError(errMsg(e)),
  });

  const setRow = (i: number, patch: Partial<CriterionRow>) =>
    setCriteria((rows) => rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));

  const toggleColumn = (key: string, on: boolean) =>
    setColumns((cols) => (on ? [...cols, key] : cols.filter((c) => c !== key)));

  const firstField = fields[0]?.fieldKey ?? "";

  return (
    <Modal
      title={existing ? `Edit view — ${existing.name}` : "New saved view"}
      icon="eye"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-pri"
            disabled={save.isPending || !name.trim() || columns.length === 0}
            onClick={() => save.mutate()}
          >
            {existing ? "Save changes" : "Save view"}
          </button>
        </>
      }
    >
      <div className="field">
        <label>View name</label>
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. My open requisitions" />
      </div>

      <h4>Criteria</h4>
      {criteria.map((c, i) => {
        const field = byKey.get(c.fieldKey);
        const ops = OPERATORS_FOR[field?.dataType ?? "Text"] ?? ["Eq"];
        return (
          <div key={i} className="filterbar" style={{ marginBottom: 8, alignItems: "flex-end" }}>
            <div className="field" style={{ margin: 0, minWidth: 160 }}>
              <select
                value={c.fieldKey}
                onChange={(e) => setRow(i, { fieldKey: e.target.value, operator: "Eq", value: "", value2: "" })}
                aria-label={`Criterion ${i + 1} field`}
              >
                {fields.map((f) => (
                  <option key={f.fieldKey} value={f.fieldKey}>
                    {f.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field" style={{ margin: 0, minWidth: 130 }}>
              <select
                value={c.operator}
                onChange={(e) => setRow(i, { operator: e.target.value })}
                aria-label={`Criterion ${i + 1} operator`}
              >
                {ops.map((op) => (
                  <option key={op} value={op}>
                    {OPERATOR_LABEL[op]}
                  </option>
                ))}
              </select>
            </div>
            {field ? (
              <div className="field" style={{ margin: 0, minWidth: 140 }}>
                <CriterionValueInput field={field} operator={c.operator} value={c.value} onChange={(v) => setRow(i, { value: v })} />
              </div>
            ) : null}
            {field && c.operator === "Between" ? (
              <div className="field" style={{ margin: 0, minWidth: 140 }}>
                <CriterionValueInput field={field} operator="Eq" value={c.value2} onChange={(v) => setRow(i, { value2: v })} />
              </div>
            ) : null}
            <button
              type="button"
              className="btn btn-ghost btn-sm"
              onClick={() => setCriteria((rows) => rows.filter((_, j) => j !== i))}
              aria-label={`Remove criterion ${i + 1}`}
            >
              <Icon name="x" size={14} />
            </button>
          </div>
        );
      })}
      <button
        type="button"
        className="btn btn-out btn-sm"
        disabled={!firstField}
        onClick={() => setCriteria((rows) => [...rows, { fieldKey: firstField, operator: "Eq", value: "", value2: "" }])}
      >
        <Icon name="plus" size={14} /> Add criterion
      </button>

      <h4 style={{ marginTop: 18 }}>Columns</h4>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "6px 16px", marginBottom: 8 }}>
        {fields.map((f) => (
          <label key={f.fieldKey} style={{ display: "flex", alignItems: "center", gap: 6, minWidth: 160 }}>
            <input
              type="checkbox"
              style={{ width: "auto" }}
              checked={columns.includes(f.fieldKey)}
              onChange={(e) => toggleColumn(f.fieldKey, e.target.checked)}
            />
            {f.label}
          </label>
        ))}
        {fields.length === 0 ? <span className="hint">No fields registered for this record type.</span> : null}
      </div>

      {canShare ? (
        <label style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 14 }}>
          <input type="checkbox" style={{ width: "auto" }} checked={shared} onChange={(e) => setShared(e.target.checked)} />
          Shared — visible to everyone
        </label>
      ) : null}

      {error ? (
        <Notice tone="error" style={{ marginTop: 14 }}>
          {error}
        </Notice>
      ) : null}
    </Modal>
  );
}
