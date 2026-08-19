import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  listOrgUnits,
  listSegmentAssignments,
  removeSegmentAssignment,
  setSegmentAssignment,
  type OrgUnitDto,
  type SegmentAssignmentDto,
} from "@/api/platform";
import { Notice, Spinner } from "@/components/ui";
import { ORG_UNIT_TYPES } from "@/lib/org-unit-types";
import { ApiRequestError } from "@/lib/api-client";

const DIMENSION_LABEL: Record<string, string> = {
  Department: "Department",
  Location: "Location",
  CostCentre: "Cost centre",
  Category: "Category",
  Project: "Project",
};

/**
 * Header segment tags — one OrgUnit pick per dimension (Platform SegmentAssignment).
 * Renders only dimensions that have active org units (or an existing assignment).
 */
export function SegmentsSection({
  recordType,
  recordId,
  readOnly = false,
  title = "Segments",
}: {
  recordType: string;
  recordId: string | null;
  readOnly?: boolean;
  title?: string;
}) {
  const qc = useQueryClient();
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [err, setErr] = useState<string | null>(null);

  const { data: assignments = [], isPending: assignPending } = useQuery({
    queryKey: ["segment-assignments", recordType, recordId],
    queryFn: () => listSegmentAssignments(recordType, recordId!),
    enabled: Boolean(recordId),
  });
  const { data: orgUnits = [], isPending: unitsPending } = useQuery({
    queryKey: ["org-units", true],
    queryFn: () => listOrgUnits(undefined, true),
  });

  const headerAssignments = useMemo(
    () => assignments.filter((a) => !a.lineId),
    [assignments],
  );

  const unitsByType = useMemo(() => {
    const map = new Map<string, OrgUnitDto[]>();
    for (const u of orgUnits) {
      const arr = map.get(u.type) ?? [];
      arr.push(u);
      map.set(u.type, arr);
    }
    return map;
  }, [orgUnits]);

  const dimensions = useMemo(() => {
    return ORG_UNIT_TYPES.filter(
      (d) => (unitsByType.get(d)?.length ?? 0) > 0 || headerAssignments.some((a) => a.dimension === d),
    );
  }, [unitsByType, headerAssignments]);

  useEffect(() => {
    const next: Record<string, string> = {};
    for (const d of ORG_UNIT_TYPES) {
      const hit = headerAssignments.find((a) => a.dimension === d);
      next[d] = hit?.orgUnitId ?? "";
    }
    setDraft(next);
  }, [headerAssignments]);

  const baseline = useMemo(() => {
    const map: Record<string, string> = {};
    for (const d of ORG_UNIT_TYPES) {
      const hit = headerAssignments.find((a) => a.dimension === d);
      map[d] = hit?.orgUnitId ?? "";
    }
    return map;
  }, [headerAssignments]);

  const dirty = dimensions.some((d) => (draft[d] ?? "") !== (baseline[d] ?? ""));

  const save = useMutation({
    mutationFn: async () => {
      for (const d of dimensions) {
        const next = draft[d] ?? "";
        const prev = baseline[d] ?? "";
        if (next === prev) continue;
        if (!next) {
          await removeSegmentAssignment(recordType, recordId!, d);
        } else {
          await setSegmentAssignment(recordType, recordId!, { dimension: d, orgUnitId: next });
        }
      }
    },
    onSuccess: () => {
      setErr(null);
      void qc.invalidateQueries({ queryKey: ["segment-assignments", recordType, recordId] });
    },
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  if (!recordId) return null;
  if (assignPending || unitsPending) return <Spinner label="Loading segments…" />;
  if (dimensions.length === 0) return null;

  return (
    <div className="card" style={{ marginTop: 14 }} aria-label={title}>
      <div className="chead">
        <h3>{title}</h3>
        <div className="spacer" />
        {!readOnly && dirty ? (
          <button type="button" className="btn btn-pri btn-sm" disabled={save.isPending} onClick={() => save.mutate()}>
            Save segments
          </button>
        ) : null}
      </div>
      <div className="cbody">
        {err ? (
          <Notice tone="error" icon="x">
            {err}
          </Notice>
        ) : null}
        <div className="grid g2">
          {dimensions.map((d) => {
            const units = unitsByType.get(d) ?? [];
            const current = headerAssignments.find((a) => a.dimension === d);
            const value = draft[d] ?? "";
            const options =
              current && !units.some((u) => u.id === current.orgUnitId)
                ? [
                    ...units,
                    {
                      id: current.orgUnitId,
                      code: current.orgUnitCode,
                      name: `${current.orgUnitName} (inactive / unavailable)`,
                      type: d,
                      isActive: false,
                    } as OrgUnitDto,
                  ]
                : units;
            return (
              <div className="field" key={d} style={{ margin: 0 }}>
                <label>{DIMENSION_LABEL[d] ?? d}</label>
                <select
                  value={value}
                  disabled={readOnly}
                  onChange={(e) => setDraft((prev) => ({ ...prev, [d]: e.target.value }))}
                >
                  <option value="">— None —</option>
                  {options.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.code} — {u.name}
                    </option>
                  ))}
                </select>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

/** Convenience re-export for callers that only need the DTO type. */
export type { SegmentAssignmentDto };
