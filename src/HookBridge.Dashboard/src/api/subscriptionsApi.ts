import { apiClient } from './apiClient';
import type {
  CreateSubscriptionRequest,
  Subscription,
  SubscriptionListFilters,
  UpdateSubscriptionRequest
} from '../types/subscription';
import type { PagedResponse } from '../types/pagination';

const getSubscriptions = async (filters?: SubscriptionListFilters): Promise<PagedResponse<Subscription>> => {
  const response = await apiClient.get<PagedResponse<Subscription>>('/api/v1/admin/subscriptions', {
    params: filters
  });

  return response.data;
};

const getSubscriptionById = async (id: string): Promise<Subscription> => {
  const response = await apiClient.get<Subscription>(`/api/v1/admin/subscriptions/${id}`);
  return response.data;
};

const createSubscription = async (request: CreateSubscriptionRequest): Promise<Subscription> => {
  const response = await apiClient.post<Subscription>('/api/v1/admin/subscriptions', request);
  return response.data;
};

const updateSubscription = async (id: string, request: UpdateSubscriptionRequest): Promise<Subscription> => {
  const response = await apiClient.put<Subscription>(`/api/v1/admin/subscriptions/${id}`, request);
  return response.data;
};

const deleteSubscription = async (id: string): Promise<void> => {
  await apiClient.delete(`/api/v1/admin/subscriptions/${id}`);
};

const enableSubscription = async (id: string): Promise<void> => {
  await apiClient.post(`/api/v1/admin/subscriptions/${id}/enable`);
};

const disableSubscription = async (id: string): Promise<void> => {
  await apiClient.post(`/api/v1/admin/subscriptions/${id}/disable`);
};

export const subscriptionsApi = {
  getSubscriptions,
  getSubscriptionById,
  createSubscription,
  updateSubscription,
  deleteSubscription,
  enableSubscription,
  disableSubscription
};
