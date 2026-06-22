import {KavitaPlusBillingInterval} from "./license-info";

export interface KavitaPlusProductInfo {
  productName?: string;
  priceAmount: number;
  priceCurrency: string;
  billingInterval: KavitaPlusBillingInterval;
}
