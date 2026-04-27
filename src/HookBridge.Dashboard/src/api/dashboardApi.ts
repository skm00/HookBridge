import { apiClient } from './apiClient';
import type { DashboardOverviewResponse } from '../types/dashboard';

const getOverview = async (): Promise<DashboardOverviewResponse> => {
  const response = await apiClient.get<DashboardOverviewResponse>('/api/v1/admin/dashboard/overview');
  return response.data;
};

export const dashboardApi = {
  getOverview
};
