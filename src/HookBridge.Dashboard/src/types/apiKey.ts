export interface ApiKeyResponse {
  id: string;
  tenantId: string;
  name: string;
  keyPrefix: string;
  isActive: boolean;
  lastUsedAt: string | null;
  revokedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateApiKeyRequest {
  name: string;
}

export interface CreateApiKeyResponse {
  plainApiKey: string;
  apiKey: ApiKeyResponse;
}
