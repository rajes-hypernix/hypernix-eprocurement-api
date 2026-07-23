import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteRole,
  getPermissionsCatalog,
  getRoleWithPermissions,
  listRoles,
  updateRolePermissions,
  upsertRole,
  type PermissionCatalogEntryDto,
} from "@/api/identity";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { EmptyState, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { FshPermissions } from "@/lib/fsh-permissions";

function errMsg(e: unknown): string {
  if (e instanceof ApiRequestError) return e.problem?.detail ?? e.message;
  if (e instanceof Error) return e.message;
  return "Something went wrong.";
}

const SYSTEM_ROLES = new Set(["Admin", "Basic"]);

export function RolesListPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { data = [], isPending } = useQuery({
    queryKey: ["roles"],
    queryFn: listRoles,
  });

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Roles</h1>
          <p>Role definitions and permission grants.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-out btn-sm" onClick={() => onNavigate("")}>
          <Icon name="users" size={15} /> Users
        </button>
        <Gated permission={FshPermissions.roles.create}>
          <button type="button" className="btn btn-pri btn-sm" onClick={() => onNavigate("roles/new")}>
            <Icon name="plus" size={15} /> New role
          </button>
        </Gated>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading roles…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Role</th>
                <th>Description</th>
                <th>Grants</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id} className="drillrow" onClick={() => onNavigate(`roles/${r.id}`)}>
                  <td style={{ fontWeight: 700 }}>{r.name}</td>
                  <td>{r.description ?? "—"}</td>
                  <td>{r.permissions?.length ?? 0}</td>
                  <td className="amt">
                    <span className="btn btn-ghost btn-sm">
                      Open <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              ))}
              {data.length === 0 ? (
                <tr>
                  <td colSpan={4}>
                    <EmptyState>No roles defined.</EmptyState>
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

export function RoleCreatePage({
  onBack,
  onSaved,
}: {
  onBack: () => void;
  onSaved: (roleId: string) => void;
}) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [err, setErr] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => upsertRole({ id: "", name: name.trim(), description: description.trim() || undefined }),
    onSuccess: (role) => onSaved(role.id),
    onError: (e) => setErr(errMsg(e)),
  });

  const submit = () => {
    setErr(null);
    if (!name.trim()) {
      setErr("Role name is required.");
      return;
    }
    save.mutate();
  };

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Roles
        </button>{" "}
        <Icon name="chev" size={12} /> New role
      </div>
      <div className="pagehead">
        <div>
          <h1>New role</h1>
          <p>Create a custom role, then assign permissions on the detail screen.</p>
        </div>
      </div>

      {err ? <Notice tone="error">{err}</Notice> : null}

      <div className="card">
        <div className="cbody">
          <div className="field">
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="field">
            <label>Description</label>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
          </div>
          <div className="actionbar" style={{ marginTop: 16 }}>
            <button type="button" className="btn btn-out" onClick={onBack}>
              Cancel
            </button>
            <button type="button" className="btn btn-pri" disabled={save.isPending} onClick={submit}>
              {save.isPending ? "Creating…" : "Create role"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

function groupCatalog(entries: PermissionCatalogEntryDto[]): Map<string, PermissionCatalogEntryDto[]> {
  const map = new Map<string, PermissionCatalogEntryDto[]>();
  for (const e of entries) {
    const list = map.get(e.resource) ?? [];
    list.push(e);
    map.set(e.resource, list);
  }
  return map;
}

export function RoleDetailPage({ roleId, onBack }: { roleId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const roleQuery = useQuery({
    queryKey: ["roles", roleId],
    queryFn: () => getRoleWithPermissions(roleId),
  });

  const catalogQuery = useQuery({
    queryKey: ["permissions-catalog"],
    queryFn: getPermissionsCatalog,
  });

  const role = roleQuery.data;
  const isSystem = role ? SYSTEM_ROLES.has(role.name) : false;

  useEffect(() => {
    if (role?.permissions) setSelected(new Set(role.permissions));
  }, [role?.permissions]);

  const grouped = useMemo(
    () => groupCatalog(catalogQuery.data ?? []),
    [catalogQuery.data],
  );

  const saveProfile = useMutation({
    mutationFn: (input: { name: string; description: string }) =>
      upsertRole({ id: roleId, name: input.name, description: input.description }),
    onSuccess: () => {
      setErr(null);
      void qc.invalidateQueries({ queryKey: ["roles", roleId] });
      void qc.invalidateQueries({ queryKey: ["roles"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const savePerms = useMutation({
    mutationFn: () => updateRolePermissions(roleId, [...selected]),
    onSuccess: () => {
      setErr(null);
      setSaved(true);
      void qc.invalidateQueries({ queryKey: ["roles", roleId] });
      setTimeout(() => setSaved(false), 2000);
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const remove = useMutation({
    mutationFn: () => deleteRole(roleId),
    onSuccess: () => onBack(),
    onError: (e) => setErr(errMsg(e)),
  });

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  useEffect(() => {
    if (role) {
      setName(role.name);
      setDescription(role.description ?? "");
    }
  }, [role]);

  const togglePerm = (perm: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(perm)) next.delete(perm);
      else next.add(perm);
      return next;
    });
  };

  if (roleQuery.isPending) return <Spinner label="Loading role…" />;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Roles
        </button>{" "}
        <Icon name="chev" size={12} /> {role?.name ?? roleId}
      </div>
      <div className="pagehead">
        <div>
          <h1>{role?.name}</h1>
          <p>{role?.description ?? "Permission grants for this role."}</p>
        </div>
        <div className="spacer" />
        <Gated permission={FshPermissions.roles.delete}>
          {!isSystem ? (
            <button
              type="button"
              className="btn btn-out btn-sm btn-danger"
              disabled={remove.isPending}
              onClick={() => remove.mutate()}
            >
              Delete
            </button>
          ) : null}
        </Gated>
      </div>

      {isSystem ? (
        <Notice tone="warn">Built-in role — name and permissions are read-only.</Notice>
      ) : null}
      {err ? <Notice tone="error">{err}</Notice> : null}
      {saved ? <Notice tone="success">Permissions saved.</Notice> : null}

      {!isSystem ? (
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="chead">
            <h3>Profile</h3>
          </div>
          <div className="cbody">
            <div className="field">
              <label>Name</label>
              <input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="field">
              <label>Description</label>
              <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} />
            </div>
            <Gated permission={FshPermissions.roles.update}>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={saveProfile.isPending}
                onClick={() => saveProfile.mutate({ name, description })}
              >
                Save profile
              </button>
            </Gated>
          </div>
        </div>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Permissions ({selected.size})</h3>
        </div>
        <div className="cbody">
          {catalogQuery.isPending ? (
            <Spinner label="Loading catalog…" />
          ) : (
            <>
              {[...grouped.entries()].map(([resource, entries]) => (
                <div key={resource} style={{ marginBottom: 20 }}>
                  <h4 style={{ marginBottom: 8 }}>{resource}</h4>
                  {entries.map((entry) => (
                    <label key={entry.name} style={{ display: "flex", gap: 10, marginBottom: 6 }}>
                      <input
                        type="checkbox"
                        checked={selected.has(entry.name)}
                        disabled={isSystem}
                        onChange={() => togglePerm(entry.name)}
                      />
                      <span>
                        <strong>{entry.action}</strong>
                        <span className="hint"> — {entry.description}</span>
                      </span>
                    </label>
                  ))}
                </div>
              ))}
              {!isSystem ? (
                <Gated permission={FshPermissions.roles.update}>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    disabled={savePerms.isPending}
                    onClick={() => savePerms.mutate()}
                  >
                    {savePerms.isPending ? "Saving…" : "Save permissions"}
                  </button>
                </Gated>
              ) : null}
            </>
          )}
        </div>
      </div>
    </>
  );
}
