import {KavitaPlusRegistrationErrorCode} from "./kavita-plus-registration-error-code";

export interface KavitaPlusRegisterResult {
  success: boolean;
  errorCode?: KavitaPlusRegistrationErrorCode;
  isSubscriptionActive: boolean;
}
