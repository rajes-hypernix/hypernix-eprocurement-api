import { useRef, useState, type ReactNode } from "react";
import { Icon } from "@/components/Icon";
import { Modal } from "@/components/ui";
import { downloadExcelTemplate } from "@/lib/excel";

export type ExcelImportRowError = {
  /** 1-based Excel sheet row (header is row 1). */
  line: number;
  message: string;
};

export type ExcelImportProgress = {
  total: number;
  processed: number;
  success: number;
  errors: ExcelImportRowError[];
};

export type ExcelImportRunner = (
  file: File,
  onProgress: (p: ExcelImportProgress) => void,
) => Promise<ExcelImportProgress>;

type Phase = "idle" | "running" | "done";

type Props = {
  title: string;
  /** Short instructions shown above the actions. */
  description?: ReactNode;
  templateFileName: string;
  templateSheetName: string;
  templateHeaders: string[];
  /** Example data row(s) included in the downloaded template. */
  templateSampleRows?: Record<string, string | number | boolean>[];
  /** Column legend, e.g. required vs optional fields. */
  columnsHint?: ReactNode;
  runImport: ExcelImportRunner;
  onClose: () => void;
  /** Called after a finished import that had at least one success. */
  onImported?: (result: ExcelImportProgress) => void;
};

const EMPTY: ExcelImportProgress = { total: 0, processed: 0, success: 0, errors: [] };

export function ExcelImportModal({
  title,
  description,
  templateFileName,
  templateSheetName,
  templateHeaders,
  templateSampleRows = [],
  columnsHint,
  runImport,
  onClose,
  onImported,
}: Props) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [phase, setPhase] = useState<Phase>("idle");
  const [progress, setProgress] = useState<ExcelImportProgress>(EMPTY);
  const [fatal, setFatal] = useState<string | null>(null);

  const pct =
    progress.total > 0 ? Math.min(100, Math.round((progress.processed / progress.total) * 100)) : 0;
  const busy = phase === "running";
  const canClose = !busy;

  const reset = () => {
    setFileName(null);
    setPhase("idle");
    setProgress(EMPTY);
    setFatal(null);
    if (fileRef.current) fileRef.current.value = "";
  };

  const startImport = async (file: File) => {
    setFileName(file.name);
    setFatal(null);
    setPhase("running");
    setProgress({ total: 0, processed: 0, success: 0, errors: [] });
    try {
      const result = await runImport(file, setProgress);
      setProgress(result);
      setPhase("done");
      if (result.success > 0) onImported?.(result);
    } catch (e) {
      setFatal(e instanceof Error ? e.message : "Import failed.");
      setPhase("done");
    }
  };

  return (
    <Modal
      title={title}
      icon="upload"
      footer={
        <>
          {phase === "done" ? (
            <button type="button" className="btn btn-out" onClick={reset} disabled={busy}>
              Import another
            </button>
          ) : null}
          <button
            type="button"
            className="btn btn-out"
            disabled={!canClose}
            onClick={() => {
              if (canClose) onClose();
            }}
          >
            {phase === "done" ? "Close" : "Cancel"}
          </button>
          {phase !== "done" ? (
            <button
              type="button"
              className="btn btn-pri"
              disabled={busy}
              onClick={() => fileRef.current?.click()}
            >
              Choose file…
            </button>
          ) : null}
        </>
      }
    >
      <div className="excel-import">
        {description ? <p className="hint" style={{ marginTop: 0 }}>{description}</p> : null}

        <div className="excel-import-steps">
          <div className="excel-import-step">
                <span className="excel-import-step-num">1</span>
            <div>
              <div style={{ fontWeight: 600, marginBottom: 4 }}>Download template</div>
              <p className="hint" style={{ margin: "0 0 8px" }}>
                Sample file with the correct headers and example rows. Replace the samples with your data.
              </p>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={busy}
                onClick={() =>
                  downloadExcelTemplate(
                    templateFileName,
                    templateSheetName,
                    templateHeaders,
                    templateSampleRows,
                  )
                }
              >
                <Icon name="download" size={14} /> Download sample template
              </button>
            </div>
          </div>

          <div className="excel-import-step">
            <span className="excel-import-step-num">2</span>
            <div>
              <div style={{ fontWeight: 600, marginBottom: 4 }}>Upload filled file</div>
              <p className="hint" style={{ margin: "0 0 8px" }}>
                Accepts .xlsx / .xls. Existing rows are updated (upsert); bad rows are listed below.
              </p>
              <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={busy}
                  onClick={() => fileRef.current?.click()}
                >
                  <Icon name="upload" size={14} /> {fileName ? "Choose another file…" : "Choose Excel file…"}
                </button>
                {fileName ? <span className="hint">{fileName}</span> : null}
              </div>
              <input
                ref={fileRef}
                type="file"
                accept=".xlsx,.xls"
                hidden
                disabled={busy}
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  e.target.value = "";
                  if (f) void startImport(f);
                }}
              />
            </div>
          </div>
        </div>

        {columnsHint ? (
          <div className="excel-import-hint">{columnsHint}</div>
        ) : (
          <div className="excel-import-hint">
            <span className="hint">Columns: </span>
            {templateHeaders.map((h, i) => (
              <span key={h}>
                {i > 0 ? ", " : null}
                <code>{h}</code>
              </span>
            ))}
          </div>
        )}

        {phase !== "idle" ? (
          <div className="excel-import-progress">
            <div style={{ display: "flex", justifyContent: "space-between", gap: 12, marginBottom: 6 }}>
              <span style={{ fontWeight: 600, fontSize: 13 }}>
                {busy ? "Importing…" : "Import finished"}
              </span>
              <span className="hint">
                {progress.processed} / {progress.total || "—"} rows · {pct}%
              </span>
            </div>
            <div className="pbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct} role="progressbar">
              <i style={{ width: `${Math.max(pct, busy && pct === 0 ? 4 : pct)}%` }} />
            </div>
            <div className="excel-import-stats">
              <span className="badge b-green">{progress.success} success</span>
              <span className={`badge ${progress.errors.length ? "b-red" : "b-grey"}`}>
                {progress.errors.length} error{progress.errors.length === 1 ? "" : "s"}
              </span>
            </div>
          </div>
        ) : null}

        {fatal ? (
          <p className="hint" style={{ color: "var(--red-ink, #9b2c2c)", marginTop: 12 }}>
            {fatal}
          </p>
        ) : null}

        {progress.errors.length > 0 ? (
          <div className="excel-import-errors">
            <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 6 }}>
              Errors by line
            </div>
            <ul>
              {progress.errors.map((e, idx) => (
                <li key={`${e.line}-${idx}`}>
                  <strong>Line {e.line}</strong>
                  <span className="hint"> — {e.message}</span>
                </li>
              ))}
            </ul>
          </div>
        ) : null}

        {phase === "done" && !fatal && progress.total === 0 ? (
          <p className="hint" style={{ marginTop: 12 }}>
            No data rows found in the file. Download the template and add at least one row under the header.
          </p>
        ) : null}
      </div>
    </Modal>
  );
}
