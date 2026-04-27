export type DashboardOverviewResponse = {
  tenantId: string;
  tenantName: string;
  plan: string;
  monthlyEventLimit: number;
  eventsReceivedThisMonth: number;
  eventsDeliveredThisMonth: number;
  eventsFailedThisMonth: number;
  totalDeliveryAttemptsThisMonth: number;
  successfulDeliveryAttemptsThisMonth: number;
  failedDeliveryAttemptsThisMonth: number;
  failedEventsInDlq: number;
  successRate: number;
  fromDate: string;
  toDate: string;
};
