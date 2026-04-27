const TOKEN_KEY = 'hookbridge_dashboard_token';

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

  clearToken(): void {
    localStorage.removeItem(TOKEN_KEY);
  },

  isAuthenticated(): boolean {
    return Boolean(this.getToken());
  }
};
