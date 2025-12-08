import {ChangeDetectionStrategy, Component, computed, inject, model, OnInit, signal} from '@angular/core';
import {ApiKeyComponent} from "../api-key/api-key.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {SettingsService} from "../../admin/settings.service";
import {WikiLink} from "../../_models/wiki";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {AuthKey, AuthKeyProvider} from "../../_models/user/auth-key";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
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
    UtcToLocalDatePipe,
    DefaultDatePipe,
    ToggleVisibilityDirective,
    DatePipe,

  ],
  templateUrl: './manage-auth-keys.component.html',
  styleUrl: './manage-auth-keys.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageAuthKeysComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(NgbModal);
  private readonly clipboard = inject(Clipboard);
  private readonly toastr = inject(ToastrService);


  protected readonly opdsUrlLink = `<a href="${WikiLink.OpdsClients}" target="_blank" rel="noopener noreferrer">Wiki</a>`

  isReadOnly = this.accountService.isReadOnly;
  opdsUrl = signal<string>('');
  authKeys = signal<AuthKey[] | null>(null);

  makeUrl: (val: string) => string = (val: string) => { return this.opdsUrl(); };

  protected readonly isOpdsEnabledResource = this.settingsService.getOpdsEnabledResource();

  ngOnInit() {
    this.loadAuthKeys();
  }

  loadAuthKeys() {
    this.accountService.getAuthKeys().subscribe(authKeys => this.authKeys.set(authKeys));
    this.accountService.getOpdsUrl().subscribe(res => this.opdsUrl.set(res));
  }

  createAuthKey() {
    const ref = this.modalService.open(CreateAuthKeyComponent, DefaultModalOptions);

    ref.closed.subscribe((result: AuthKey | null) => {
      if (result === null) return;

      this.loadAuthKeys();
    });
  }

  rotate(authKey: AuthKey) {
    const ref = this.modalService.open(CreateAuthKeyComponent, DefaultModalOptions);
    ref.componentInstance.authKey.set(authKey);

    ref.closed.subscribe((result: AuthKey | null) => {
      if (result === null) return;

      this.loadAuthKeys();
    });
  }

  async delete(authKey: AuthKey) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-auth-key'))) {
      return;
    }
    this.accountService.deleteAuthKey(authKey.id).subscribe(res => {
      this.loadAuthKeys();
    })
  }

  copy(data: string) {
    this.clipboard.copy(data);
    this.toastr.success(translate('toasts.copied-to-clipboard'));
  }

  protected readonly ColumnMode = ColumnMode;
  protected readonly AuthKeyProvider = AuthKeyProvider;
}
