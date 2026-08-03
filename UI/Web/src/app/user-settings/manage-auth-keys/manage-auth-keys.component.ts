import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {ApiKeyComponent} from "../api-key/api-key.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {SettingsService} from "../../admin/settings.service";
import {WikiLink} from "../../_models/wiki";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {AuthKey, AuthKeyProvider, KoboName, OpdsName} from "../../_models/user/auth-key";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {ToggleVisibilityDirective} from "../../_directives/toggle-visibility.directive";
import {ConfirmService} from "../../shared/confirm.service";
import {CreateAuthKeyComponent} from "../_modals/create-auth-key/create-auth-key.component";
import {Clipboard} from "@angular/cdk/clipboard";
import {DatePipe} from "@angular/common";
import {ToastrService} from "ngx-toastr";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {ModalService} from "../../_services/modal.service";
import {form, FormField} from "@angular/forms/signals";
import {FormsModule} from "@angular/forms";
import {RouterLink} from "@angular/router";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";

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
    ResponsiveTableComponent,
    FormField,
    FormsModule,
    RouterLink,
  ],
  templateUrl: './manage-auth-keys.component.html',
  styleUrl: './manage-auth-keys.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageAuthKeysComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(ModalService);
  private readonly clipboard = inject(Clipboard);
  private readonly toastr = inject(ToastrService);

  protected readonly opdsUrlLink = `<a href="${WikiLink.OpdsClients}" target="_blank" rel="noopener noreferrer">Wiki</a>`
  protected readonly removedFromKoboTab = SettingsTabId.RemovedFromKobo;

  isReadOnly = this.accountService.hasReadOnlyRole;

  opdsAuthKeyModel = signal<string>(OpdsName);
  opdsAuthKeyForm = form(this.opdsAuthKeyModel);
  opdsUrlRsc = this.accountService.opdsUrlRsc(() => this.opdsAuthKeyModel());
  koboSyncUrl = signal<string | null>(null);
  koboSyncError = signal<string | null>(null);

  authKeys = computed(() => {
    const account = this.accountService.currentUser();
    if (!account) return null;

    return account.authKeys;
  });

  hasKoboAuthKey = computed(() => {
    return this.authKeys()?.some(k => k.name === KoboName) ?? false;
  });

  trackByAuthKey = (index: number, item: AuthKey) => `${item.id}_${item.key}_${item.name}`;

  protected readonly isOpdsEnabledResource = this.settingsService.getOpdsEnabledResource();
  protected readonly isKoboEnabledResource = this.settingsService.getKoboEnabledResource();

  ngOnInit() {
    this.opdsUrlRsc.reload();
  }

  createAuthKey() {
    const ref = this.modalService.open(CreateAuthKeyComponent);

    ref.closed.subscribe((result: AuthKey | null) => {
      if (result === null) return;

      this.opdsUrlRsc.reload();
    });
  }

  rotate(authKey: AuthKey) {
    const ref = this.modalService.open(CreateAuthKeyComponent);
    ref.setInput('authKey', authKey);

    ref.closed.subscribe((result: AuthKey | null) => {
      if (result === null) return;

      this.opdsUrlRsc.reload();
      if (authKey.name === KoboName && this.koboSyncUrl()) {
        this.createOrViewKoboSyncUrl();
      }
    });
  }

  async delete(authKey: AuthKey) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-auth-key'))) {
      return;
    }
    this.accountService.deleteAuthKey(authKey.id).subscribe(res => {
      if (this.opdsAuthKeyModel() === authKey.name) {
        this.opdsAuthKeyModel.set(OpdsName);
      }

      this.opdsUrlRsc.reload();
      if (authKey.name === KoboName) {
        this.koboSyncUrl.set(null);
        this.koboSyncError.set(null);
      }
    })
  }

  createOrViewKoboSyncUrl() {
    this.koboSyncError.set(null);
    this.accountService.getKoboSyncUrl().subscribe({
      next: (url) => {
        this.koboSyncUrl.set(url);
        this.accountService.refreshAccount().subscribe();
      },
      error: (err) => {
        this.koboSyncUrl.set(null);
        this.koboSyncError.set(err?.error || translate('manage-auth-keys.clients-kobo-error'));
      }
    });
  }

  async rotateKoboSyncUrl() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-rotate-kobo-sync'))) {
      return;
    }
    this.accountService.rotateKoboSyncUrl().subscribe({
      next: (url) => {
        this.koboSyncUrl.set(url);
        this.koboSyncError.set(null);
        this.accountService.refreshAccount().subscribe();
        this.toastr.success(translate('toasts.kobo-sync-rotated'));
      },
      error: (err) => {
        this.toastr.error(err?.error || translate('errors.generic'));
      }
    });
  }

  async revokeKoboSyncUrl() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-revoke-kobo-sync'))) {
      return;
    }
    this.accountService.revokeKoboSyncUrl().subscribe({
      next: () => {
        this.koboSyncUrl.set(null);
        this.koboSyncError.set(null);
        this.accountService.refreshAccount().subscribe();
        this.toastr.success(translate('toasts.kobo-sync-revoked'));
      },
      error: (err) => {
        this.toastr.error(err?.error || translate('errors.generic'));
      }
    });
  }

  async forceFullKoboSync() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-force-full-kobo-sync'))) {
      return;
    }
    this.accountService.forceFullKoboSync().subscribe({
      next: () => {
        this.toastr.success(translate('toasts.kobo-force-full-sync'));
      },
      error: (err) => {
        this.toastr.error(err?.error || translate('errors.generic'));
      }
    });
  }

  copy(data: string) {
    this.clipboard.copy(data);
    this.toastr.success(translate('toasts.copied-to-clipboard'));
  }

  protected readonly AuthKeyProvider = AuthKeyProvider;
}
