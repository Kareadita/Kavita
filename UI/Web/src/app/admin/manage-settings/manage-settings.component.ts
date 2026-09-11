import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {ToastrService} from '@openng/ngx-toastr';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {translate, TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {ConfirmService} from "../../shared/confirm.service";
import {catchError, debounceTime, distinctUntilChanged, EMPTY, filter, of, switchMap, tap} from "rxjs";
import {toObservable} from "@angular/core/rxjs-interop";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {EnterBlurDirective} from "../../_directives/enter-blur.directive";
import {LogLevelPipe} from "../../_pipes/log-level.pipe";
import {ServerService} from "../../_services/server.service";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {disabled, form, FormField, max, min, pattern, required} from "@angular/forms/signals";
import {emptyOrPattern} from "../../_validators/empty-or-pattern.validator";
import {url} from "../../_validators/url.validator";

const ValidIpAddress = /^(\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*\,)*\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*$/i;

interface FormModel {
  cacheDirectory: string;
  taskScan: string;
  taskBackup: string;
  taskCleanup: string;
  ipAddresses: string;
  port: number;
  loggingLevel: string;
  allowStatCollection: boolean;
  enableOpds: boolean;
  baseUrl: string;
  totalBackups: number;
  cacheSize: number;
  totalLogs: number;
  enableFolderWatching: boolean;
  hostName: string;
  onDeckProgressDays: number;
  onDeckUpdateDays: number;
}

@Component({
    selector: 'app-manage-settings',
    templateUrl: './manage-settings.component.html',
    styleUrls: ['./manage-settings.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoModule, SettingItemComponent, SettingSwitchComponent, DefaultValuePipe, EnterBlurDirective, LogLevelPipe, ValidationErrorsComponent, FormFieldDirective, FormField]
})
export class ManageSettingsComponent implements OnInit {

  private readonly translocoService = inject(TranslocoService);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly serverService = inject(ServerService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly WikiLink = WikiLink;

  formModel = signal<FormModel>({
    allowStatCollection: false,
    baseUrl: "",
    cacheDirectory: "",
    cacheSize: 0,
    enableFolderWatching: false,
    enableOpds: false,
    hostName: "",
    ipAddresses: "",
    loggingLevel: "information",
    onDeckProgressDays: 0,
    onDeckUpdateDays: 0,
    port: 0,
    taskBackup: "",
    taskCleanup: "",
    taskScan: "",
    totalBackups: 0,
    totalLogs: 0

  });
  formGroup = form(this.formModel, path => {
    required(path.cacheDirectory);
    required(path.taskScan);
    required(path.taskBackup);
    required(path.taskCleanup);
    emptyOrPattern(path.ipAddresses, ValidIpAddress);
    disabled(path.ipAddresses, { when: () => this.isDocker()});
    required(path.port);
    disabled(path.port, { when: () => this.isDocker()});
    required(path.loggingLevel);
    pattern(path.baseUrl, /^(\/[\w-]+)*\/$/);
    required(path.totalBackups);
    min(path.totalBackups, 1);
    max(path.totalBackups, 30);
    required(path.cacheSize);
    min(path.cacheSize, 50);
    required(path.totalLogs);
    min(path.totalLogs, 1);
    max(path.totalLogs, 30);
    url(path.hostName, { requireTls: false });
  });

  serverSettings = signal<ServerSettings | null>(null);

  taskFrequencies = signal<string[]>([]);
  logLevels = signal<string[]>([]);
  isDocker = signal<boolean>(false);

  allowStatsTooltip = translate('manage-settings.allow-stats-tooltip-part-1') + ' <a href="' +
    WikiLink.DataCollection +
    '" rel="noopener noreferrer" target="_blank">wiki</a> ' +
    translate('manage-settings.allow-stats-tooltip-part-2');

  constructor() {
    toObservable(this.formModel).pipe(
      distinctUntilChanged(),
      debounceTime(300),
      filter(() => this.formGroup().valid()),
      switchMap(_ => {
        const serverSettings = this.serverSettings();
        if (serverSettings == null) {
          return EMPTY;
        }

        const data = {
          ...serverSettings,
          ...this.formModel()
        }
        return this.settingsService.updateServerSettings(data).pipe(catchError(err => {
          console.error(err);
          return of(null);
        }));
      }),
    ).subscribe();
  }

  ngOnInit(): void {
    this.settingsService.getTaskFrequencies().subscribe(frequencies => {
      this.taskFrequencies.set(frequencies);
    });
    this.settingsService.getLoggingLevels().subscribe(levels => {
      this.logLevels.set(levels);
    });
    this.settingsService.getServerSettings().subscribe((settings: ServerSettings) => {
      this.serverSettings.set(settings);
      this.formModel.set(settings);
    });
    this.serverService.getServerInfo().subscribe(info => {
      this.isDocker.set(info.isDocker);
    });
  }

  resetForm() {
    const serverSettings = this.serverSettings();
    if (serverSettings != null) {
      this.formModel.set(serverSettings);
    }
  }

  async resetToDefaults() {
    this.confirmService.confirm$(translate('toasts.confirm-reset-server-settings')).pipe(
      filter(b => b),
      switchMap(() => this.settingsService.resetServerSettings()),
      tap(res => {
        this.serverSettings.set(res);
        this.resetForm();
        this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
      }),
    ).subscribe();
  }

  resetIPAddresses() {
    const serverSettings = this.serverSettings();
    if (serverSettings == null) return;

    this.settingsService.resetIPAddressesSettings().pipe(
      tap(settings => {
        this.serverSettings.set({
          ...serverSettings,
          ipAddresses: settings.ipAddresses
        });
        this.formGroup.ipAddresses().value.set(settings.ipAddresses);
        this.toastr.success(this.translocoService.translate('toasts.reset-ip-address'));
      })
    ).subscribe();
  }

  resetBaseUrl() {
    const serverSettings = this.serverSettings();
    if (serverSettings == null) return;

    this.settingsService.resetBaseUrl().pipe(
      tap(settings => {
        this.serverSettings.set({
          ...serverSettings,
          baseUrl: settings.baseUrl
        });
        this.formGroup.ipAddresses().value.set(settings.baseUrl);
        this.toastr.success(this.translocoService.translate('toasts.reset-base-url'));
      })
    ).subscribe();
  }

}
