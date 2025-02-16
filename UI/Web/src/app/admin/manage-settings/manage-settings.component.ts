import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs/operators';
import {ServerService} from 'src/app/_services/server.service';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {ConfirmService} from "../../shared/confirm.service";
import {debounceTime, distinctUntilChanged, filter, of, switchMap, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

const ValidIpAddress = /^(\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*\,)*\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*$/i;

@Component({
  selector: 'app-manage-settings',
  templateUrl: './manage-settings.component.html',
  styleUrls: ['./manage-settings.component.scss'],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TitleCasePipe, TranslocoModule, SettingItemComponent, SettingSwitchComponent, DefaultValuePipe]
})
export class ManageSettingsComponent implements OnInit {

  private readonly translocoService = inject(TranslocoService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly serverService = inject(ServerService);
  private readonly confirmService = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly WikiLink = WikiLink;

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  allowStatsTooltip = translate('manage-settings.allow-stats-tooltip-part-1') + ' <a href="' +
    WikiLink.DataCollection +
    '" rel="noopener noreferrer" target="_blank">wiki</a> ' +
    translate('manage-settings.allow-stats-tooltip-part-2');

  ngOnInit(): void {
    this.settingsService.getTaskFrequencies().pipe(take(1)).subscribe(frequencies => {
      this.taskFrequencies = frequencies;
      this.cdRef.markForCheck();
    });
    this.settingsService.getLoggingLevels().pipe(take(1)).subscribe(levels => {
      this.logLevels = levels;
      this.cdRef.markForCheck();
    });
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('cacheDirectory', new FormControl(this.serverSettings.cacheDirectory, [Validators.required]));
      this.settingsForm.addControl('taskScan', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
      this.settingsForm.addControl('taskCleanup', new FormControl(this.serverSettings.taskCleanup, [Validators.required]));
      this.settingsForm.addControl('ipAddresses', new FormControl(this.serverSettings.ipAddresses, [Validators.required, Validators.pattern(ValidIpAddress)]));
      this.settingsForm.addControl('port', new FormControl(this.serverSettings.port, [Validators.required]));
      this.settingsForm.addControl('loggingLevel', new FormControl(this.serverSettings.loggingLevel, [Validators.required]));
      this.settingsForm.addControl('allowStatCollection', new FormControl(this.serverSettings.allowStatCollection, [Validators.required]));
      this.settingsForm.addControl('enableOpds', new FormControl(this.serverSettings.enableOpds, [Validators.required]));
      this.settingsForm.addControl('baseUrl', new FormControl(this.serverSettings.baseUrl, [Validators.pattern(/^(\/[\w-]+)*\/$/)]));
      this.settingsForm.addControl('totalBackups', new FormControl(this.serverSettings.totalBackups, [Validators.required, Validators.min(1), Validators.max(30)]));
      this.settingsForm.addControl('cacheSize', new FormControl(this.serverSettings.cacheSize, [Validators.required, Validators.min(50)]));
      this.settingsForm.addControl('totalLogs', new FormControl(this.serverSettings.totalLogs, [Validators.required, Validators.min(1), Validators.max(30)]));
      this.settingsForm.addControl('enableFolderWatching', new FormControl(this.serverSettings.enableFolderWatching, [Validators.required]));
      this.settingsForm.addControl('encodeMediaAs', new FormControl(this.serverSettings.encodeMediaAs, []));
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, [Validators.pattern(/^(http:|https:)+[^\s]+[\w]$/)]));
      this.settingsForm.addControl('onDeckProgressDays', new FormControl(this.serverSettings.onDeckProgressDays, [Validators.required]));
      this.settingsForm.addControl('onDeckUpdateDays', new FormControl(this.serverSettings.onDeckUpdateDays, [Validators.required]));

      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(300),
        filter(_ => this.settingsForm.valid),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packData();
          return this.settingsService.updateServerSettings(data);
        }),
        tap(settings => {
          this.serverSettings = settings;
          this.resetForm();
          this.cdRef.markForCheck();
        })
      ).subscribe();

      this.serverService.getServerInfo().subscribe(info => {
        if (info.isDocker) {
          this.settingsForm.get('ipAddresses')?.disable();
          this.settingsForm.get('port')?.disable();
          this.cdRef.markForCheck();
        }
      });
      this.cdRef.markForCheck();
    });
    this.cdRef.markForCheck();
  }

  resetForm() {
    this.settingsForm.get('cacheDirectory')?.setValue(this.serverSettings.cacheDirectory, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('scanTask')?.setValue(this.serverSettings.taskScan, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('taskCleanup')?.setValue(this.serverSettings.taskCleanup, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('ipAddresses')?.setValue(this.serverSettings.ipAddresses, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('port')?.setValue(this.serverSettings.port, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('loggingLevel')?.setValue(this.serverSettings.loggingLevel, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('allowStatCollection')?.setValue(this.serverSettings.allowStatCollection, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('enableOpds')?.setValue(this.serverSettings.enableOpds, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('baseUrl')?.setValue(this.serverSettings.baseUrl, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('totalBackups')?.setValue(this.serverSettings.totalBackups, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('totalLogs')?.setValue(this.serverSettings.totalLogs, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('enableFolderWatching')?.setValue(this.serverSettings.enableFolderWatching, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('encodeMediaAs')?.setValue(this.serverSettings.encodeMediaAs, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('cacheSize')?.setValue(this.serverSettings.cacheSize, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('onDeckProgressDays')?.setValue(this.serverSettings.onDeckProgressDays, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('onDeckUpdateDays')?.setValue(this.serverSettings.onDeckUpdateDays, {onlySelf: true, emitEvent: false});
    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  packData() {
    const modelSettings = this.settingsForm.value;
    modelSettings.bookmarksDirectory = this.serverSettings.bookmarksDirectory;
    modelSettings.smtpConfig = this.serverSettings.smtpConfig;

    return modelSettings;
  }

  async resetToDefaults() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-reset-server-settings'))) return;

    this.settingsService.resetServerSettings().subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetIPAddresses() {
    this.settingsService.resetIPAddressesSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings.ipAddresses = settings.ipAddresses;
      this.settingsForm.get('ipAddresses')?.setValue(this.serverSettings.ipAddresses);
      this.toastr.success(this.translocoService.translate('toasts.reset-ip-address'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetBaseUrl() {
    this.settingsService.resetBaseUrl().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings.baseUrl = settings.baseUrl;
      this.settingsForm.get('baseUrl')?.setValue(this.serverSettings.baseUrl);
      this.toastr.success(this.translocoService.translate('toasts.reset-base-url'));
      this.cdRef.markForCheck();
    }, (err: any) => {
      console.error('error: ', err);
    });
  }
}
