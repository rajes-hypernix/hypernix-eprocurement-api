import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  adminSetPassword,
  adminUpdateUser,
  assignUserRoles,
  getUserRoles,
  registerUser,
  searchAllUsers,
  searchUsers,
  toggleUserStatus,
  type UserDto,
  type UserRoleDto,
} from "@/api/identity";
import { useAuth } from "@/auth/use-auth";
import { EntityChangeHistoryModal } from "@/components/EntityChangeHistoryModal";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { ConfirmModal, EmptyState, Modal, Spinner } from "@/components/ui";
import { useErrorDialog } from "@/feedback/ErrorDialogContext";
import { activeLabel, exportRowsToExcel } from "@/lib/excel";
import { dateTimeMY } from "@/lib/format";
import { FshPermissions } from "@/lib/fsh-permissions";

function displayName(u: UserDto): string {
  const n = [u.firstName, u.lastName].filter(Boolean).join(" ").trim();
  return n || u.userName || u.email || u.id || "User";
}

function IconButton({
  icon,
  label,
  onClick,
  danger,
}: {
  icon: string;
  label: string;
  onClick: () => void;
  danger?: boolean;
}) {
  return (
    <button
      type="button"
      className={`btn btn-out btn-sm btn-icon${danger ? " btn-danger-text" : ""}`}
      title={label}
      aria-label={label}
      onClick={(e) => {
        e.stopPropagation();
        onClick();
      }}
    >
      <Icon name={icon} size={14} />
    </button>
  );
}

function HistoryButton({
  label,
  entityId,
  onOpen,
}: {
  label: string;
  entityId: string;
  onOpen: (args: { title: string; entityId: string }) => void;
}) {
  return (
    <Gated permission={FshPermissions.auditTrails.view}>
      <IconButton icon="clock" label={`History · ${label}`} onClick={() => onOpen({ title: label, entityId })} />
    </Gated>
  );
}

type UserExportRow = UserDto & { roles: string };

async function buildUserExportRows(
  users: UserDto[],
  onProgress?: (done: number, total: number) => void,
): Promise<UserExportRow[]> {
  const rows: UserExportRow[] = [];
  const total = users.length;
  const concurrency = 6;
  let index = 0;
  let done = 0;

  async function worker() {
    while (index < users.length) {
      const i = index++;
      const user = users[i]!;
      let roles = "";
      if (user.id) {
        try {
          const all = await getUserRoles(user.id);
          roles = all
            .filter((r) => r.enabled)
            .map((r) => r.roleName)
            .filter(Boolean)
            .join(", ");
        } catch {
          roles = "";
        }
      }
      rows[i] = { ...user, roles };
      done += 1;
      onProgress?.(done, total);
    }
  }

  await Promise.all(Array.from({ length: Math.min(concurrency, Math.max(1, total)) }, () => worker()));
  return rows;
}

type CreateForm = {
  firstName: string;
  lastName: string;
  email: string;
  userName: string;
  phoneNumber: string;
  password: string;
  confirmPassword: string;
};

const emptyCreate: CreateForm = {
  firstName: "",
  lastName: "",
  email: "",
  userName: "",
  phoneNumber: "",
  password: "",
  confirmPassword: "",
};

type EditForm = {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
};

export function UsersListPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const qc = useQueryClient();
  const { user: authUser } = useAuth();
  const canManageRoles = (authUser?.permissions ?? []).includes(FshPermissions.users.manageRoles);
  const { showError, showErrorFrom } = useErrorDialog();
  const [q, setQ] = useState("");
  const [search, setSearch] = useState("");
  const [active, setActive] = useState<"all" | "yes" | "no">("all");
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);
  const pageSize = 20;

  const [history, setHistory] = useState<{ title: string; entityId: string } | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState<CreateForm>(emptyCreate);
  const [editing, setEditing] = useState<UserDto | null>(null);
  const [editForm, setEditForm] = useState<EditForm>({
    firstName: "",
    lastName: "",
    email: "",
    phoneNumber: "",
  });
  const [roleDraft, setRoleDraft] = useState<UserRoleDto[]>([]);
  const [passwordUser, setPasswordUser] = useState<UserDto | null>(null);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [pendingStatus, setPendingStatus] = useState<{ user: UserDto; activate: boolean } | null>(null);

  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(q);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const filterParams = {
    search: search.trim() || undefined,
    isActive: active === "all" ? undefined : active === "yes",
  } as const;

  const { data, isPending, isFetching } = useQuery({
    queryKey: ["users", search, active, page, pageSize],
    queryFn: () =>
      searchUsers({
        ...filterParams,
        pageNumber: page,
        pageSize,
      }),
  });

  const rolesQuery = useQuery({
    queryKey: ["user", editing?.id, "roles"],
    queryFn: () => getUserRoles(editing!.id!),
    enabled: !!editing?.id,
  });

  useEffect(() => {
    if (rolesQuery.data) setRoleDraft(rolesQuery.data.map((r) => ({ ...r })));
  }, [rolesQuery.data]);

  const users = data?.items ?? [];
  const totalPages = Math.max(1, data?.totalPages ?? 1);
  const totalCount = data?.totalCount ?? 0;

  const invalidateUsers = () => void qc.invalidateQueries({ queryKey: ["users"] });

  const createUser = useMutation({
    mutationFn: () =>
      registerUser({
        firstName: createForm.firstName.trim(),
        lastName: createForm.lastName.trim(),
        email: createForm.email.trim(),
        userName: createForm.userName.trim(),
        phoneNumber: createForm.phoneNumber.trim() || undefined,
        password: createForm.password,
        confirmPassword: createForm.confirmPassword,
      }),
    onSuccess: () => {
      setCreateOpen(false);
      setCreateForm(emptyCreate);
      invalidateUsers();
    },
    onError: (e) => showErrorFrom(e, "Could not create user"),
  });

  const saveEdit = useMutation({
    mutationFn: async () => {
      if (!editing?.id) throw new Error("Missing user");
      await adminUpdateUser(editing.id, {
        firstName: editForm.firstName.trim(),
        lastName: editForm.lastName.trim(),
        email: editForm.email.trim(),
        phoneNumber: editForm.phoneNumber.trim() || null,
      });
      if (canManageRoles) {
        await assignUserRoles(editing.id, roleDraft);
      }
    },
    onSuccess: () => {
      setEditing(null);
      invalidateUsers();
    },
    onError: (e) => showErrorFrom(e, "Could not update user"),
  });

  const setUserPassword = useMutation({
    mutationFn: () => {
      if (!passwordUser?.id) throw new Error("Missing user");
      return adminSetPassword(passwordUser.id, { password, confirmPassword });
    },
    onSuccess: () => {
      setPasswordUser(null);
      setPassword("");
      setConfirmPassword("");
    },
    onError: (e) => showErrorFrom(e, "Could not set password"),
  });

  const toggleStatus = useMutation({
    mutationFn: () => {
      if (!pendingStatus?.user.id) throw new Error("Missing user");
      return toggleUserStatus(pendingStatus.user.id, pendingStatus.activate);
    },
    onSuccess: () => {
      setPendingStatus(null);
      invalidateUsers();
    },
    onError: (e) => showErrorFrom(e, "Could not update user status"),
  });

  const openCreate = () => {
    setCreateForm(emptyCreate);
    setCreateOpen(true);
  };

  const openEdit = (u: UserDto) => {
    setEditing(u);
    setEditForm({
      firstName: u.firstName ?? "",
      lastName: u.lastName ?? "",
      email: u.email ?? "",
      phoneNumber: u.phoneNumber ?? "",
    });
    setRoleDraft([]);
  };

  const openPassword = (u: UserDto) => {
    setPasswordUser(u);
    setPassword("");
    setConfirmPassword("");
  };

  const submitCreate = () => {
    if (
      !createForm.firstName.trim() ||
      !createForm.lastName.trim() ||
      !createForm.email.trim() ||
      !createForm.userName.trim()
    ) {
      showError("Fill in all required fields.");
      return;
    }
    if (!createForm.password || !createForm.confirmPassword) {
      showError("Password and confirm password are required.");
      return;
    }
    if (createForm.password !== createForm.confirmPassword) {
      showError("Passwords do not match.");
      return;
    }
    createUser.mutate();
  };

  const submitEdit = () => {
    if (!editForm.firstName.trim() || !editForm.lastName.trim() || !editForm.email.trim()) {
      showError("First name, last name, and email are required.");
      return;
    }
    saveEdit.mutate();
  };

  const submitPassword = () => {
    if (!password || !confirmPassword) {
      showError("Password and confirm password are required.");
      return;
    }
    if (password !== confirmPassword) {
      showError("Passwords do not match.");
      return;
    }
    if (password.length < 6) {
      showError("Password must be at least 6 characters.");
      return;
    }
    setUserPassword.mutate();
  };

  const exportUsers = async () => {
    setExporting(true);
    try {
      const all = await searchAllUsers(filterParams);
      const rows = await buildUserExportRows(all);
      exportRowsToExcel(
        "users.xlsx",
        "Users",
        [
          { header: "Username", key: "userName", value: (u) => u.userName ?? "" },
          { header: "First name", key: "firstName", value: (u) => u.firstName ?? "" },
          { header: "Last name", key: "lastName", value: (u) => u.lastName ?? "" },
          { header: "Email", key: "email", value: (u) => u.email ?? "" },
          { header: "Phone", key: "phone", value: (u) => u.phoneNumber ?? "" },
          { header: "Active", key: "active", value: (u) => activeLabel(u.isActive) },
          {
            header: "Email confirmed",
            key: "emailConfirmed",
            value: (u) => activeLabel(u.emailConfirmed),
          },
          {
            header: "Created",
            key: "created",
            value: (u) => (u.createdOnUtc ? dateTimeMY(u.createdOnUtc) : ""),
          },
          { header: "Roles", key: "roles", value: (u) => u.roles },
        ],
        rows,
      );
    } catch (e) {
      showErrorFrom(e, "Could not export users");
    } finally {
      setExporting(false);
    }
  };

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>User management</h1>
          <p>Directory of e-procurement users, roles, and access.</p>
        </div>
        <div className="spacer" />
        <Gated permission={FshPermissions.users.view}>
          <button
            type="button"
            className="btn btn-out btn-sm"
            disabled={exporting || totalCount === 0}
            onClick={() => void exportUsers()}
          >
            {exporting ? "Exporting…" : "Export Excel"}
          </button>
        </Gated>
        <button type="button" className="btn btn-out btn-sm" onClick={() => onNavigate("roles")}>
          <Icon name="users" size={15} /> Roles
        </button>
        <Gated permission={FshPermissions.users.create}>
          <button type="button" className="btn btn-pri btn-sm" onClick={openCreate}>
            <Icon name="plus" size={15} /> New user
          </button>
        </Gated>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search name / email / username</label>
              <input value={q} placeholder="Search…" onChange={(e) => setQ(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 160 }}>
              <label>Active</label>
              <select
                value={active}
                onChange={(e) => {
                  setActive(e.target.value as "all" | "yes" | "no");
                  setPage(1);
                }}
              >
                <option value="all">All</option>
                <option value="yes">Active</option>
                <option value="no">Inactive</option>
              </select>
            </div>
            {isFetching && !isPending ? (
              <span className="hint" style={{ alignSelf: "end", marginBottom: 8 }}>
                Refreshing…
              </span>
            ) : null}
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading users…" />
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>User</th>
                  <th>Username</th>
                  <th>Email confirmed</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div style={{ fontWeight: 700 }}>{displayName(u)}</div>
                      <div className="hint">{u.email}</div>
                    </td>
                    <td>{u.userName}</td>
                    <td>{u.emailConfirmed ? "Yes" : "No"}</td>
                    <td>
                      <span className={`badge ${u.isActive ? "b-green" : "b-red"}`}>
                        {u.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td>
                      <span className="hint">{u.createdOnUtc ? dateTimeMY(u.createdOnUtc) : "—"}</span>
                    </td>
                    <td className="amt">
                      <div style={{ display: "flex", gap: 6, justifyContent: "flex-end", flexWrap: "wrap" }}>
                        {u.id ? (
                          <HistoryButton label={displayName(u)} entityId={u.id} onOpen={setHistory} />
                        ) : null}
                        <Gated permission={FshPermissions.users.update}>
                          <IconButton icon="edit" label="Edit" onClick={() => openEdit(u)} />
                          <IconButton icon="lock" label="Set password" onClick={() => openPassword(u)} />
                          <IconButton
                            icon={u.isActive ? "x" : "check"}
                            label={u.isActive ? "Deactivate" : "Activate"}
                            danger={u.isActive}
                            onClick={() => setPendingStatus({ user: u, activate: !u.isActive })}
                          />
                        </Gated>
                      </div>
                    </td>
                  </tr>
                ))}
                {users.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      <EmptyState icon="users">No users match the filter.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
            {totalPages > 1 ? (
              <div className="actionbar" style={{ padding: "12px 16px" }}>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </button>
                <span className="hint" style={{ margin: "0 12px" }}>
                  Page {page} of {totalPages} · {totalCount} total
                </span>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </button>
              </div>
            ) : (
              <p className="hint" style={{ margin: "10px 16px 12px", textAlign: "right" }}>
                {totalCount} total
              </p>
            )}
          </>
        )}
      </div>

      {createOpen ? (
        <Modal
          title="New user"
          icon="plus"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setCreateOpen(false)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={createUser.isPending}
                onClick={submitCreate}
              >
                {createUser.isPending ? "Creating…" : "Create user"}
              </button>
            </>
          }
        >
          <p className="hint" style={{ marginTop: 0 }}>
            Register a new e-procurement account. A confirmation email will be sent.
          </p>
          <div className="grid2">
            <div className="field">
              <label>First name</label>
              <input
                value={createForm.firstName}
                onChange={(e) => setCreateForm((f) => ({ ...f, firstName: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Last name</label>
              <input
                value={createForm.lastName}
                onChange={(e) => setCreateForm((f) => ({ ...f, lastName: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Email</label>
              <input
                type="email"
                value={createForm.email}
                onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Username</label>
              <input
                value={createForm.userName}
                onChange={(e) => setCreateForm((f) => ({ ...f, userName: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Phone (optional)</label>
              <input
                value={createForm.phoneNumber}
                onChange={(e) => setCreateForm((f) => ({ ...f, phoneNumber: e.target.value }))}
              />
            </div>
            <div className="field" />
            <div className="field">
              <label>Password</label>
              <input
                type="password"
                value={createForm.password}
                onChange={(e) => setCreateForm((f) => ({ ...f, password: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Confirm password</label>
              <input
                type="password"
                value={createForm.confirmPassword}
                onChange={(e) => setCreateForm((f) => ({ ...f, confirmPassword: e.target.value }))}
              />
            </div>
          </div>
        </Modal>
      ) : null}

      {editing ? (
        <Modal
          title={`Edit user · ${displayName(editing)}`}
          icon="edit"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={saveEdit.isPending}
                onClick={submitEdit}
              >
                {saveEdit.isPending ? "Saving…" : "Save"}
              </button>
            </>
          }
        >
          <div className="grid2">
            <div className="field">
              <label>First name</label>
              <input
                value={editForm.firstName}
                onChange={(e) => setEditForm((f) => ({ ...f, firstName: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Last name</label>
              <input
                value={editForm.lastName}
                onChange={(e) => setEditForm((f) => ({ ...f, lastName: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Email</label>
              <input
                type="email"
                value={editForm.email}
                onChange={(e) => setEditForm((f) => ({ ...f, email: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Phone</label>
              <input
                value={editForm.phoneNumber}
                onChange={(e) => setEditForm((f) => ({ ...f, phoneNumber: e.target.value }))}
              />
            </div>
            <div className="field">
              <label>Username</label>
              <input value={editing.userName ?? ""} disabled />
              <span className="hint">Username cannot be changed here.</span>
            </div>
            <div className="field">
              <label>Status</label>
              <div style={{ paddingTop: 8 }}>
                <span className={`badge ${editing.isActive ? "b-green" : "b-red"}`}>
                  {editing.isActive ? "Active" : "Inactive"}
                </span>
                <span className="hint" style={{ marginLeft: 8 }}>
                  {editing.emailConfirmed ? "Email confirmed" : "Email not confirmed"}
                </span>
              </div>
            </div>
          </div>

          <div style={{ marginTop: 16 }}>
            <h4 style={{ margin: "0 0 10px" }}>Roles</h4>
            {rolesQuery.isPending ? (
              <Spinner label="Loading roles…" />
            ) : (
              <>
                {roleDraft.map((r, i) => (
                  <label key={r.roleId ?? i} style={{ display: "flex", gap: 10, marginBottom: 10 }}>
                    <Gated
                      permission={FshPermissions.users.manageRoles}
                      fallback={<input type="checkbox" checked={r.enabled} readOnly disabled />}
                    >
                      <input
                        type="checkbox"
                        checked={r.enabled}
                        onChange={(e) =>
                          setRoleDraft((prev) =>
                            prev.map((x, j) => (j === i ? { ...x, enabled: e.target.checked } : x)),
                          )
                        }
                      />
                    </Gated>
                    <span>
                      <strong>{r.roleName}</strong>
                      {r.description ? <span className="hint"> — {r.description}</span> : null}
                    </span>
                  </label>
                ))}
                {roleDraft.length === 0 ? <EmptyState>No roles in catalog.</EmptyState> : null}
              </>
            )}
          </div>
        </Modal>
      ) : null}

      {passwordUser ? (
        <Modal
          title={`Set password · ${displayName(passwordUser)}`}
          icon="lock"
          footer={
            <>
              <button
                type="button"
                className="btn btn-out"
                onClick={() => {
                  setPasswordUser(null);
                  setPassword("");
                  setConfirmPassword("");
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={setUserPassword.isPending}
                onClick={submitPassword}
              >
                {setUserPassword.isPending ? "Saving…" : "Set password"}
              </button>
            </>
          }
        >
          <p className="hint" style={{ marginTop: 0 }}>
            Set a new password for this user. They can change it later from their profile.
          </p>
          <div className="grid2">
            <div className="field">
              <label>New password</label>
              <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>
            <div className="field">
              <label>Confirm password</label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
              />
            </div>
          </div>
        </Modal>
      ) : null}

      {pendingStatus ? (
        <ConfirmModal
          title={pendingStatus.activate ? "Activate user" : "Deactivate user"}
          icon={pendingStatus.activate ? "check" : "x"}
          danger={!pendingStatus.activate}
          busy={toggleStatus.isPending}
          confirmLabel={pendingStatus.activate ? "Activate" : "Deactivate"}
          body={
            pendingStatus.activate ? (
              <>
                Activate user <strong>{displayName(pendingStatus.user)}</strong>?
              </>
            ) : (
              <>
                Deactivate user <strong>{displayName(pendingStatus.user)}</strong>? They will not be
                able to sign in until reactivated.
              </>
            )
          }
          onCancel={() => setPendingStatus(null)}
          onConfirm={() => toggleStatus.mutate()}
        />
      ) : null}

      {history ? (
        <EntityChangeHistoryModal
          title={history.title}
          entityId={history.entityId}
          preset="user"
          onClose={() => setHistory(null)}
        />
      ) : null}
    </>
  );
}
