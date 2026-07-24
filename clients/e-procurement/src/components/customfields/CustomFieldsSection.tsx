import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  getCustomFieldValues,
  setCustomFieldValues,
  type CustomFieldValueDto,
  type CustomFieldValueInput,
} from "@/api/platform";
import { listCustomListItems } from "@/api/platform";
import { Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

type Draft = Record<string, CustomFieldValueInput>;

function toDraft(values: CustomFieldValueDto[]): Draft {
  const draft: Draft = {};
  for (const v of values) {
    draft[v.customFieldDefId] = {
      customFieldDefId: v.customFieldDefId,
      lineId: v.lineId ?? null,
      valueText: v.valueText ?? null,
      valueNumber: v.valueNumber ?? null,
      valueDate: v.valueDate ?? null,
      valueDateTime: v.valueDateTime ?? null,
      valueBool: v.valueBool ?? null,
      valueListCode: v.valueListCode ?? null,
      valueRefId: v.valueRefId ?? null,
      valueLabel: v.valueLabel ?? null,
    };
  }
  return draft;
}

function ListValueInput({ listKey, value, onChange }: { listKey: string; value: string | null; onChange: (v: string | null) => void }) {
  const { data: items } = useQuery({ queryKey: ["custom-list-items", listKey], queryFn: () => listCustomListItems(listKey) });
  return (
    <select value={value ?? ""} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">Select…</option>
      {(items ?? []).map((i) => (
        <option key={i.code} value={i.code}>
          {i.label}
        </option>
      ))}
    </select>
  );
}

/**
 * Drop this into any record's detail/form page to render every custom field that applies to its
 * record type, joined with whatever value exists for this record instance — the dynamic
 * rendering hook every existing form grows (Phase 5/6). Read-only when `readOnly` is set (e.g.
 * before the record has been saved and has no id yet).
 */
export function CustomFieldsSection({
  recordType,
  recordId,
  readOnly = false,
}: {
  recordType: string;
  recordId: string | null;
  readOnly?: boolean;
}) {
  const [draft, setDraft] = useState<Draft>({});
  const [dirty, setDirty] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const { data: values, isPending } = useQuery({
    queryKey: ["custom-field-values", recordType, recordId],
    queryFn: () => getCustomFieldValues(recordType, recordId!),
    enabled: Boolean(recordId),
  });

  useEffect(() => {
    if (values) {
      setDraft(toDraft(values));
      setDirty(false);
    }
  }, [values]);

  const save = useMutation({
    mutationFn: () => setCustomFieldValues(recordType, recordId!, Object.values(draft)),
    onSuccess: () => setDirty(false),
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const patch = (defId: string, patch: Partial<CustomFieldValueInput>) => {
    setDraft((prev) => ({ ...prev, [defId]: { ...prev[defId]!, ...patch } }));
    setDirty(true);
  };

  if (!recordId) return null;
  if (isPending) return <Spinner label="Loading custom fields…" />;
  if (!values || values.length === 0) return null;

  return (
    <div className="card" style={{ marginTop: 14 }}>
      <div className="chead">
        <h3>Custom fields</h3>
        {!readOnly && dirty ? (
          <button type="button" className="btn btn-pri btn-sm" disabled={save.isPending} onClick={() => save.mutate()}>
            Save custom fields
          </button>
        ) : null}
      </div>
      <div className="cbody">
        {err ? (
          <Notice tone="error" icon="x">
            {err}
          </Notice>
        ) : null}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
          {values.map((v) => {
            const d = draft[v.customFieldDefId];
            const disabled = readOnly || v.displayType === "Disabled";
            if (v.displayType === "Inline") {
              return (
                <div className="field" key={v.customFieldDefId}>
                  <label>{v.label}</label>
                  <p className="hint" style={{ margin: 0 }}>
                    {v.valueText ?? v.valueListCode ?? v.valueLabel ?? "—"}
                  </p>
                </div>
              );
            }
            return (
              <div className="field" key={v.customFieldDefId}>
                <label>
                  {v.label}
                  {v.isRequired ? " *" : ""}
                </label>
                {v.dataType === "LongText" ? (
                  <textarea
                    rows={3}
                    disabled={disabled}
                    value={d?.valueText ?? ""}
                    onChange={(e) => patch(v.customFieldDefId, { valueText: e.target.value || null })}
                  />
                ) : v.dataType === "Bool" ? (
                  <input
                    type="checkbox"
                    disabled={disabled}
                    checked={d?.valueBool ?? false}
                    onChange={(e) => patch(v.customFieldDefId, { valueBool: e.target.checked })}
                  />
                ) : v.dataType === "Date" ? (
                  <input
                    type="date"
                    disabled={disabled}
                    value={d?.valueDate ?? ""}
                    onChange={(e) => patch(v.customFieldDefId, { valueDate: e.target.value || null })}
                  />
                ) : v.dataType === "DateTime" ? (
                  <input
                    type="datetime-local"
                    disabled={disabled}
                    value={d?.valueDateTime ?? ""}
                    onChange={(e) => patch(v.customFieldDefId, { valueDateTime: e.target.value || null })}
                  />
                ) : v.dataType === "Int" || v.dataType === "Decimal" || v.dataType === "Money" || v.dataType === "Percent" ? (
                  <input
                    type="number"
                    disabled={disabled}
                    value={d?.valueNumber ?? ""}
                    onChange={(e) => patch(v.customFieldDefId, { valueNumber: e.target.value ? Number(e.target.value) : null })}
                  />
                ) : v.dataType === "ListValue" && v.listKey ? (
                  <ListValueInput
                    listKey={v.listKey}
                    value={d?.valueListCode ?? null}
                    onChange={(val) => patch(v.customFieldDefId, { valueListCode: val })}
                  />
                ) : v.dataType === "RecordRef" ? (
                  <p className="hint" style={{ margin: 0 }}>
                    {v.valueLabel ?? "Not set"} <span className="mut">(reference picker not built yet)</span>
                  </p>
                ) : (
                  <input
                    type="text"
                    disabled={disabled}
                    value={d?.valueText ?? ""}
                    onChange={(e) => patch(v.customFieldDefId, { valueText: e.target.value || null })}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
