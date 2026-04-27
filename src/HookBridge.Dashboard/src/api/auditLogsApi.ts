import { apiClient } from './apiClient';
import type { AuditLogResponse, AuditLogSearchRequest } from '../types/auditLog';
import type { PagedResponse } from '../types/pagination';

const searchAuditLogs = async (
  filters: AuditLogSearchRequest = {}
): Promise<PagedResponse<AuditLogResponse>> => {
  const response = await apiClient.get<PagedResponse<AuditLogResponse>>('/api/v1/admin/audit-logs', {
    params: filters
  });

  return response.data;
};

const getAuditLogById = async (id: string): Promise<AuditLogResponse> => {
  const response = await apiClient.get<AuditLogResponse>(`/api/v1/admin/audit-logs/${id}`);
  return response.data;
};

export const auditLogsApi = {
  searchAuditLogs,
  getAuditLogById
};
