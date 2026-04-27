import type { PagedRequest } from './paging';

export type IncomingEventStatus = 'Accepted' | 'Delivered' | 'Failed' | 'PartiallyFailed' | 'NoSubscriptions';

export type IncomingEventResponse = {
  id: string;
  tenantId: string;
  eventId: string;
  eventType: string;
  sourceTimestamp: string | null;
  status: IncomingEventStatus | string;
  receivedAt: string;
  apiKeyId: string | null;
  correlationId: string | null;
  payload: unknown;
};

export type IncomingEventSearchRequest = PagedRequest & {
  eventId?: string;
  eventType?: string;
  status?: IncomingEventStatus | '';
  fromDate?: string;
  toDate?: string;
  correlationId?: string;
};
