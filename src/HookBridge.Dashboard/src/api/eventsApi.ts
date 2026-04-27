import { apiClient } from './apiClient';
import type { IncomingEventResponse, IncomingEventSearchRequest } from '../types/event';
import type { PagedResponse } from '../types/paging';

const searchEvents = async (
  filters: IncomingEventSearchRequest = {}
): Promise<IncomingEventResponse[]> => {
  const response = await apiClient.get<PagedResponse<IncomingEventResponse>>('/api/v1/admin/events', {
    params: filters
  });

  return response.data.items;
};

const getEventById = async (id: string): Promise<IncomingEventResponse> => {
  const response = await apiClient.get<IncomingEventResponse>(`/api/v1/admin/events/${id}`);
  return response.data;
};

export const eventsApi = {
  searchEvents,
  getEventById
};
