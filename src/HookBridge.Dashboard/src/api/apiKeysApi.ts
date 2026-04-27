import { apiClient } from './apiClient';
import type { ApiKeyResponse, CreateApiKeyRequest, CreateApiKeyResponse } from '../types/apiKey';

const getApiKeys = async (tenantId: string): Promise<ApiKeyResponse[]> => {
  const response = await apiClient.get<ApiKeyResponse[]>(`/api/v1/admin/tenants/${tenantId}/api-keys`);
  return response.data;
};

const createApiKey = async (tenantId: string, request: CreateApiKeyRequest): Promise<CreateApiKeyResponse> => {
  const response = await apiClient.post<CreateApiKeyResponse>(`/api/v1/admin/tenants/${tenantId}/api-keys`, request);
  return response.data;
};

const revokeApiKey = async (tenantId: string, keyId: string): Promise<void> => {
  await apiClient.delete(`/api/v1/admin/tenants/${tenantId}/api-keys/${keyId}`);
};

export const apiKeysApi = {
  getApiKeys,
  createApiKey,
  revokeApiKey
};
