export enum KavitaPlusSubscriptionState
{
  Active = 0,
  Cancelled = 1,
  Paused = 2
}

export enum KavitaPlusBillingInterval
{
  Day = 0,
  Week = 1,
  Month = 2,
  Year = 3
}

export interface LicenseInfo {
  state: KavitaPlusSubscriptionState;
  isActive: boolean;
  isCancelled: boolean;
  expirationDate: string;
  nextChargeDate: string;
  subscribedSince: string;
  productName?: string;
  /** In Cents **/
  priceAmount?: number;
  priceCurrency?: string;
  billingInterval?: KavitaPlusBillingInterval;
  hasActiveDiscount: boolean;
  isValidVersion: boolean;
  registeredEmail: string;
  totalMonthsSubbed: number;
  hasLicense: boolean;
  installId: string;
}
