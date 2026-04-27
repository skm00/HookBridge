import { apiClient } from './apiClient';
import type {
  BillingStatusResponse,
  CheckoutSessionResponse,
  CreateCheckoutSessionRequest
} from '../types/billing';

const getBillingStatus = async (tenantId: string): Promise<BillingStatusResponse> => {
  const response = await apiClient.get<BillingStatusResponse>(`/api/v1/admin/tenants/${tenantId}/billing/status`);
  return response.data;
};

const createCheckoutSession = async (
  tenantId: string,
  request: CreateCheckoutSessionRequest
): Promise<CheckoutSessionResponse> => {
  const response = await apiClient.post<CheckoutSessionResponse>(`/api/v1/admin/tenants/${tenantId}/billing/checkout`, request);
  return response.data;
};

export const billingApi = {
  getBillingStatus,
  createCheckoutSession
};
