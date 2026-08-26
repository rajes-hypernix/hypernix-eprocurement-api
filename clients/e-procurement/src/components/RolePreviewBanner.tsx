import { useAuth } from "@/auth/use-auth";

export function RolePreviewBanner() {
  const { rolePreview, stopRolePreview } = useAuth();
  if (!rolePreview) return null;

  return (
    <div className="role-preview-banner" role="status">
      <span>
        Previewing role <strong>{rolePreview.roleName}</strong>. Menus and buttons match that role.
        You are still yourself — this is not a vendor login. Changes are blocked.
      </span>
      <button type="button" className="btn ghost btn-sm role-preview-stop" onClick={() => stopRolePreview()}>
        Stop preview
      </button>
    </div>
  );
}
