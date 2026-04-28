import axios from 'axios';

const FORBIDDEN_MESSAGE = 'You do not have permission to perform this action.';
const RATE_LIMIT_MESSAGE = 'Rate limit exceeded. Please try again later.';

type ApiErrorData = {
  success?: boolean;
  message?: string;
  statusCode?: number;
  traceId?: string | null;
  errors?: Record<string, string[]>;
};

type NormalizedError = {
  message: string;
  statusCode?: number;
  traceId?: string | null;
  errors?: Record<string, string[]>;
};

const toCamelCase = (key: string): string => key.charAt(0).toLowerCase() + key.slice(1);
const toPascalCase = (key: string): string => key.charAt(0).toUpperCase() + key.slice(1);

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null;

const isApiErrorData = (value: unknown): value is ApiErrorData => {
  if (!isRecord(value)) {
    return false;
  }

  return value.success === false || typeof value.message === 'string' || typeof value.statusCode === 'number';
};

const normalizeValidationErrors = (errors: unknown): Record<string, string[]> => {
  if (!isRecord(errors)) {
    return {};
  }

  return Object.entries(errors).reduce<Record<string, string[]>>((accumulator, [key, value]) => {
    if (!Array.isArray(value)) {
      return accumulator;
    }

    const messages = value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0);
    if (messages.length === 0) {
      return accumulator;
    }

    accumulator[key] = messages;
    const camelCaseKey = toCamelCase(key);
    const pascalCaseKey = toPascalCase(key);

    if (!accumulator[camelCaseKey]) {
      accumulator[camelCaseKey] = messages;
    }

    if (!accumulator[pascalCaseKey]) {
      accumulator[pascalCaseKey] = messages;
    }

    return accumulator;
  }, {});
};

const getNormalizedError = (error: unknown): NormalizedError | null => {
  if (isRecord(error) && isRecord(error.normalizedError)) {
    const normalized = error.normalizedError;
    return {
      message: typeof normalized.message === 'string' ? normalized.message : 'Request failed.',
      statusCode: typeof normalized.statusCode === 'number' ? normalized.statusCode : undefined,
      traceId: typeof normalized.traceId === 'string' ? normalized.traceId : null,
      errors: normalizeValidationErrors(normalized.errors)
    };
  }

  if (axios.isAxiosError(error) && isApiErrorData(error.response?.data)) {
    const payload = error.response?.data;
    return {
      message: payload.message ?? 'Request failed.',
      statusCode: payload.statusCode ?? error.response?.status,
      traceId: payload.traceId ?? null,
      errors: normalizeValidationErrors(payload.errors)
    };
  }

  return null;
};

export const getErrorMessage = (error: unknown): string => {
  const normalized = getNormalizedError(error);

  if (normalized?.statusCode === 403) {
    return FORBIDDEN_MESSAGE;
  }

  if (normalized?.statusCode === 429) {
    return RATE_LIMIT_MESSAGE;
  }

  if (normalized?.message?.trim()) {
    return normalized.message;
  }

  if (axios.isAxiosError(error) && !error.response) {
    return 'Network error. Please check your connection and try again.';
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'An unexpected error occurred.';
};

export const getValidationErrors = (error: unknown): Record<string, string[]> => {
  const normalized = getNormalizedError(error);
  return normalized?.errors ?? {};
};

export const getTraceId = (error: unknown): string | null => {
  const normalized = getNormalizedError(error);
  return normalized?.traceId ?? null;
};

export const isUnauthorizedError = (error: unknown): boolean => {
  const normalized = getNormalizedError(error);
  return normalized?.statusCode === 401;
};

export const isForbiddenError = (error: unknown): boolean => {
  const normalized = getNormalizedError(error);
  return normalized?.statusCode === 403;
};

export const isRateLimitError = (error: unknown): boolean => {
  const normalized = getNormalizedError(error);
  return normalized?.statusCode === 429;
};
