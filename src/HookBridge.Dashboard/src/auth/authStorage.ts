const TOKEN_KEY = 'hookbridge_dashboard_token';

type UserProfile = {
  email: string | null;
  role: string | null;
};

const parseJwtPayload = (token: string): Record<string, unknown> | null => {
  const parts = token.split('.');

  if (parts.length < 2) {
    return null;
  }

  try {
    const base64Payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const paddedPayload = base64Payload.padEnd(Math.ceil(base64Payload.length / 4) * 4, '=');
    const decodedPayload = atob(paddedPayload);
    return JSON.parse(decodedPayload) as Record<string, unknown>;
  } catch {
    return null;
  }
};

const readClaim = (payload: Record<string, unknown> | null, keys: string[]): string | null => {
  if (!payload) {
    return null;
  }

  for (const key of keys) {
    const value = payload[key];
    if (typeof value === 'string' && value.trim().length > 0) {
      return value;
    }
  }

  return null;
};

export const authStorage = {
  setToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
  },

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  },

  getTenantId(): string | null {
    const token = this.getToken();

    if (!token) {
      return null;
    }

    const payload = parseJwtPayload(token);
    const tenantId = payload?.tenantId;

    return typeof tenantId === 'string' && tenantId.trim().length > 0 ? tenantId : null;
  },

  getUserProfile(): UserProfile {
    const token = this.getToken();

    if (!token) {
      return { email: null, role: null };
    }

    const payload = parseJwtPayload(token);

    return {
      email: readClaim(payload, ['email', 'unique_name', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress']),
      role: readClaim(payload, ['role', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'])
    };
  },

  clearToken(): void {
    localStorage.removeItem(TOKEN_KEY);
  },

  isAuthenticated(): boolean {
    return Boolean(this.getToken());
  }
};
