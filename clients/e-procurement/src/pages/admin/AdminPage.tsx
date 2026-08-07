import { useNavigate, useParams } from "react-router-dom";
import {
  RolesListPage,
  RoleCreatePage,
  RoleDetailPage,
} from "@/pages/admin/AdminRoles";
import { UsersListPage } from "@/pages/admin/AdminUsers";

/** Route switch for /admin and /admin/* */
export function AdminPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/admin${path ? `/${path}` : ""}`);

  if (route === "roles") return <RolesListPage onNavigate={go} />;
  if (route === "roles/new") return <RoleCreatePage onBack={() => go("roles")} onSaved={(id) => go(`roles/${id}`)} />;
  if (route.startsWith("roles/")) {
    const roleId = route.slice("roles/".length);
    if (roleId && roleId !== "new") {
      return <RoleDetailPage roleId={roleId} onBack={() => go("roles")} />;
    }
  }

  // Legacy /admin/:userId and /admin/new → list (add/edit are dialogs on the list)
  return <UsersListPage onNavigate={go} />;
}
