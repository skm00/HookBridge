export type BillingPlan = 'Free' | 'Starter' | 'Pro' | 'Enterprise';

export type BillingStatusResponse = {
  tenantId: string;
  plan: BillingPlan;
  monthlyEventLimit: number;
  billingStatus: 'Free' | 'Active' | 'PaymentFailed' | 'Canceled' | string;
  stripeCustomerId?: string | null;
  stripeSubscriptionId?: string | null;
  currentPeriodStart?: string | null;
  currentPeriodEnd?: string | null;
};

export type CreateCheckoutSessionRequest = {
  plan: Exclude<BillingPlan, 'Free'>;
};

export type CheckoutSessionResponse = {
  sessionId: string;
  checkoutUrl: string;
};
