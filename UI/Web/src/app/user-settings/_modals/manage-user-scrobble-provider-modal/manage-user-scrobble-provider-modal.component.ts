import {ChangeDetectionStrategy, Component, computed, inject, model, OnInit, signal} from '@angular/core';
import {UserScrobbleProvider} from "../../../_models/kavitaplus/scrobble-providers/user-scrobble-provider";
import {ScrobbleProvider, ScrobblingService} from "../../../_services/scrobbling.service";
import {NgbActiveModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule} from "@angular/forms";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";
import {TruncatePipe} from "../../../_pipes/truncate.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {UtcToLocalDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {ToastrService} from "ngx-toastr";
import {
  ScrobbleProviderImageComponent
} from "../../../shared/_components/scrobble-provider-image/scrobble-provider-image.component";
import {TimeDifferencePipe} from "../../../_pipes/time-difference.pipe";
import {ConfirmService} from "../../../shared/confirm.service";
import {NULL_DATE} from "../../../_pipes/date-year-range.pipe";
import {AccountService} from "../../../_services/account.service";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {APP_BASE_HREF} from "@angular/common";
import {ActivatedRoute} from "@angular/router";
import {filter} from "rxjs";
import {map, tap} from "rxjs/operators";

@Component({
  selector: 'app-manage-user-scrobble-provider-modal-modal',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    DefaultValuePipe,
    ScrobbleProviderNamePipe,
    TruncatePipe,
    UtcToLocalTimePipe,
    UtcToLocalDatePipe,
    ScrobbleProviderImageComponent,
    NgbTooltip,
    TimeDifferencePipe,
    SafeUrlPipe
  ],
  templateUrl: './manage-user-scrobble-provider-modal.component.html',
  styleUrl: './manage-user-scrobble-provider-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageUserScrobbleProviderModalComponent implements OnInit {

  private readonly scrobblingService = inject(ScrobblingService);
  private readonly modal = inject(NgbActiveModal);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  userScrobbleProvider = model.required<UserScrobbleProvider>();

  generateTokenLink = computed<string | null>(() => this.userScrobbleProvider().generateTokenLink);
  canGenerateEvents = computed(() => this.userScrobbleProvider().canGenerateEvents);

  oAuthFlowLink = computed<string | null>(() => {
    if (!this.userScrobbleProvider().supportsOAuthFlow) {
      return null;
    }

    const apiKey = this.accountService.currentUserGenericApiKey();
    if (!apiKey) {
      return null;
    }

    return this.baseUrl + `api/oauth/start?upstream=${this.userScrobbleProvider().oAuthUpStream}&apiKey=${apiKey}`;
  });

  formGroup!: FormGroup<{
    userName: FormControl<string>,
    authenticationToken: FormControl<string>,
    refreshToken: FormControl<string>,
  }>;

  ngOnInit() {
    this.formGroup = this.fb.group({
      userName: this.fb.control(this.userScrobbleProvider().userName),
      authenticationToken: this.fb.control(this.userScrobbleProvider().authenticationToken),
      refreshToken: this.fb.control(this.userScrobbleProvider().refreshToken ?? ''),
    });
  }

  async generateEvents() {
    if (this.userScrobbleProvider().hasRunScrobbleEventGeneration) {
      // Alert the user they have already run this X times before
      if (!await this.confirmService.confirm(translate('toasts.confirm-rerun-backfill', {provider: this.userScrobbleProvider().provider}))) return;
    }

    this.scrobblingService.triggerScrobbleEventGeneration(this.userScrobbleProvider().provider).subscribe(_ => {
      this.toastr.info(translate('toasts.scrobble-gen-init'));
      this.close();
    });
  }

  close() {
    this.modal.close();
  }

  save() {
    this.scrobblingService.saveUserScrobbleProvider({
      provider: this.userScrobbleProvider().provider,
      ...this.formGroup.getRawValue(),
    }).subscribe(() => this.close());
  }

  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly NULL_DATE = NULL_DATE;
}
