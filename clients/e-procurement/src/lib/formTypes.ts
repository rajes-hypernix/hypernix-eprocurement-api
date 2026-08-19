import type { FormItemDto } from "@/api/sourcing";

/** Mirrors POC QTYPES — the 12 question field types expected by FormItem. */
export const QTYPES: [string, string][] = [
  ["short_text", "Short text"],
  ["long_text", "Long text"],
  ["number", "Number"],
  ["money", "Money"],
  ["percent", "Percentage"],
  ["list", "List (single)"],
  ["multi", "Multi-select"],
  ["yesno", "Yes / No"],
  ["date", "Date"],
  ["attachment", "Attachment"],
  ["table", "Table / matrix"],
  ["group", "Repeatable group"],
];

export const typeLabel = (t: string): string => QTYPES.find((x) => x[0] === t)?.[1] ?? t;

export type FormItemConfig = {
  options?: string[];
  unit?: string;
  columns?: string[];
  rows?: string[];
  cellType?: string;
  fields?: { label: string; type: string }[];
  max?: number;
  filetypes?: string;
  multiple?: boolean;
  /** Library round-trip meta (stripped before save to RFQ). */
  __kind?: string;
  __group?: string;
  __section?: string;
};

export function parseConfig(json: string | null | undefined): FormItemConfig {
  if (!json) return {};
  try {
    return JSON.parse(json) as FormItemConfig;
  } catch {
    return {};
  }
}

/** Editor-friendly item with parsed config + stable client id. */
export type EditItem = {
  id: string;
  kind: string;
  group: string;
  section: string;
  label: string;
  type: string;
  required: boolean;
  config: FormItemConfig;
  help: string;
};

let _idSeq = 0;
export const newItemId = () => `q${++_idSeq}`;

const LEGACY_TYPE: Record<string, string> = {
  text: "short_text",
  longtext: "long_text",
  select: "list",
  file: "attachment",
};

export function normalizeType(t: string | null | undefined): string {
  const raw = (t ?? "short_text").trim();
  return LEGACY_TYPE[raw] ?? (raw || "short_text");
}

export function normalizeGroup(g: string | null | undefined): GroupKey {
  const v = (g ?? "Technical").trim().toLowerCase();
  return v === "commercial" ? "Commercial" : "Technical";
}

export const fromDto = (d: FormItemDto): EditItem => {
  const raw = parseConfig(d.configJson);
  const { __kind, __group, __section, ...config } = raw;
  return {
    id: newItemId(),
    kind: __kind || d.kind || "question",
    group: normalizeGroup(__group || d.group),
    section: (__section ?? d.section) || "",
    label: d.label ?? "",
    type: normalizeType(d.type),
    required: d.required ?? false,
    config,
    help: d.help ?? "",
  };
};

export const defaultConfig = (type: string): FormItemConfig => {
  if (type === "list" || type === "multi") return { options: ["Option 1"] };
  if (type === "table") {
    return {
      columns: ["FY2023", "FY2024", "FY2025"],
      rows: ["Revenue", "Net profit", "Total assets"],
      cellType: "money",
    };
  }
  if (type === "group") {
    return {
      fields: [
        { label: "Project name", type: "short_text" },
        { label: "Contract value", type: "money" },
        { label: "Year", type: "number" },
      ],
      max: 3,
    };
  }
  return {};
};

export type FormSections = { Technical: string[]; Commercial: string[] };
export type GroupKey = "Technical" | "Commercial";

export const sectionsOf = (g: GroupKey, sections: FormSections, items: EditItem[]): string[] => {
  const secs = [...(sections[g] ?? [])];
  for (const it of items) {
    if (it.group === g && it.section && !secs.includes(it.section)) secs.push(it.section);
  }
  return secs;
};

export const toDto = (e: EditItem, order: number): FormItemDto => ({
  kind: e.kind,
  group: e.group,
  section: e.section,
  label: e.label,
  type: e.type,
  required: e.required,
  configJson: JSON.stringify(e.config ?? {}),
  help: e.help || null,
  order,
});

/** Pack group/section/kind into configJson for FormTemplate library (no extra DB columns). */
export const toLibraryQuestion = (e: EditItem, order: number) => ({
  order,
  label: e.label || (e.kind === "terms" ? "Terms & conditions" : e.kind === "instruction" ? "Instruction" : "Untitled"),
  type: e.kind === "question" ? e.type || "short_text" : e.kind,
  required: e.required,
  configJson: JSON.stringify({
    ...e.config,
    __kind: e.kind,
    __group: e.group,
    __section: e.section,
  }),
  help: e.help || null,
});

export function fromLibraryQuestion(q: {
  label: string;
  type: string;
  required: boolean;
  configJson?: string | null;
  help?: string | null;
}): EditItem {
  const raw = parseConfig(q.configJson);
  const { __kind, __group, __section, ...config } = raw;
  const kind = __kind || (q.type === "instruction" || q.type === "terms" ? q.type : "question");
  return {
    id: newItemId(),
    kind,
    group: normalizeGroup(__group),
    section: __section ?? "",
    label: q.label ?? "",
    type: kind === "question" ? normalizeType(q.type) : "",
    required: q.required ?? false,
    config,
    help: q.help ?? "",
  };
}
