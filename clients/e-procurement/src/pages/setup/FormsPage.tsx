import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createFormTemplate,
  getFormTemplate,
  listFormTemplates,
  setFormTemplateActive,
  type CreateFormTemplateQuestionDto,
  type FormTemplateListItemDto,
} from "@/api/platform";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { EmptyState, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { FshPermissions } from "@/lib/fsh-permissions";

const QUESTION_TYPES = ["Text", "TextArea", "Select", "Number", "Date", "Checkbox"] as const;

type View = "list" | "create" | "detail";

function errMsg(e: unknown): string {
  if (e instanceof ApiRequestError) return e.problem?.detail ?? e.message;
  if (e instanceof Error) return e.message;
  return "Something went wrong.";
}

type QuestionRow = CreateFormTemplateQuestionDto & { key: number };

function blankQuestion(key: number): QuestionRow {
  return { key, order: key, label: "", type: "Text", required: false };
}

export function FormsPage() {
  const qc = useQueryClient();
  const [view, setView] = useState<View>("list");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const [key, setKey] = useState("");
  const [name, setName] = useState("");
  const [questions, setQuestions] = useState<QuestionRow[]>([blankQuestion(1)]);
  const [nextKey, setNextKey] = useState(2);

  const templatesQuery = useQuery({
    queryKey: ["form-templates", false],
    queryFn: () => listFormTemplates(false),
  });

  const detailQuery = useQuery({
    queryKey: ["form-template", selectedId],
    queryFn: () => getFormTemplate(selectedId!),
    enabled: view === "detail" && !!selectedId,
  });

  const create = useMutation({
    mutationFn: () =>
      createFormTemplate({
        key: key.trim(),
        name: name.trim(),
        questions: questions.map((q, i) => ({
          order: i + 1,
          label: q.label,
          type: q.type,
          required: q.required,
        })),
      }),
    onSuccess: () => {
      setErr(null);
      setView("list");
      setKey("");
      setName("");
      setQuestions([blankQuestion(1)]);
      setNextKey(2);
      void qc.invalidateQueries({ queryKey: ["form-templates"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const toggleActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setFormTemplateActive(id, active),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["form-templates"] });
      void qc.invalidateQueries({ queryKey: ["form-template", selectedId] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const addQuestion = () => {
    setQuestions((prev) => [...prev, blankQuestion(nextKey)]);
    setNextKey((k) => k + 1);
  };

  const removeQuestion = (rowKey: number) => {
    setQuestions((prev) => prev.filter((q) => q.key !== rowKey));
  };

  if (view === "create") {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={() => setView("list")}>
            Forms
          </button>{" "}
          <Icon name="chev" size={12} /> New template
        </div>
        <div className="pagehead">
          <div>
            <h1>New form template</h1>
            <p>Define fields used on onboarding and transaction forms.</p>
          </div>
        </div>

        {err ? <Notice tone="error">{err}</Notice> : null}

        <div className="card">
          <div className="cbody">
            <div className="grid2">
              <div className="field">
                <label>Key</label>
                <input value={key} onChange={(e) => setKey(e.target.value)} />
              </div>
              <div className="field">
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} />
              </div>
            </div>

            <h4 style={{ marginTop: 20, marginBottom: 10 }}>Questions</h4>
            {questions.map((q) => (
              <div key={q.key} className="filterbar" style={{ marginBottom: 10 }}>
                <div className="field" style={{ margin: 0 }}>
                  <label>Label</label>
                  <input
                    value={q.label}
                    onChange={(e) =>
                      setQuestions((prev) =>
                        prev.map((x) => (x.key === q.key ? { ...x, label: e.target.value } : x)),
                      )
                    }
                  />
                </div>
                <div className="field" style={{ margin: 0, minWidth: 140 }}>
                  <label>Type</label>
                  <select
                    value={q.type}
                    onChange={(e) =>
                      setQuestions((prev) =>
                        prev.map((x) => (x.key === q.key ? { ...x, type: e.target.value } : x)),
                      )
                    }
                  >
                    {QUESTION_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </select>
                </div>
                <label style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 20 }}>
                  <input
                    type="checkbox"
                    checked={q.required}
                    onChange={(e) =>
                      setQuestions((prev) =>
                        prev.map((x) =>
                          x.key === q.key ? { ...x, required: e.target.checked } : x,
                        ),
                      )
                    }
                  />
                  Required
                </label>
                {questions.length > 1 ? (
                  <button
                    type="button"
                    className="btn btn-out btn-sm"
                    style={{ alignSelf: "flex-end" }}
                    onClick={() => removeQuestion(q.key)}
                  >
                    Remove
                  </button>
                ) : null}
              </div>
            ))}

            <button type="button" className="btn btn-out btn-sm" onClick={addQuestion}>
              <Icon name="plus" size={14} /> Add question
            </button>

            <div className="actionbar" style={{ marginTop: 16 }}>
              <button type="button" className="btn btn-out" onClick={() => setView("list")}>
                Cancel
              </button>
              <Gated permission={FshPermissions.formTemplates.manage}>
                <button
                  type="button"
                  className="btn btn-pri"
                  disabled={create.isPending}
                  onClick={() => create.mutate()}
                >
                  {create.isPending ? "Creating…" : "Create template"}
                </button>
              </Gated>
            </div>
          </div>
        </div>
      </>
    );
  }

  if (view === "detail" && selectedId) {
    const tpl = detailQuery.data;
    return (
      <>
        <div className="crumb">
          <button
            type="button"
            className="lnk"
            onClick={() => {
              setView("list");
              setSelectedId(null);
            }}
          >
            Forms
          </button>{" "}
          <Icon name="chev" size={12} /> {tpl?.name ?? selectedId}
        </div>
        <div className="pagehead">
          <div>
            <h1>{tpl?.name ?? "Form template"}</h1>
            <p>
              Key: <code>{tpl?.key}</code>
            </p>
          </div>
          <div className="spacer" />
          {tpl ? (
            <Gated permission={FshPermissions.formTemplates.manage}>
              <button
                type="button"
                className={`btn btn-sm ${tpl.isActive ? "btn-out" : "btn-pri"}`}
                disabled={toggleActive.isPending}
                onClick={() => toggleActive.mutate({ id: tpl.id, active: !tpl.isActive })}
              >
                {tpl.isActive ? "Deactivate" : "Activate"}
              </button>
            </Gated>
          ) : null}
        </div>

        {err ? <Notice tone="error">{err}</Notice> : null}

        <div className="card">
          {detailQuery.isPending ? (
            <Spinner label="Loading template…" />
          ) : tpl ? (
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Label</th>
                  <th>Type</th>
                  <th>Required</th>
                </tr>
              </thead>
              <tbody>
                {tpl.questions.map((q) => (
                  <tr key={q.id}>
                    <td>{q.order}</td>
                    <td>{q.label}</td>
                    <td>{q.type}</td>
                    <td>{q.required ? "Yes" : "No"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <EmptyState>Template not found.</EmptyState>
          )}
        </div>
      </>
    );
  }

  const templates = templatesQuery.data ?? [];

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Forms</h1>
          <p>Form template library for onboarding and transactions.</p>
        </div>
        <div className="spacer" />
        <Gated permission={FshPermissions.formTemplates.manage}>
          <button type="button" className="btn btn-pri btn-sm" onClick={() => setView("create")}>
            <Icon name="plus" size={15} /> New template
          </button>
        </Gated>
      </div>

      {err ? <Notice tone="error">{err}</Notice> : null}

      <div className="card">
        {templatesQuery.isPending ? (
          <Spinner label="Loading templates…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Name</th>
                <th>Questions</th>
                <th>Active</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {templates.map((t: FormTemplateListItemDto) => (
                <tr
                  key={t.id}
                  className="drillrow"
                  onClick={() => {
                    setSelectedId(t.id);
                    setView("detail");
                  }}
                >
                  <td style={{ fontWeight: 700 }}>{t.key}</td>
                  <td>{t.name}</td>
                  <td>{t.questionCount}</td>
                  <td>{t.isActive ? "Yes" : "No"}</td>
                  <td className="amt">
                    <span className="btn btn-ghost btn-sm">
                      Open <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              ))}
              {templates.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    <EmptyState>No form templates.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
