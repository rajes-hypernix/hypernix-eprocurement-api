import { apiFetch } from "@/lib/api-client";
import { ApiPaths } from "@/api/types";

export async function getMyPermissions(): Promise<string[]> {
  return (await apiFetch<string[] | null>(`${ApiPaths.identity}/permissions`)) ?? [];
}
