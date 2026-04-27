import { apiClient } from './apiClient';
import type { DeliveryAttemptResponse, DeliveryAttemptSearchRequest } from '../types/deliveryLog';
import type { PagedResponse } from '../types/pagination';

const searchDeliveryLogs = async (
  filters: DeliveryAttemptSearchRequest = {}
): Promise<PagedResponse<DeliveryAttemptResponse>> => {
  const response = await apiClient.get<PagedResponse<DeliveryAttemptResponse>>('/api/v1/admin/delivery-logs', {
    params: filters
  });

  return response.data;
};

const getDeliveryLogById = async (id: string): Promise<DeliveryAttemptResponse> => {
  const response = await apiClient.get<DeliveryAttemptResponse>(`/api/v1/admin/delivery-logs/${id}`);
  return response.data;
};

export const deliveryLogsApi = {
  searchDeliveryLogs,
  getDeliveryLogById
};
