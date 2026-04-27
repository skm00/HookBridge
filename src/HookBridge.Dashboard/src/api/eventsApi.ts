import { apiClient } from './apiClient';
import type { IncomingEventResponse, IncomingEventSearchRequest } from '../types/event';
import type { PagedResponse } from '../types/pagination';

const searchEvents = async (
  filters: IncomingEventSearchRequest = {}
): Promise<PagedResponse<IncomingEventResponse>> => {
  const response = await apiClient.get<PagedResponse<IncomingEventResponse>>('/api/v1/admin/events', {
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
