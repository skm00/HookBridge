export type DeliveryAttemptStatus = 'Pending' | 'Success' | 'Failed';

export type DeliveryAttemptResponse = {
  id: string;
  tenantId: string;
  eventId: string;
  subscriptionId: string;
  eventType: string;
  targetUrl: string;
  attemptNumber: number;
  status: DeliveryAttemptStatus | number | string;
  httpStatusCode: number | null;
  responseBody: string | null;
  errorMessage: string | null;
  durationMs: number;
  attemptedAt: string;
  correlationId: string | null;
  createdAt: string;
  updatedAt: string | null;
};

export type DeliveryAttemptSearchRequest = {
  eventId?: string;
  subscriptionId?: string;
  eventType?: string;
  status?: DeliveryAttemptStatus | '';
  httpStatusCode?: number;
  fromDate?: string;
  toDate?: string;
  targetUrl?: string;
};
