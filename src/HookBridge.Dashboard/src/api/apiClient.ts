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

type NormalizedError = {
  message: string;
  statusCode: number;
  traceId?: string | null;
  errors?: Record<string, string[]>;
};

type ErrorWithNormalizedData = Error & {
  normalizedError?: NormalizedError;
};

const isApiResponse = <T>(payload: unknown): payload is ApiResponse<T> => {
  return typeof payload === 'object' && payload !== null && 'success' in payload;
};

const isApiErrorResponse = (payload: unknown): payload is ApiErrorResponse => {
  if (!isApiResponse(payload) || payload.success !== false) {
    return false;
  }

  const statusCode = (payload as ApiErrorResponse).statusCode;
  const message = (payload as ApiErrorResponse).message;

  return typeof statusCode === 'number' && typeof message === 'string';
};

const getMessageForStatus = (statusCode: number, fallback: string): string => {
  if (statusCode === 403) {
    return 'You do not have permission to perform this action.';
  }

  if (statusCode === 429) {
    return 'Rate limit exceeded. Please try again later.';
  }

  return fallback;
};

const buildNormalizedError = (payload: ApiErrorResponse): NormalizedError => ({
  message: getMessageForStatus(payload.statusCode, payload.message),
  statusCode: payload.statusCode,
  traceId: payload.traceId ?? null,
  errors: payload.errors
});

export const createApiClientError = (normalizedError: NormalizedError): ErrorWithNormalizedData => {
  const error = new Error(normalizedError.message) as ErrorWithNormalizedData;
  error.normalizedError = normalizedError;
  return error;
};

export const handleAuthErrorStatus = (statusCode: number): void => {
  if (statusCode !== 401) {
    return;
  }

  authStorage.clearToken();
  window.location.href = '/login';
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

      if (isApiErrorResponse(response.data)) {
        const normalizedError = buildNormalizedError(response.data);
        if (normalizedError.statusCode === 401) {
          handleAuthErrorStatus(normalizedError.statusCode);
        }

        return Promise.reject(createApiClientError(normalizedError));
      }

      return Promise.reject(new Error(response.data.message ?? 'Request failed.'));
    }

    return response;
  },
  (error) => {
    if (isApiErrorResponse(error.response?.data)) {
      const normalizedError = buildNormalizedError(error.response.data);

      if (normalizedError.statusCode === 401) {
        handleAuthErrorStatus(normalizedError.statusCode);
      }

      return Promise.reject(createApiClientError(normalizedError));
    }

    if (error.response?.status) {
      handleAuthErrorStatus(error.response.status);
    }

    return Promise.reject(error);
  }
);
