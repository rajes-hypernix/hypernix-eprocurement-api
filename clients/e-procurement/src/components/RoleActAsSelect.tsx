import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { listRoles } from "@/api/identity";
import { useAuth } from "@/auth/use-auth";
import { useErrorDialog } from "@/feedback/ErrorDialogContext";

/** Top-bar role overlay picker — only rendered when canPreviewRoles. */
export function RoleActAsSelect() {
  const { canPreviewRoles, rolePreview, startRolePreview, stopRolePreview } = useAuth();
  const { showErrorFrom } = useErrorDialog();
  const navigate = useNavigate();
  const [pending, setPending] = useState(false);

  const rolesQuery = useQuery({
    queryKey: ["roles"],
    queryFn: listRoles,
    enabled: canPreviewRoles,
    staleTime: 60_000,
  });

  if (!canPreviewRoles) return null;

  const roles = [...(rolesQuery.data ?? [])].sort((a, b) => a.name.localeCompare(b.name));

  const onChange = (roleId: string) => {
    if (!roleId) {
      stopRolePreview();
      return;
    }
    const role = roles.find((r) => r.id === roleId);
    if (!role) return;
    setPending(true);
    void startRolePreview(role.id, role.name)
      .then(() => {
        void navigate("/dashboard", { replace: true });
      })
      .catch((e) => showErrorFrom(e, "Could not preview role"))
      .finally(() => setPending(false));
  };

  return (
    <label className="act-as">
      <span className="act-as-label">Act as</span>
      <select
        aria-label="Act as role"
        className="act-as-select"
        disabled={pending || rolesQuery.isPending}
        value={rolePreview?.roleId ?? ""}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="">My access</option>
        {roles.map((r) => (
          <option key={r.id} value={r.id}>
            {r.name}
          </option>
        ))}
      </select>
    </label>
  );
}
