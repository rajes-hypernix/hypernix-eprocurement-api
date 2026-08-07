import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery, type PagedResponse } from "@/api/types";

export type UserDto = {
  id?: string;
  userName?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  isActive: boolean;
  emailConfirmed: boolean;
  phoneNumber?: string;
  imageUrl?: string;
  twoFactorEnabled?: boolean;
  createdOnUtc?: string;
};

export type UserRoleDto = {
  roleId?: string;
  roleName?: string;
  description?: string;
  enabled: boolean;
};

export type RoleDto = {
  id: string;
  name: string;
  description?: string | null;
  createdOnUtc?: string;
  permissions?: string[] | null;
};

export type SearchUsersParams = {
  pageNumber?: number;
  pageSize?: number;
  sort?: string;
  search?: string;
  isActive?: boolean | null;
  emailConfirmed?: boolean | null;
  roleId?: string | null;
};

export type RegisterUserInput = {
  firstName: string;
  lastName: string;
  email: string;
  userName: string;
  password: string;
  confirmPassword: string;
  phoneNumber?: string;
};

export type RegisterUserResponse = {
  userId: string;
  message?: string;
};

export type UpsertRoleInput = {
  id: string;
  name: string;
  description?: string;
};

export type PermissionCatalogEntryDto = {
  name: string;
  description: string;
  resource: string;
  action: string;
  isBasic: boolean;
  isRoot: boolean;
};

export async function getMyPermissions(): Promise<string[]> {
  return (await apiFetch<string[] | null>(`${ApiPaths.identity}/permissions`)) ?? [];
}

export async function getMyProfile(): Promise<UserDto> {
  return apiFetch<UserDto>(`${ApiPaths.identity}/profile`);
}

export type UpdateProfileInput = {
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
};

/** Self-service profile update (name / phone). Email changes require an admin. */
export async function updateMyProfile(input: UpdateProfileInput): Promise<void> {
  const profile = await getMyProfile();
  await apiFetch<unknown>(`${ApiPaths.identity}/profile`, {
    method: "PUT",
    body: JSON.stringify({
      id: profile.id,
      firstName: input.firstName ?? profile.firstName ?? null,
      lastName: input.lastName ?? profile.lastName ?? null,
      phoneNumber: input.phoneNumber ?? profile.phoneNumber ?? null,
      email: profile.email,
      deleteCurrentImage: false,
    }),
  });
}

/** Authenticated password change (current + new + confirm). */
export async function changePassword(input: {
  password: string;
  newPassword: string;
  confirmNewPassword: string;
}): Promise<void> {
  await apiFetch<string>(`${ApiPaths.identity}/change-password`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export async function searchUsers(params: SearchUsersParams = {}): Promise<PagedResponse<UserDto>> {
  return apiFetch<PagedResponse<UserDto>>(
    `${ApiPaths.identity}/users/search${toQuery({
      PageNumber: params.pageNumber ?? 1,
      PageSize: params.pageSize ?? 20,
      Sort: params.sort,
      Search: params.search,
      IsActive: params.isActive ?? undefined,
      EmailConfirmed: params.emailConfirmed ?? undefined,
      RoleId: params.roleId ?? undefined,
    })}`,
  );
}

/** Load every page for the current filter (used by Excel export). */
export async function searchAllUsers(params: Omit<SearchUsersParams, "pageNumber" | "pageSize"> = {}): Promise<UserDto[]> {
  const pageSize = 100; // API max (PagedQueryValidator)
  const first = await searchUsers({ ...params, pageNumber: 1, pageSize });
  const items = [...(first.items ?? [])];
  const totalPages = Math.max(1, first.totalPages ?? 1);
  for (let page = 2; page <= totalPages; page++) {
    const next = await searchUsers({ ...params, pageNumber: page, pageSize });
    items.push(...(next.items ?? []));
  }
  return items;
}

export async function getUserById(id: string): Promise<UserDto> {
  return apiFetch<UserDto>(`${ApiPaths.identity}/users/${encodeURIComponent(id)}`);
}

export async function getUserRoles(id: string): Promise<UserRoleDto[]> {
  return apiFetch<UserRoleDto[]>(`${ApiPaths.identity}/users/${encodeURIComponent(id)}/roles`);
}

export async function assignUserRoles(userId: string, userRoles: UserRoleDto[]): Promise<string> {
  return apiFetch<string>(`${ApiPaths.identity}/users/${encodeURIComponent(userId)}/roles`, {
    method: "POST",
    body: JSON.stringify({ userId, userRoles }),
  });
}

export async function toggleUserStatus(userId: string, activate: boolean): Promise<void> {
  await apiFetch<void>(`${ApiPaths.identity}/users/${encodeURIComponent(userId)}`, {
    method: "PATCH",
    body: JSON.stringify({ userId, activateUser: activate }),
  });
}

export async function deleteUser(userId: string): Promise<void> {
  await apiFetch<void>(`${ApiPaths.identity}/users/${encodeURIComponent(userId)}`, {
    method: "DELETE",
  });
}

export async function registerUser(input: RegisterUserInput): Promise<RegisterUserResponse> {
  return apiFetch<RegisterUserResponse>(`${ApiPaths.identity}/register`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export type AdminUpdateUserInput = {
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
  email?: string | null;
};

/** Admin update of another user's profile (name / phone / email). */
export async function adminUpdateUser(userId: string, input: AdminUpdateUserInput): Promise<void> {
  await apiFetch<void>(`${ApiPaths.identity}/users/${encodeURIComponent(userId)}`, {
    method: "PUT",
    body: JSON.stringify({
      userId,
      firstName: input.firstName,
      lastName: input.lastName,
      phoneNumber: input.phoneNumber ?? null,
      email: input.email ?? null,
    }),
  });
}

/** Admin sets a new password for a user (no current password required). */
export async function adminSetPassword(
  userId: string,
  input: { password: string; confirmPassword: string },
): Promise<string> {
  return apiFetch<string>(`${ApiPaths.identity}/users/${encodeURIComponent(userId)}/set-password`, {
    method: "POST",
    body: JSON.stringify({
      userId,
      password: input.password,
      confirmPassword: input.confirmPassword,
    }),
  });
}

export async function listRoles(): Promise<RoleDto[]> {
  const result = await apiFetch<RoleDto[] | { items?: RoleDto[] }>(`${ApiPaths.identity}/roles`);
  if (Array.isArray(result)) return result;
  return result.items ?? [];
}

export async function getRoleWithPermissions(id: string): Promise<RoleDto> {
  return apiFetch<RoleDto>(`${ApiPaths.identity}/${encodeURIComponent(id)}/permissions`);
}

export async function upsertRole(input: UpsertRoleInput): Promise<RoleDto> {
  return apiFetch<RoleDto>(`${ApiPaths.identity}/roles`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export async function updateRolePermissions(roleId: string, permissions: string[]): Promise<string> {
  return apiFetch<string>(`${ApiPaths.identity}/${encodeURIComponent(roleId)}/permissions`, {
    method: "PUT",
    body: JSON.stringify({ roleId, permissions }),
  });
}

export async function deleteRole(id: string): Promise<void> {
  await apiFetch<void>(`${ApiPaths.identity}/roles/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export async function getPermissionsCatalog(): Promise<PermissionCatalogEntryDto[]> {
  return apiFetch<PermissionCatalogEntryDto[]>(`${ApiPaths.identity}/permissions/catalog`);
}
