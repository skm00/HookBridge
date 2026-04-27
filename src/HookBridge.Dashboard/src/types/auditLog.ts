import type { PagedRequest } from './pagination';

export type AuditLogResponse = {
  id: string;
  tenantId: string;
  userId: string | null;
  userEmail: string | null;
  action: string;
  resourceType: string;
  resourceId: string | null;
  description: string | null;
  metadata: unknown;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
};

export type AuditLogSearchRequest = PagedRequest & {
  userId?: string;
  userEmail?: string;
  action?: string;
  resourceType?: string;
  resourceId?: string;
  fromDate?: string;
  toDate?: string;
};
