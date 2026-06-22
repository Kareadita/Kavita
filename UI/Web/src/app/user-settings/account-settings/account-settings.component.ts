import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {
  ChangeAgeRestrictionComponent
} from "src/app/user-settings/change-age-restriction/change-age-restriction.component";
import {ChangeEmailComponent} from "src/app/user-settings/change-email/change-email.component";
import {ChangePasswordComponent} from "src/app/user-settings/change-password/change-password.component";
import {AccountService} from "src/app/_services/account.service";
import {ConfirmService} from "src/app/shared/confirm.service";
import {EMPTY, filter, from, switchMap} from "rxjs";
import {ImageComponent} from "src/app/shared/image/image.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ChangeUsernameComponent} from "../change-username/change-username.component";

@Component({
  selector: 'app-account-settings',
  imports: [
    ChangeAgeRestrictionComponent,
    ChangeEmailComponent,
    ChangePasswordComponent,
    ImageComponent,
    TranslocoDirective,
    ChangeUsernameComponent
  ],
  templateUrl: './account-settings.component.html',
  styleUrl: './account-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountSettingsComponent {

  protected readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);

  protected clearOidcLink() {
    from(this.confirmService.confirm(translate('account-settings.confirm-unlink'), {
      ...this.confirmService.defaultConfirm,
      content: translate('account-settings.confirm-unlink-extra'),
      header: translate('account-settings.confirm-unlink'),
    })).pipe(
      filter(confirmed => confirmed),
      switchMap(() => this.accountService.clearOidcLink()),
      switchMap(() => {
        if (this.accountService.currentUser()?.token) {
          return this.accountService.refreshAccount();
        }

        this.accountService.logout(false, true);
        return EMPTY;
      }),
    ).subscribe();

  }

}
