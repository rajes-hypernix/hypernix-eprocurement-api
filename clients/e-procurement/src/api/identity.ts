import { apiFetch } from "@/lib/api-client";

export async function getMyPermissions(): Promise<string[]> {
  return (await apiFetch<string[] | null>("/api/v1/identity/permissions")) ?? [];
}
