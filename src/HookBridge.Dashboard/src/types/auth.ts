export type AdminRole = 'Owner' | 'Admin' | 'Developer' | 'Viewer';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  organizationName?: string;
}

export interface AdminUser {
  id: string;
  tenantId: string;
  email: string;
  fullName: string;
  role: AdminRole;
  organizationName: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: AdminUser;
}
