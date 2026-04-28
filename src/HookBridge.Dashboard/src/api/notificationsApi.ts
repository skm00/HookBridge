import { apiClient } from './apiClient';
import type {
  NotificationResponse,
  NotificationSearchRequest,
  UnreadNotificationCountResponse
} from '../types/notification';
import type { PagedResponse } from '../types/pagination';

const searchNotifications = async (
  filters: NotificationSearchRequest = {}
): Promise<PagedResponse<NotificationResponse>> => {
  const response = await apiClient.get<PagedResponse<NotificationResponse>>('/api/v1/admin/notifications', {
    params: filters
  });

  return response.data;
};

const getNotificationById = async (id: string): Promise<NotificationResponse> => {
  const response = await apiClient.get<NotificationResponse>(`/api/v1/admin/notifications/${id}`);
  return response.data;
};

const markNotificationAsRead = async (id: string): Promise<void> => {
  await apiClient.post(`/api/v1/admin/notifications/${id}/read`);
};

const getUnreadNotificationCount = async (): Promise<UnreadNotificationCountResponse> => {
  const response = await apiClient.get<UnreadNotificationCountResponse>('/api/v1/admin/notifications/unread-count');
  return response.data;
};

export const notificationsApi = {
  searchNotifications,
  getNotificationById,
  markNotificationAsRead,
  getUnreadNotificationCount
};
