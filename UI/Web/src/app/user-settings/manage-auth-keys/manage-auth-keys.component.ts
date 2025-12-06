import {ChangeDetectionStrategy, Component, computed, inject, model} from '@angular/core';
import {ApiKeyComponent} from "../api-key/api-key.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {SettingsService} from "../../admin/settings.service";
import {WikiLink} from "../../_models/wiki";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {AuthKey, AuthKeyProvider} from "../../_models/user/auth-key";
import {UtcToLocaleDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {ToggleVisibilityDirective} from "../../_directives/toggle-visibility.directive";
import {ConfirmService} from "../../shared/confirm.service";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {CreateAuthKeyComponent} from "../_modals/create-auth-key/create-auth-key.component";
import {Clipboard} from "@angular/cdk/clipboard";
import {DatePipe} from "@angular/common";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-manage-auth-keys',
  imports: [
    ApiKeyComponent,
    TranslocoDirective,
    NgxDatatableModule,
    UtcToLocaleDatePipe,
    DefaultDatePipe,
    ToggleVisibilityDirective,
    DatePipe,

  ],
  templateUrl: './manage-auth-keys.component.html',
  styleUrl: './manage-auth-keys.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageAuthKeysComponent {
  private readonly accountService = inject(AccountService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(NgbModal);
  private readonly clipboard = inject(Clipboard);
  private readonly toastr = inject(ToastrService);


  protected readonly opdsUrlLink = `<a href="${WikiLink.OpdsClients}" target="_blank" rel="noopener noreferrer">Wiki</a>`

  opdsUrl = model<string>('');
  makeUrl: (val: string) => string = (val: string) => { return this.opdsUrl(); };
  isReadOnly = computed(() => this.accountService.hasReadOnlyRole(this.accountService.currentUserSignal()!));

  protected readonly authKeysResource = this.accountService.getAuthKeysResource();
  protected readonly isOpdsEnabledResource = this.settingsService.getOpdsEnabledResource();

  constructor() {
    this.accountService.getOpdsUrl().subscribe(res => {
      this.opdsUrl.set(res);
    });
  }

  createAuthKey() {
    const ref = this.modalService.open(CreateAuthKeyComponent, DefaultModalOptions);

    ref.closed.subscribe((result: AuthKey | null) => {
      if (result === null) return;
      this.authKeysResource.reload();
    });
  }

  rotate(authKey: AuthKey) {
    const ref = this.modalService.open(CreateAuthKeyComponent, DefaultModalOptions);
    ref.componentInstance.authKey.set(authKey);

    ref.closed.subscribe((result: AuthKey | null) => {
      if (result === null) return;
      this.authKeysResource.reload();
    });
  }

  async delete(authKey: AuthKey) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-auth-key'))) {
      return;
    }
    this.accountService.deleteAuthKey(authKey.id).subscribe(res => {
      this.authKeysResource.reload();
    })
  }

  copy(data: string) {
    this.clipboard.copy(data);
    this.toastr.success(translate('toasts.copied-to-clipboard'));
  }

  protected readonly ColumnMode = ColumnMode;
  protected readonly AuthKeyProvider = AuthKeyProvider;
}
