import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs/operators';
import {ServerService} from 'src/app/_services/server.service';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgFor, NgIf, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@ngneat/transloco";

const ValidIpAddress = /^(\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*\,)*\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*$/i;
//const ValidIpAddressWithRangeAndComma = /^(\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*\,)*\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*$/i;

@Component({
  selector: 'app-manage-settings',
  templateUrl: './manage-settings.component.html',
  styleUrls: ['./manage-settings.component.scss'],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgFor, TitleCasePipe, TranslocoModule, NgTemplateOutlet]
})
export class ManageSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];
  private readonly translocoService = inject(TranslocoService);
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor(private settingsService: SettingsService, private toastr: ToastrService,
    private serverService: ServerService) { }

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
      this.settingsForm.addControl('ipAddresses', new FormControl(this.serverSettings.ipAddresses, [Validators.pattern(ValidIpAddress)]));
      this.settingsForm.addControl('port', new FormControl(this.serverSettings.port, [Validators.required]));
      this.settingsForm.addControl('loggingLevel', new FormControl(this.serverSettings.loggingLevel, [Validators.required]));
      this.settingsForm.addControl('allowStatCollection', new FormControl(this.serverSettings.allowStatCollection, [Validators.required]));
      this.settingsForm.addControl('enableOpds', new FormControl(this.serverSettings.enableOpds, [Validators.required]));
      this.settingsForm.addControl('baseUrl', new FormControl(this.serverSettings.baseUrl, [Validators.pattern(/^(\/[\w-]+)*\/$/)]));
      this.settingsForm.addControl('emailServiceUrl', new FormControl(this.serverSettings.emailServiceUrl, [Validators.required]));
      this.settingsForm.addControl('totalBackups', new FormControl(this.serverSettings.totalBackups, [Validators.required, Validators.min(1), Validators.max(30)]));
      this.settingsForm.addControl('cacheSize', new FormControl(this.serverSettings.cacheSize, [Validators.required, Validators.min(50)]));
      this.settingsForm.addControl('totalLogs', new FormControl(this.serverSettings.totalLogs, [Validators.required, Validators.min(1), Validators.max(30)]));
      this.settingsForm.addControl('enableFolderWatching', new FormControl(this.serverSettings.enableFolderWatching, [Validators.required]));
      this.settingsForm.addControl('encodeMediaAs', new FormControl(this.serverSettings.encodeMediaAs, []));
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, [Validators.pattern(/^(http:|https:)+[^\s]+[\w]$/)]));
      this.settingsForm.addControl('onDeckProgressDays', new FormControl(this.serverSettings.onDeckProgressDays, [Validators.required]));
      this.settingsForm.addControl('onDeckUpdateDays', new FormControl(this.serverSettings.onDeckUpdateDays, [Validators.required]));
      this.settingsForm.addControl('customHeaderWhitelistIpRanges', new FormControl(this.serverSettings.customHeaderWhitelistIpRanges, [Validators.required]));

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
    this.settingsForm.get('cacheDirectory')?.setValue(this.serverSettings.cacheDirectory);
    this.settingsForm.get('scanTask')?.setValue(this.serverSettings.taskScan);
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup);
    this.settingsForm.get('ipAddresses')?.setValue(this.serverSettings.ipAddresses);
    this.settingsForm.get('port')?.setValue(this.serverSettings.port);
    this.settingsForm.get('loggingLevel')?.setValue(this.serverSettings.loggingLevel);
    this.settingsForm.get('allowStatCollection')?.setValue(this.serverSettings.allowStatCollection);
    this.settingsForm.get('enableOpds')?.setValue(this.serverSettings.enableOpds);
    this.settingsForm.get('baseUrl')?.setValue(this.serverSettings.baseUrl);
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl);
    this.settingsForm.get('totalBackups')?.setValue(this.serverSettings.totalBackups);
    this.settingsForm.get('totalLogs')?.setValue(this.serverSettings.totalLogs);
    this.settingsForm.get('enableFolderWatching')?.setValue(this.serverSettings.enableFolderWatching);
    this.settingsForm.get('encodeMediaAs')?.setValue(this.serverSettings.encodeMediaAs);
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName);
    this.settingsForm.get('cacheSize')?.setValue(this.serverSettings.cacheSize);
    this.settingsForm.get('onDeckProgressDays')?.setValue(this.serverSettings.onDeckProgressDays);
    this.settingsForm.get('onDeckUpdateDays')?.setValue(this.serverSettings.onDeckUpdateDays);
    this.settingsForm.get('customHeaderWhitelistIpRanges')?.setValue(this.serverSettings.customHeaderWhitelistIpRanges);
    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  async saveSettings() {
    const modelSettings = this.settingsForm.value;
    modelSettings.bookmarksDirectory = this.serverSettings.bookmarksDirectory;
    this.settingsService.updateServerSettings(modelSettings).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetIPAddresses() {
    this.settingsService.resetIPAddressesSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings.ipAddresses = settings.ipAddresses;
      this.settingsForm.get('ipAddresses')?.setValue(this.serverSettings.ipAddresses);
      this.toastr.success(translate('toasts.reset-ip-address'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetBaseUrl() {
    this.settingsService.resetBaseUrl().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings.baseUrl = settings.baseUrl;
      this.settingsForm.get('baseUrl')?.setValue(this.serverSettings.baseUrl);
      this.toastr.success(translate('toasts.reset-base-url'));
      this.cdRef.markForCheck();
    }, (err: any) => {
      console.error('error: ', err);
    });
  }
}
