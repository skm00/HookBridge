import type { PagedRequest } from './pagination';

export type BackoffType = 'Fixed' | 'Exponential';

export type AuthenticationType = 'None' | 'Basic' | 'ApiKeyHeader' | 'HmacSignature' | 'OAuth2ClientCredentials';

export type KeyValue = {
  name: string;
  value: string;
};

export type RetryPolicy = {
  maxAttempts: number;
  initialDelaySeconds: number;
  backoffType: BackoffType;
};

export type BasicAuthentication = {
  username: string;
  password: string;
};

export type ApiKeyHeaderAuthentication = {
  headerName: string;
  headerValue: string;
};

export type HmacSignatureAuthentication = {
  secret: string;
  headerName: string;
  algorithm: string;
};

export type OAuth2ClientCredentialsAuthentication = {
  tokenUrl: string;
  clientId: string;
  clientSecret: string;
  scope?: string;
};

export type Authentication = {
  type: AuthenticationType;
  basic?: BasicAuthentication;
  apiKeyHeader?: ApiKeyHeaderAuthentication;
  hmacSignature?: HmacSignatureAuthentication;
  oauth2?: OAuth2ClientCredentialsAuthentication;
};

export type Subscription = {
  id: string;
  eventType: string;
  targetUrl: string;
  headers: KeyValue[];
  authentication?: Authentication;
  retryPolicy: RetryPolicy;
  timeoutSeconds: number;
  deliveryFormat: 'Raw' | 'HookBridgeEnvelope' | 'CloudEventsStructured' | 'CloudEventsBinary';
  isActive: boolean;
  disabledAt?: string;
  createdAt: string;
  updatedAt?: string;
};

export type SubscriptionListFilters = PagedRequest & {
  eventType?: string;
  targetUrl?: string;
  isActive?: boolean;
};

export type CreateSubscriptionRequest = {
  eventType?: string;
  deliveryFormat?: 'Raw' | 'HookBridgeEnvelope' | 'CloudEventsStructured' | 'CloudEventsBinary';
  targetUrl: string;
  headers: KeyValue[];
  authentication?: Authentication;
  retryPolicy: RetryPolicy;
  timeoutSeconds: number;
};

export type UpdateSubscriptionRequest = {
  eventType?: string;
  deliveryFormat?: 'Raw' | 'HookBridgeEnvelope' | 'CloudEventsStructured' | 'CloudEventsBinary';
  targetUrl: string;
  headers: KeyValue[];
  authentication?: Authentication;
  retryPolicy: RetryPolicy;
  timeoutSeconds: number;
};

export type EndpointValidationRequest = {
  targetUrl: string;
  method?: string;
  samplePayload?: Record<string, unknown>;
  headers?: KeyValue[];
  authentication?: Authentication;
  timeoutSeconds: number;
};

export type EndpointValidationResponse = {
  isSuccess: boolean;
  statusCode?: number;
  message: string;
  durationMs: number;
  responseBody?: string;
};
