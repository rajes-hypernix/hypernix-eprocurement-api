import { useMemo, useState, type ReactNode } from "react";
import { EmptyState, Spinner } from "@/components/ui";

export type SortDir = "asc" | "desc";

export type DataTableColumn<T> = {
  id: string;
  header: string;
  /** Enable click-to-sort. Provide sortValue for non-trivial cells. */
  sortable?: boolean;
  sortValue?: (row: T) => string | number | boolean | null | undefined;
  align?: "left" | "right";
  /** Clicks in this cell do not fire onRowClick (for action buttons). */
  interactive?: boolean;
  render: (row: T) => ReactNode;
  className?: string;
};

export type DataTableProps<T> = {
  rows: T[];
  columns: DataTableColumn<T>[];
  rowKey: (row: T) => string;
  loading?: boolean;
  loadingLabel?: string;
  empty?: ReactNode;
  /** Initial sort — defaults to first sortable column desc when set. */
  initialSort?: { id: string; dir: SortDir };
  onRowClick?: (row: T) => void;
  rowClassName?: (row: T) => string | undefined;
  footer?: ReactNode;
};

function compareValues(a: string | number | boolean | null | undefined, b: string | number | boolean | null | undefined): number {
  if (a == null && b == null) return 0;
  if (a == null) return -1;
  if (b == null) return 1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  if (typeof a === "boolean" && typeof b === "boolean") return Number(a) - Number(b);
  return String(a).localeCompare(String(b), undefined, { sensitivity: "base", numeric: true });
}

/**
 * Reusable sorted data table for any module (masters, sourcing lists, etc.).
 * Visual style follows layered surfaces (white body, muted header).
 */
export function DataTable<T>({
  rows,
  columns,
  rowKey,
  loading,
  loadingLabel = "Loading…",
  empty = "No rows.",
  initialSort,
  onRowClick,
  rowClassName,
  footer,
}: DataTableProps<T>) {
  const [sortId, setSortId] = useState<string | null>(initialSort?.id ?? null);
  const [sortDir, setSortDir] = useState<SortDir>(initialSort?.dir ?? "desc");

  const sorted = useMemo(() => {
    if (!sortId) return rows;
    const col = columns.find((c) => c.id === sortId);
    if (!col?.sortable) return rows;
    const getter = col.sortValue ?? ((row: T) => String(col.render(row) ?? ""));
    const copy = [...rows];
    copy.sort((ra, rb) => {
      const cmp = compareValues(getter(ra), getter(rb));
      return sortDir === "asc" ? cmp : -cmp;
    });
    return copy;
  }, [rows, columns, sortId, sortDir]);

  const toggleSort = (col: DataTableColumn<T>) => {
    if (!col.sortable) return;
    if (sortId !== col.id) {
      setSortId(col.id);
      setSortDir("asc");
      return;
    }
    setSortDir((d) => (d === "asc" ? "desc" : "asc"));
  };

  if (loading) return <Spinner label={loadingLabel} />;

  if (rows.length === 0) {
    return typeof empty === "string" ? <EmptyState>{empty}</EmptyState> : <>{empty}</>;
  }

  return (
    <>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              {columns.map((col) => {
                const active = sortId === col.id;
                const cls = [
                  col.align === "right" ? "amt" : "",
                  col.sortable ? "dt-sortable" : "",
                  active && sortDir === "asc" ? "dt-asc" : "",
                  active && sortDir === "desc" ? "dt-desc" : "",
                  col.className ?? "",
                ]
                  .filter(Boolean)
                  .join(" ");
                return (
                  <th
                    key={col.id}
                    className={cls || undefined}
                    onClick={col.sortable ? () => toggleSort(col) : undefined}
                    aria-sort={
                      col.sortable && active ? (sortDir === "asc" ? "ascending" : "descending") : undefined
                    }
                  >
                    {col.header}
                    {col.sortable ? (
                      <span className="dt-sort" aria-hidden>
                        {active ? (sortDir === "asc" ? "▲" : "▼") : "↕"}
                      </span>
                    ) : null}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {sorted.map((row) => {
              const extra = rowClassName?.(row);
              return (
                <tr
                  key={rowKey(row)}
                  className={[onRowClick ? "drillrow" : "", extra ?? ""].filter(Boolean).join(" ") || undefined}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                >
                  {columns.map((col) => (
                    <td
                      key={col.id}
                      className={[col.align === "right" ? "amt" : "", col.className ?? ""].filter(Boolean).join(" ") || undefined}
                      onClick={
                        col.interactive || (col.align === "right" && onRowClick)
                          ? (e) => e.stopPropagation()
                          : undefined
                      }
                    >
                      {col.render(row)}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {footer}
    </>
  );
}
