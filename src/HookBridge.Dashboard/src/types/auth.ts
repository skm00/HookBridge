export type AdminRole = 'Owner' | 'Admin' | 'Developer' | 'Viewer';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  tenantId: string;
  fullName: string;
  email: string;
  password: string;
  role: AdminRole;
}

export interface AdminUser {
  id: string;
  tenantId: string;
  email: string;
  fullName: string;
  role: AdminRole;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: AdminUser;
}
