import * as XLSX from "xlsx";

export type ExcelColumn<T> = {
  header: string;
  key: string;
  value: (row: T) => string | number | boolean | null | undefined;
};

/** Download rows as .xlsx (SheetJS). */
export function exportRowsToExcel<T>(
  fileName: string,
  sheetName: string,
  columns: ExcelColumn<T>[],
  rows: T[],
): void {
  const data = rows.map((row) => {
    const obj: Record<string, string | number | boolean> = {};
    for (const col of columns) {
      const v = col.value(row);
      obj[col.header] = v == null ? "" : (v as string | number | boolean);
    }
    return obj;
  });
  const ws = XLSX.utils.json_to_sheet(data);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, sheetName.slice(0, 31));
  XLSX.writeFile(wb, fileName.endsWith(".xlsx") ? fileName : `${fileName}.xlsx`);
}

/**
 * Download an import template (header row + optional sample rows).
 * Pass empty `sampleRows` for headers only; prefer one sample so users see the format.
 */
export function downloadExcelTemplate(
  fileName: string,
  sheetName: string,
  headers: string[],
  sampleRows: Record<string, string | number | boolean>[] = [],
): void {
  const data =
    sampleRows.length > 0
      ? sampleRows.map((row) => {
          const obj: Record<string, string | number | boolean> = {};
          for (const h of headers) obj[h] = row[h] ?? "";
          return obj;
        })
      : [
          Object.fromEntries(headers.map((h) => [h, ""])) as Record<string, string>,
        ];
  const ws = XLSX.utils.json_to_sheet(data);
  // Keep header-only templates as a single header row (drop the blank data row).
  if (sampleRows.length === 0) {
    const range = XLSX.utils.decode_range(ws["!ref"] ?? "A1");
    range.e.r = 0;
    ws["!ref"] = XLSX.utils.encode_range(range);
  }
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, sheetName.slice(0, 31));
  XLSX.writeFile(wb, fileName.endsWith(".xlsx") ? fileName : `${fileName}.xlsx`);
}

/** Parse first sheet of an .xlsx/.xls file into row objects keyed by header. */
export async function parseExcelFile(file: File): Promise<Record<string, unknown>[]> {
  const buf = await file.arrayBuffer();
  const wb = XLSX.read(buf, { type: "array" });
  const name = wb.SheetNames[0];
  if (!name) return [];
  const sheet = wb.Sheets[name];
  return XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: "" });
}

export function cellStr(row: Record<string, unknown>, ...headers: string[]): string {
  for (const h of headers) {
    const direct = row[h];
    if (direct != null && String(direct).trim() !== "") return String(direct).trim();
    const found = Object.keys(row).find((k) => k.trim().toLowerCase() === h.trim().toLowerCase());
    if (found != null && String(row[found]).trim() !== "") return String(row[found]).trim();
  }
  return "";
}

/**
 * Parse Yes/No (or true/false/1/0) Active columns.
 * Returns `null` when the cell is blank (caller keeps existing / default).
 */
export function cellActive(row: Record<string, unknown>, ...headers: string[]): boolean | null {
  const raw = cellStr(row, ...(headers.length ? headers : ["Active", "IsActive", "Status"]));
  if (!raw) return null;
  const v = raw.toLowerCase();
  if (["yes", "y", "true", "1", "active"].includes(v)) return true;
  if (["no", "n", "false", "0", "inactive"].includes(v)) return false;
  throw new Error(`Active must be Yes or No (got "${raw}").`);
}

export function activeLabel(isActive: boolean): string {
  return isActive ? "Yes" : "No";
}

