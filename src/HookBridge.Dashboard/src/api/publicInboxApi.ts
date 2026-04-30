import apiClient from './apiClient';

export interface PublicInboxRequestItem {
  method: string;
  headers: Record<string, string>;
  body: string;
  receivedAt: string;
}

export interface PublicInboxState {
  token: string;
  expiresAt: string;
  maxRequests: number;
  requestCount: number;
  remainingRequests: number;
  isExpired: boolean;
  requests: PublicInboxRequestItem[];
}

export const createPublicInbox = async (): Promise<{ token: string; webhookUrl: string; expiresAt: string; maxRequests: number; remainingRequests: number }> => {
  const { data } = await apiClient.post('/api/v1/public/inbox');
  return data;
};

export const getPublicInbox = async (token: string): Promise<PublicInboxState> => {
  const { data } = await apiClient.get(`/api/v1/public/inbox/${token}`);
  return data;
};
