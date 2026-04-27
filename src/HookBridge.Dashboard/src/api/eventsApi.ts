import { apiClient } from './apiClient';
import type { IncomingEventResponse, IncomingEventSearchRequest } from '../types/event';

const searchEvents = async (
  filters: IncomingEventSearchRequest = {}
): Promise<IncomingEventResponse[]> => {
  const response = await apiClient.get<IncomingEventResponse[]>('/api/v1/admin/events', {
    params: filters
  });

  return response.data;
};

const getEventById = async (id: string): Promise<IncomingEventResponse> => {
  const response = await apiClient.get<IncomingEventResponse>(`/api/v1/admin/events/${id}`);
  return response.data;
};

export const eventsApi = {
  searchEvents,
  getEventById
};
