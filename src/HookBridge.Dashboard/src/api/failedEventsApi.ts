import { apiClient } from './apiClient';
import type { FailedEventResponse, FailedEventSearchRequest } from '../types/failedEvent';

const searchFailedEvents = async (
  filters: FailedEventSearchRequest = {}
): Promise<FailedEventResponse[]> => {
  const response = await apiClient.get<FailedEventResponse[]>('/api/v1/admin/failed-events', {
    params: filters
  });

  return response.data;
};

const getFailedEventById = async (id: string): Promise<FailedEventResponse> => {
  const response = await apiClient.get<FailedEventResponse>(`/api/v1/admin/failed-events/${id}`);
  return response.data;
};

const retryFailedEvent = async (id: string): Promise<void> => {
  await apiClient.post(`/api/v1/admin/failed-events/${id}/retry`);
};

export const failedEventsApi = {
  searchFailedEvents,
  getFailedEventById,
  retryFailedEvent
};
