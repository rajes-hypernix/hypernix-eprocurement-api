import { useNavigate, useParams } from "react-router-dom";
import {
  RolesListPage,
  RoleCreatePage,
  RoleDetailPage,
} from "@/pages/admin/AdminRoles";
import {
  UsersListPage,
  UserCreatePage,
  UserDetailPage,
} from "@/pages/admin/AdminUsers";

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
  if (route === "new") return <UserCreatePage onBack={() => go("")} onSaved={(id) => go(id)} />;
  if (route) return <UserDetailPage userId={route} onBack={() => go("")} />;

  return <UsersListPage onNavigate={go} />;
}
