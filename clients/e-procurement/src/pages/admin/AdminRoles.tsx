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
import { ConfirmModal, EmptyState, Notice, Spinner } from "@/components/ui";
import { useAuth } from "@/auth/use-auth";
import { useErrorDialog } from "@/feedback/ErrorDialogContext";
import { dateTimeMY } from "@/lib/format";
import { FshPermissions } from "@/lib/fsh-permissions";

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
          <p>Define roles and assign which permissions each role has.</p>
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
          <>
            <table>
              <thead>
                <tr>
                  <th>Role</th>
                  <th>Description</th>
                  <th>Created</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {data.map((r) => (
                  <tr key={r.id} className="drillrow" onClick={() => onNavigate(`roles/${r.id}`)}>
                    <td style={{ fontWeight: 700 }}>{r.name}</td>
                    <td>{r.description ?? "—"}</td>
                    <td>
                      <span className="hint">{r.createdOnUtc ? dateTimeMY(r.createdOnUtc) : "—"}</span>
                    </td>
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
                      <EmptyState icon="users">No roles defined.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
            <p className="hint" style={{ margin: "10px 16px 12px", textAlign: "right" }}>
              {data.length} total
            </p>
          </>
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
  const { showError, showErrorFrom } = useErrorDialog();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const save = useMutation({
    mutationFn: () =>
      upsertRole({ id: "", name: name.trim(), description: description.trim() || undefined }),
    onSuccess: (role) => onSaved(role.id),
    onError: (e) => showErrorFrom(e, "Could not create role"),
  });

  const submit = () => {
    if (!name.trim()) {
      showError("Role name is required.");
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

type TriState = "all" | "partial" | "none";

type ModuleNode = {
  resource: string;
  label: string;
  perms: PermissionCatalogEntryDto[];
};

type AreaGroup = {
  area: string;
  modules: ModuleNode[];
};

/** Flat Identity resources (no `Area.` prefix) → Identity group. */
const IDENTITY_RESOURCES = new Set([
  "Users",
  "UserRoles",
  "Roles",
  "RoleClaims",
  "Sessions",
  "Groups",
  "Impersonation",
]);

const AREA_ORDER = [
  "Identity",
  "Platform",
  "Suppliers",
  "Sourcing",
  "Procurement",
  "Catalog",
  "Notifications",
  "Files",
  "Tickets",
  "Billing",
  "Webhooks",
  "Auditing",
  "Multitenancy",
];

function humanizeLabel(value: string): string {
  const spaced = value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[._-]+/g, " ")
    .trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function humanizeAction(action: string): string {
  return humanizeLabel(action);
}

function resolveArea(resource: string): { area: string; label: string } {
  if (IDENTITY_RESOURCES.has(resource)) return { area: "Identity", label: resource };
  if (resource === "Tenants") return { area: "Multitenancy", label: "Tenants" };
  if (resource === "AuditTrails") return { area: "Auditing", label: "AuditTrails" };
  const dot = resource.indexOf(".");
  if (dot > 0) {
    return { area: resource.slice(0, dot), label: resource.slice(dot + 1) };
  }
  return { area: resource, label: resource };
}

function areaSortIndex(area: string): number {
  const i = AREA_ORDER.indexOf(area);
  return i >= 0 ? i : AREA_ORDER.length;
}

function buildAreas(entries: PermissionCatalogEntryDto[]): AreaGroup[] {
  const byResource = new Map<string, PermissionCatalogEntryDto[]>();
  for (const e of entries) {
    const list = byResource.get(e.resource) ?? [];
    list.push(e);
    byResource.set(e.resource, list);
  }

  const byArea = new Map<string, ModuleNode[]>();
  for (const [resource, perms] of byResource) {
    const { area, label } = resolveArea(resource);
    const list = byArea.get(area) ?? [];
    list.push({
      resource,
      label,
      perms: [...perms].sort((a, b) => a.action.localeCompare(b.action)),
    });
    byArea.set(area, list);
  }

  return [...byArea.entries()]
    .sort(([a], [b]) => {
      const d = areaSortIndex(a) - areaSortIndex(b);
      return d !== 0 ? d : a.localeCompare(b);
    })
    .map(([area, modules]) => ({
      area,
      modules: modules.sort((a, b) => a.label.localeCompare(b.label)),
    }));
}

function moduleTriState(module: ModuleNode, selected: Set<string>): TriState {
  const onCount = module.perms.filter((p) => selected.has(p.name)).length;
  if (onCount === 0) return "none";
  if (onCount === module.perms.length) return "all";
  return "partial";
}

function areaTriState(group: AreaGroup, selected: Set<string>): TriState {
  let on = 0;
  let total = 0;
  for (const m of group.modules) {
    total += m.perms.length;
    on += m.perms.filter((p) => selected.has(p.name)).length;
  }
  if (on === 0) return "none";
  if (on === total) return "all";
  return "partial";
}

function areaPermCount(group: AreaGroup, selected: Set<string>): { on: number; total: number } {
  let on = 0;
  let total = 0;
  for (const m of group.modules) {
    total += m.perms.length;
    on += m.perms.filter((p) => selected.has(p.name)).length;
  }
  return { on, total };
}

function TriCheckbox({
  state,
  editable,
  onClick,
  size = 16,
}: {
  state: TriState;
  editable: boolean;
  onClick?: () => void;
  size?: number;
}) {
  return (
    <button
      type="button"
      className={`role-tri role-tri--${state}${editable ? " editable" : ""}`}
      style={{ width: size, height: size }}
      onClick={(e) => {
        e.stopPropagation();
        if (editable) onClick?.();
      }}
      disabled={!editable}
      aria-checked={state === "all" ? "true" : state === "partial" ? "mixed" : "false"}
      role="checkbox"
    >
      {state === "all" ? <Icon name="check" size={Math.round(size * 0.7)} /> : null}
      {state === "partial" ? <Icon name="minus" size={Math.round(size * 0.7)} /> : null}
    </button>
  );
}

export function RoleDetailPage({ roleId, onBack }: { roleId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { user: authUser } = useAuth();
  const { showError, showErrorFrom } = useErrorDialog();
  const [pendingDelete, setPendingDelete] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [query, setQuery] = useState("");
  const [expandedAreas, setExpandedAreas] = useState<Set<string>>(() => new Set());
  const [expandedModules, setExpandedModules] = useState<Set<string>>(() => new Set());
  const [expandedSeeded, setExpandedSeeded] = useState(false);

  const canUpdateRoles = (authUser?.permissions ?? []).includes(FshPermissions.roles.update);

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
  const editable = !isSystem && canUpdateRoles;

  useEffect(() => {
    if (role?.permissions) setSelected(new Set(role.permissions));
  }, [role?.permissions]);

  const areas = useMemo(() => buildAreas(catalogQuery.data ?? []), [catalogQuery.data]);

  // Open areas/modules that already have grants once catalog + selection are ready.
  useEffect(() => {
    if (expandedSeeded || areas.length === 0 || !role?.permissions) return;
    const openAreas = new Set<string>();
    const openModules = new Set<string>();
    for (const g of areas) {
      let areaHasGrant = false;
      for (const m of g.modules) {
        if (m.perms.some((p) => selected.has(p.name))) {
          openModules.add(m.resource);
          areaHasGrant = true;
        }
      }
      if (areaHasGrant) openAreas.add(g.area);
    }
    setExpandedAreas(openAreas);
    setExpandedModules(openModules);
    setExpandedSeeded(true);
  }, [areas, role?.permissions, selected, expandedSeeded]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return areas;
    return areas
      .map((g) => {
        if (g.area.toLowerCase().includes(q)) return g;
        const modules = g.modules
          .map((m) => {
            if (
              m.resource.toLowerCase().includes(q) ||
              m.label.toLowerCase().includes(q) ||
              humanizeLabel(m.label).toLowerCase().includes(q)
            ) {
              return m;
            }
            const perms = m.perms.filter(
              (p) =>
                p.action.toLowerCase().includes(q) ||
                p.name.toLowerCase().includes(q) ||
                humanizeAction(p.action).toLowerCase().includes(q) ||
                p.description.toLowerCase().includes(q),
            );
            return perms.length ? { ...m, perms } : null;
          })
          .filter((m): m is ModuleNode => m != null);
        return modules.length ? { ...g, modules } : null;
      })
      .filter((g): g is AreaGroup => g != null);
  }, [areas, query]);

  const totals = useMemo(() => {
    let granted = 0;
    let total = 0;
    for (const g of areas) {
      for (const m of g.modules) {
        for (const p of m.perms) {
          total += 1;
          if (selected.has(p.name)) granted += 1;
        }
      }
    }
    return { granted, total };
  }, [areas, selected]);

  const saveProfile = useMutation({
    mutationFn: (input: { name: string; description: string }) =>
      upsertRole({ id: roleId, name: input.name, description: input.description }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["roles", roleId] });
      void qc.invalidateQueries({ queryKey: ["roles"] });
    },
    onError: (e) => showErrorFrom(e, "Could not save role profile"),
  });

  const savePerms = useMutation({
    mutationFn: () => updateRolePermissions(roleId, [...selected]),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["roles", roleId] });
    },
    onError: (e) => showErrorFrom(e, "Could not save permissions"),
  });

  const remove = useMutation({
    mutationFn: () => deleteRole(roleId),
    onSuccess: () => onBack(),
    onError: (e) => showErrorFrom(e, "Could not delete role"),
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
    if (!editable) return;
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(perm)) next.delete(perm);
      else next.add(perm);
      return next;
    });
  };

  const toggleModule = (module: ModuleNode) => {
    if (!editable) return;
    const state = moduleTriState(module, selected);
    const nextOn = state !== "all";
    setSelected((prev) => {
      const next = new Set(prev);
      for (const p of module.perms) {
        if (nextOn) next.add(p.name);
        else next.delete(p.name);
      }
      return next;
    });
  };

  const toggleArea = (group: AreaGroup) => {
    if (!editable) return;
    const state = areaTriState(group, selected);
    const nextOn = state !== "all";
    setSelected((prev) => {
      const next = new Set(prev);
      for (const m of group.modules) {
        for (const p of m.perms) {
          if (nextOn) next.add(p.name);
          else next.delete(p.name);
        }
      }
      return next;
    });
  };

  const toggleExpandedArea = (area: string) => {
    setExpandedAreas((prev) => {
      const next = new Set(prev);
      if (next.has(area)) next.delete(area);
      else next.add(area);
      return next;
    });
  };

  const toggleExpandedModule = (resource: string) => {
    setExpandedModules((prev) => {
      const next = new Set(prev);
      if (next.has(resource)) next.delete(resource);
      else next.add(resource);
      return next;
    });
  };

  const expandAll = () => {
    setExpandedAreas(new Set(filtered.map((g) => g.area)));
    setExpandedModules(new Set(filtered.flatMap((g) => g.modules.map((m) => m.resource))));
  };
  const collapseAll = () => {
    setExpandedAreas(new Set());
    setExpandedModules(new Set());
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
          <p>{role?.description ?? "Choose which permissions this role can use."}</p>
        </div>
        <div className="spacer" />
        <Gated permission={FshPermissions.roles.delete}>
          {!isSystem ? (
            <button
              type="button"
              className="btn btn-out btn-sm btn-danger"
              disabled={remove.isPending}
              onClick={() => setPendingDelete(true)}
            >
              Delete
            </button>
          ) : null}
        </Gated>
      </div>

      {isSystem ? (
        <Notice tone="warn">Built-in role — name and permissions are read-only.</Notice>
      ) : !canUpdateRoles ? (
        <Notice tone="warn">You can view this role, but you do not have permission to change it.</Notice>
      ) : (
        <Notice tone="success">
          Editable — click an area or module checkbox to toggle everything under it, or click individual
          permissions.
        </Notice>
      )}

      {!isSystem ? (
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="chead">
            <h3>Profile</h3>
          </div>
          <div className="cbody">
            <div className="field">
              <label>Name</label>
              <input value={name} onChange={(e) => setName(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Description</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={2}
                disabled={!editable}
              />
            </div>
            {editable ? (
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={saveProfile.isPending}
                onClick={() => {
                  if (!name.trim()) {
                    showError("Role name is required.");
                    return;
                  }
                  saveProfile.mutate({ name: name.trim(), description });
                }}
              >
                {saveProfile.isPending ? "Saving…" : "Save profile"}
              </button>
            ) : null}
          </div>
        </div>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Permissions</h3>
          <span className="hint" style={{ marginLeft: "auto" }}>
            {totals.granted}/{totals.total} granted
          </span>
        </div>
        <div className="cbody">
          {catalogQuery.isPending ? (
            <Spinner label="Loading catalog…" />
          ) : (
            <>
              <div className="role-tree-controls">
                <div className="field" style={{ margin: 0, flex: 1 }}>
                  <label>Filter</label>
                  <input
                    value={query}
                    placeholder="Filter areas, modules, or permissions…"
                    onChange={(e) => setQuery(e.target.value)}
                  />
                </div>
                <button type="button" className="lnk role-tree-link" onClick={expandAll}>
                  Expand all
                </button>
                <button type="button" className="lnk role-tree-link" onClick={collapseAll}>
                  Collapse all
                </button>
              </div>

              {filtered.length === 0 ? (
                <EmptyState icon="list">No permissions match the filter.</EmptyState>
              ) : (
                <div className="role-tree">
                  {filtered.map((g) => {
                    const areaState = areaTriState(g, selected);
                    const areaOpen = expandedAreas.has(g.area);
                    const areaCount = areaPermCount(g, selected);
                    return (
                      <div key={g.area} className="role-tree-area">
                        <div
                          className="role-tree-row role-tree-area-row"
                          role="button"
                          tabIndex={0}
                          onClick={() => toggleExpandedArea(g.area)}
                          onKeyDown={(e) => {
                            if (e.key === "Enter" || e.key === " ") {
                              e.preventDefault();
                              toggleExpandedArea(g.area);
                            }
                          }}
                        >
                          <span className={`role-tree-caret${areaOpen ? " open" : ""}`}>
                            <Icon name="chev" size={15} />
                          </span>
                          <TriCheckbox
                            state={areaState}
                            editable={editable}
                            onClick={() => toggleArea(g)}
                          />
                          <span className="role-tree-name">{humanizeLabel(g.area)}</span>
                          <span className="role-tree-badge">
                            {areaCount.on}/{areaCount.total}
                          </span>
                        </div>
                        {areaOpen ? (
                          <div className="role-tree-area-body">
                            {g.modules.map((m) => {
                              const state = moduleTriState(m, selected);
                              const isOpen = expandedModules.has(m.resource);
                              const onCount = m.perms.filter((p) => selected.has(p.name)).length;
                              return (
                                <div key={m.resource} className="role-tree-node">
                                  <div
                                    className="role-tree-row"
                                    role="button"
                                    tabIndex={0}
                                    onClick={() => toggleExpandedModule(m.resource)}
                                    onKeyDown={(e) => {
                                      if (e.key === "Enter" || e.key === " ") {
                                        e.preventDefault();
                                        toggleExpandedModule(m.resource);
                                      }
                                    }}
                                  >
                                    <span className={`role-tree-caret${isOpen ? " open" : ""}`}>
                                      <Icon name="chev" size={14} />
                                    </span>
                                    <TriCheckbox
                                      state={state}
                                      editable={editable}
                                      onClick={() => toggleModule(m)}
                                    />
                                    <span className="role-tree-name role-tree-name--mod">
                                      {humanizeLabel(m.label)}
                                    </span>
                                    <span className="role-tree-badge">
                                      {onCount}/{m.perms.length}
                                    </span>
                                  </div>
                                  {isOpen ? (
                                    <div className="role-tree-children">
                                      {m.perms.map((p) => {
                                        const on = selected.has(p.name);
                                        return (
                                          <button
                                            key={p.name}
                                            type="button"
                                            className={`role-tree-leaf${on ? " on" : ""}${editable ? " editable" : ""}`}
                                            disabled={!editable}
                                            title={p.description || p.name}
                                            onClick={() => togglePerm(p.name)}
                                          >
                                            <TriCheckbox
                                              state={on ? "all" : "none"}
                                              editable={editable}
                                              onClick={() => togglePerm(p.name)}
                                              size={14}
                                            />
                                            {humanizeAction(p.action)}
                                          </button>
                                        );
                                      })}
                                    </div>
                                  ) : null}
                                </div>
                              );
                            })}
                          </div>
                        ) : null}
                      </div>
                    );
                  })}
                </div>
              )}

              {editable ? (
                <div className="actionbar" style={{ marginTop: 16 }}>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    disabled={savePerms.isPending}
                    onClick={() => savePerms.mutate()}
                  >
                    {savePerms.isPending ? "Saving…" : "Save permissions"}
                  </button>
                </div>
              ) : null}
            </>
          )}
        </div>
      </div>

      {pendingDelete ? (
        <ConfirmModal
          title="Delete role"
          icon="x"
          danger
          busy={remove.isPending}
          confirmLabel="Delete"
          body={
            <>
              Delete role <strong>{role?.name}</strong>? Users with only this role may lose access.
            </>
          }
          onCancel={() => setPendingDelete(false)}
          onConfirm={() => remove.mutate()}
        />
      ) : null}
    </>
  );
}
