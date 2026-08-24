import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {EMPTY, filter, from, switchMap} from "rxjs";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ChangeUsernameComponent} from "../change-username/change-username.component";
import {ChangeAgeRestrictionComponent} from "../change-age-restriction/change-age-restriction.component";
import {ChangeEmailComponent} from "../change-email/change-email.component";
import {ChangePasswordComponent} from "../change-password/change-password.component";
import {ImageComponent} from "../../shared/image/image.component";
import {AccountService} from "../../_services/account.service";
import {ConfirmService} from "../../shared/confirm.service";

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
