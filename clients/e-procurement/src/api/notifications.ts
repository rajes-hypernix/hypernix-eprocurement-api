import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery } from "@/api/types";

export type NotificationDto = {
  id: string;
  type: string;
  title: string;
  body?: string | null;
  link?: string | null;
  source: string;
  metadataJson: string;
  readAtUtc?: string | null;
  createdAtUtc: string;
};

const ROOT = ApiPaths.notifications;

export function listNotifications(
  params: { unreadOnly?: boolean; page?: number; pageSize?: number } = {},
): Promise<NotificationDto[]> {
  return apiFetch<NotificationDto[]>(
    `${ROOT}/${toQuery({
      unreadOnly: params.unreadOnly,
      page: params.page,
      pageSize: params.pageSize,
    })}`,
  );
}

export function getUnreadCount(): Promise<number> {
  return apiFetch<number>(`${ROOT}/unread-count`);
}

export function markNotificationRead(notificationId: string): Promise<void> {
  return apiFetch<void>(`${ROOT}/${encodeURIComponent(notificationId)}/read`, {
    method: "POST",
  });
}

export function markAllNotificationsRead(): Promise<{ updated: number }> {
  return apiFetch<{ updated: number }>(`${ROOT}/read-all`, { method: "POST" });
}
