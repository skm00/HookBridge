import type { PagedRequest } from './pagination';

export type NotificationResponse = {
  id: string;
  tenantId: string;
  type: string;
  severity: 'Info' | 'Warning' | 'Error' | 'Critical' | string;
  title: string;
  message: string;
  resourceType: string | null;
  resourceId: string | null;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
};

export type NotificationSearchRequest = PagedRequest & {
  type?: string;
  severity?: string;
  isRead?: boolean;
  fromDate?: string;
  toDate?: string;
};

export type UnreadNotificationCountResponse = {
  unreadCount: number;
};
