import axios from 'axios';
import { apiClient } from './apiClient';
import type { AuthResponse, LoginRequest, RegisterRequest, AdminRole } from '../types/auth';

const roleByNumber: Record<number, AdminRole> = {
  0: 'Owner',
  1: 'Admin',
  2: 'Developer',
  3: 'Viewer'
};

const normalizeRole = (role: unknown): AdminRole => {
  if (typeof role === 'string' && ['Owner', 'Admin', 'Developer', 'Viewer'].includes(role)) {
    return role as AdminRole;
  }

  if (typeof role === 'number' && roleByNumber[role]) {
    return roleByNumber[role];
  }

  return 'Viewer';
};

const toAuthResponse = (payload: Record<string, unknown>): AuthResponse => {
  const userPayload = (payload.user as Record<string, unknown> | undefined) ?? {};

  return {
    token: String(payload.token ?? payload.accessToken ?? ''),
    expiresAt: String(payload.expiresAt ?? payload.expiresAtUtc ?? ''),
    user: {
      id: String(userPayload.id ?? ''),
      tenantId: String(userPayload.tenantId ?? ''),
      email: String(userPayload.email ?? ''),
      fullName: String(userPayload.fullName ?? ''),
      role: normalizeRole(userPayload.role)
    }
  };
};

const extractErrorMessage = (error: unknown): string => {
  if (!axios.isAxiosError(error)) {
    return 'Authentication failed. Please try again.';
  }

  const responseData = error.response?.data;

  if (typeof responseData?.message === 'string' && responseData.message.trim()) {
    return responseData.message;
  }

  if (Array.isArray(responseData?.errors) && responseData.errors.length > 0) {
    const firstError = responseData.errors[0];
    if (typeof firstError?.errorMessage === 'string' && firstError.errorMessage.trim()) {
      return firstError.errorMessage;
    }
  }

  return 'Authentication failed. Please try again.';
};

const login = async (request: LoginRequest): Promise<AuthResponse> => {
  try {
    const response = await apiClient.post('/api/v1/auth/login', request);
    return toAuthResponse(response.data as Record<string, unknown>);
  } catch (error) {
    throw new Error(extractErrorMessage(error));
  }
};

const roleToNumber: Record<AdminRole, number> = {
  Owner: 0,
  Admin: 1,
  Developer: 2,
  Viewer: 3
};

const register = async (request: RegisterRequest): Promise<AuthResponse> => {
  try {
    const response = await apiClient.post('/api/v1/auth/register', {
      ...request,
      role: roleToNumber[request.role]
    });
    return toAuthResponse(response.data as Record<string, unknown>);
  } catch (error) {
    throw new Error(extractErrorMessage(error));
  }
};

export const authApi = {
  login,
  register
};
