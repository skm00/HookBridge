import axios from 'axios';
import { authStorage } from '../auth/authStorage';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;

type ApiResponse<T> = {
  success: boolean;
  message?: string | null;
  data?: T;
  traceId?: string | null;
};

type ApiErrorResponse = {
  success: false;
  message: string;
  statusCode: number;
  traceId?: string | null;
  errors?: Record<string, string[]>;
};

const isApiResponse = <T>(payload: unknown): payload is ApiResponse<T> => {
  return typeof payload === 'object' && payload !== null && 'success' in payload;
};

const formatApiError = (payload: ApiErrorResponse): string => {
  const validationErrors = payload.errors
    ? Object.values(payload.errors).flat().join(' ')
    : '';

  return [payload.message, validationErrors].filter(Boolean).join(' ').trim();
};

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    'Content-Type': 'application/json'
  }
});

apiClient.interceptors.request.use((config) => {
  const token = authStorage.getToken();

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => {
    if (isApiResponse(response.data)) {
      if (response.data.success) {
        return {
          ...response,
          data: response.data.data
        };
      }

      return Promise.reject(new Error(response.data.message ?? 'Request failed.'));
    }

    return response;
  },
  (error) => {
    if (error.response?.status === 401) {
      authStorage.clearToken();
      window.location.href = '/login';
    }

    if (isApiResponse(error.response?.data)) {
      const payload = error.response.data as ApiErrorResponse;
      return Promise.reject(new Error(formatApiError(payload)));
    }

    return Promise.reject(error);
  }
);
