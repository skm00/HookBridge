import { apiClient } from './apiClient';
import type { DeliveryAttemptResponse, DeliveryAttemptSearchRequest } from '../types/deliveryLog';
import type { PagedResponse } from '../types/paging';

const searchDeliveryLogs = async (
  filters: DeliveryAttemptSearchRequest = {}
): Promise<DeliveryAttemptResponse[]> => {
  const response = await apiClient.get<PagedResponse<DeliveryAttemptResponse>>('/api/v1/admin/delivery-logs', {
    params: filters
  });

  return response.data.items;
};

const getDeliveryLogById = async (id: string): Promise<DeliveryAttemptResponse> => {
  const response = await apiClient.get<DeliveryAttemptResponse>(`/api/v1/admin/delivery-logs/${id}`);
  return response.data;
};

export const deliveryLogsApi = {
  searchDeliveryLogs,
  getDeliveryLogById
};
