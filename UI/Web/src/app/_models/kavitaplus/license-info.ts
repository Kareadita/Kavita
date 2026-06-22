export enum KavitaPlusSubscriptionState
{
  Active = 0,
  Paused = 2,
  Cancelling = 3,
  Expired = 4
}

export enum KavitaPlusBillingInterval
{
  Day = 0,
  Week = 1,
  Month = 2,
  Year = 3
}

export class LicenseInfo {
  state!: KavitaPlusSubscriptionState;
  isActive!: boolean;
  isCancelled!: boolean;
  expirationDate!: string;
  nextChargeDate!: string;
  subscribedSince!: string;
  productName?: string;
  /** In Cents */
  priceAmount?: number;
  priceCurrency?: string;
  billingInterval?: KavitaPlusBillingInterval;
  hasActiveDiscount!: boolean;
  isValidVersion!: boolean;
  registeredEmail!: string;
  totalMonthsSubbed!: number;
  hasLicense!: boolean;
  installId!: string;
  discordId!: string | null;
  discordUsername!: string | null;
  hasDiscordSet!: boolean;
  pastDue!: boolean;

  get daysRemaining(): number {
    if (!this.nextChargeDate) return 0;
    const diff = Math.ceil((new Date(this.nextChargeDate).getTime() - Date.now()) / 86_400_000);
    return Math.max(0, diff);
  }

  get daysUntilExpiry(): number {
    if (!this.expirationDate) return 0;
    return Math.max(0, Math.ceil((new Date(this.expirationDate).getTime() - Date.now()) / 86_400_000));
  }

  static from(data: Partial<LicenseInfo>): LicenseInfo {
    return Object.assign(new LicenseInfo(), data);
  }
}
