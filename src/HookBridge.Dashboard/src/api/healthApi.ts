import { apiClient } from './apiClient';
import type { HealthResponse } from '../types/health';

const getMongoHealth = async (): Promise<HealthResponse> => {
  const response = await apiClient.get<HealthResponse>('/api/v1/health/mongodb');
  return response.data;
};

const getKafkaHealth = async (): Promise<HealthResponse> => {
  const response = await apiClient.get<HealthResponse>('/api/v1/health/kafka');
  return response.data;
};

const getWorkerHealth = async (): Promise<HealthResponse> => {
  const response = await apiClient.get<HealthResponse>('/api/v1/health/worker');
  return response.data;
};

const getElasticHealth = async (): Promise<HealthResponse> => {
  const response = await apiClient.get<HealthResponse>('/api/v1/health/elasticsearch');
  return response.data;
};

export const healthApi = {
  getMongoHealth,
  getKafkaHealth,
  getWorkerHealth,
  getElasticHealth
};
