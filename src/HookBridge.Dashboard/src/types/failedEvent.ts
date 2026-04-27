export type FailedEventStatus = 'DLQ' | 'RetryRequested';

export type FailedEventResponse = {
  id: string;
  tenantId: string;
  eventId: string;
  subscriptionId: string;
  eventType: string;
  targetUrl: string;
  reason: string;
  finalAttemptNumber: number;
  lastHttpStatusCode: number | null;
  lastErrorMessage: string | null;
  status: FailedEventStatus | string;
  failedAt: string;
  correlationId: string | null;
  createdAt: string;
  updatedAt: string | null;
};

export type FailedEventSearchRequest = {
  eventId?: string;
  subscriptionId?: string;
  eventType?: string;
  status?: FailedEventStatus | '';
  fromDate?: string;
  toDate?: string;
};
