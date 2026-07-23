import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteView, getViewFields, listViews, VIEW_RECORD_TYPES, type SavedViewDto } from "@/api/views";
import { useAuth } from "@/auth/use-auth";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { ConfirmModal, EmptyState, Modal, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { FshPermissions } from "@/lib/fsh-permissions";
import { ViewBuilder } from "@/components/views/SavedViewControls";

/**
 * Saved Views home — every view the caller can see (mine / shared / system) across the
 * record types the registry supports, in one place. "New view" works for any record type
 * from here; editing/sharing/deleting reuse the same `ViewBuilder` used inline on the lists.
 */

const RECORD_TYPE_LABEL: Record<string, string> = { Requisition: "Requisitions", Rfq: "RFQs" };

type Tab = "all" | (typeof VIEW_RECORD_TYPES)[number];

function errMsg(e: unknown): string {
  if (e instanceof ApiRequestError) return e.problem?.detail ?? e.message;
  if (e instanceof Error) return e.message;
  return "Something went wrong.";
}

export function SavedViewsHome() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { user } = useAuth();
  const myId = user?.id;

  const [tab, setTab] = useState<Tab>("all");
  const [creating, setCreating] = useState(false);
  const [newType, setNewType] = useState<(typeof VIEW_RECORD_TYPES)[number]>("Requisition");
  const [builder, setBuilder] = useState<{ recordType: string; existing: SavedViewDto | null } | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<SavedViewDto | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const { data: views = [], isPending } = useQuery({ queryKey: ["views", "all"], queryFn: () => listViews() });

  const { data: newTypeFields = [] } = useQuery({
    queryKey: ["view-fields", builder?.recordType ?? newType],
    queryFn: () => getViewFields(builder?.recordType ?? newType),
    staleTime: Infinity,
  });

  const refresh = () => void qc.invalidateQueries({ queryKey: ["views"] });

  const remove = useMutation({
    mutationFn: (id: string) => deleteView(id),
    onSuccess: () => {
      setConfirmDelete(null);
      refresh();
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const kindOf = (v: SavedViewDto): "System" | "Mine" | "Shared" =>
    v.isSystem ? "System" : v.ownerUserId === myId ? "Mine" : "Shared";

  const rows = useMemo(() => (tab === "all" ? views : views.filter((v) => v.recordType === tab)), [views, tab]);

  const openList = (v: SavedViewDto) => {
    if (v.recordType === "Requisition") void navigate(`/reqs?view=${v.id}`);
    else if (v.recordType === "Rfq") void navigate(`/rfqs?view=${v.id}`);
  };

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Saved Views</h1>
          <p>Every view you can run — yours, shared, and system — across requisitions and RFQs.</p>
        </div>
        <div className="spacer" />
        <div className="viewtoggle">
          {(
            [
              ["all", "list", "All"],
              ["Requisition", "doc", "Requisitions"],
              ["Rfq", "rfq", "RFQs"],
            ] as const
          ).map(([key, icon, label]) => (
            <button key={key} type="button" className={tab === key ? "on" : ""} onClick={() => setTab(key)}>
              <Icon name={icon} size={14} /> {label}
            </button>
          ))}
        </div>
        <Gated permission={FshPermissions.views.manageOwn}>
          <button type="button" className="btn btn-pri btn-sm" onClick={() => setCreating(true)}>
            <Icon name="plus" size={15} /> New view
          </button>
        </Gated>
      </div>

      {err ? <Notice tone="error">{err}</Notice> : null}

      <div className="card">
        {isPending ? (
          <Spinner label="Loading views…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>View</th>
                <th>Record type</th>
                <th>Code</th>
                <th className="amt">Criteria</th>
                <th>Kind</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((v) => {
                const kind = kindOf(v);
                const canEdit = !v.isSystem && v.ownerUserId === myId;
                return (
                  <tr key={v.id}>
                    <td style={{ fontWeight: 700, color: "var(--teal)" }}>{v.name}</td>
                    <td>{RECORD_TYPE_LABEL[v.recordType] ?? v.recordType}</td>
                    <td className="mono">{v.code}</td>
                    <td className="amt">{v.filters.length}</td>
                    <td>
                      <span className={`badge ${kind === "System" ? "b-blue" : kind === "Mine" ? "b-teal" : "b-grey"}`}>
                        {kind}
                        {!v.isSystem && v.isShared ? " · shared" : ""}
                      </span>
                    </td>
                    <td className="amt">
                      <div className="rowactions">
                        <button type="button" className="btn btn-ghost btn-sm" onClick={() => openList(v)}>
                          Open <Icon name="chev" size={13} />
                        </button>
                        {canEdit ? (
                          <>
                            <button
                              type="button"
                              className="btn btn-ghost btn-sm"
                              onClick={() => setBuilder({ recordType: v.recordType, existing: v })}
                            >
                              <Icon name="edit" size={14} /> Edit
                            </button>
                            <button
                              type="button"
                              className="btn btn-ghost btn-sm"
                              style={{ color: "var(--red)" }}
                              onClick={() => setConfirmDelete(v)}
                            >
                              Delete
                            </button>
                          </>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={6}>
                    <EmptyState icon="eye">No saved views yet — create the first one.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>

      {creating ? (
        <Modal
          title="New saved view"
          icon="eye"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setCreating(false)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                onClick={() => {
                  setCreating(false);
                  setBuilder({ recordType: newType, existing: null });
                }}
              >
                Choose fields…
              </button>
            </>
          }
        >
          <div className="field">
            <label>Record type</label>
            <select
              value={newType}
              onChange={(e) => setNewType(e.target.value as (typeof VIEW_RECORD_TYPES)[number])}
            >
              {VIEW_RECORD_TYPES.map((rt) => (
                <option key={rt} value={rt}>
                  {RECORD_TYPE_LABEL[rt] ?? rt}
                </option>
              ))}
            </select>
          </div>
        </Modal>
      ) : null}

      {builder ? (
        <ViewBuilder
          recordType={builder.recordType}
          existing={builder.existing}
          defaultColumns={newTypeFields.slice(0, 5).map((f) => f.fieldKey)}
          onClose={() => setBuilder(null)}
          onSaved={() => {
            setBuilder(null);
            refresh();
          }}
        />
      ) : null}

      {confirmDelete ? (
        <ConfirmModal
          title="Delete saved view"
          icon="x"
          body={
            <>
              Delete <b>{confirmDelete.name}</b>? This cannot be undone.
            </>
          }
          confirmLabel="Delete"
          danger
          busy={remove.isPending}
          onCancel={() => setConfirmDelete(null)}
          onConfirm={() => remove.mutate(confirmDelete.id)}
        />
      ) : null}
    </>
  );
}
