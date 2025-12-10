import { CanActivateFn } from '@angular/router';
import {AccountService} from "../_services/account.service";
import {inject} from "@angular/core";

export const profileGuard: CanActivateFn = (route, state) => {
  const accountService = inject(AccountService);
  return accountService.currentUserSignal()?.preferences.socialPreferences.shareProfile ?? false;
};
