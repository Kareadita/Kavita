import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {forkJoin, take} from 'rxjs';
import {EmailTestResult, SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgIf, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@ngneat/transloco";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ServerService} from "../../_services/server.service";

@Component({
    selector: 'app-manage-email-settings',
    templateUrl: './manage-email-settings.component.html',
    styleUrls: ['./manage-email-settings.component.scss'],
    standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, TranslocoModule, SafeHtmlPipe]
})
export class ManageEmailSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  link = '<a href="https://github.com/Kareadita/KavitaEmail" target="_blank" rel="noopener noreferrer">Kavita Email</a>';
  emailVersion: string | null = null;
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly serverService = inject(ServerService);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);

  constructor() { }

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('emailServiceUrl', new FormControl(this.serverSettings.emailServiceUrl, [Validators.required]));
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, []));
      this.cdRef.markForCheck();
    });

    this.serverService.getEmailVersion().subscribe(version => {
      this.emailVersion = version;
      this.cdRef.markForCheck();
    });
  }

  resetForm() {
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl);
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName);
    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.emailServiceUrl = this.settingsForm.get('emailServiceUrl')?.value;
    modelSettings.hostName = this.settingsForm.get('hostName')?.value;


    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe((settings: ServerSettings) => {
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

  resetEmailServiceUrl() {
    this.settingsService.resetEmailServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings.emailServiceUrl = settings.emailServiceUrl;
      this.resetForm();
      this.toastr.success(translate('toasts.email-service-reset'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  testEmailServiceUrl() {
    if (this.settingsForm.get('emailServiceUrl')?.value === '') return;
    forkJoin([this.settingsService.testEmailServerSettings(this.settingsForm.get('emailServiceUrl')?.value), this.serverService.getEmailVersion()])
        .pipe(take(1)).subscribe(async (results) => {
          const result = results[0] as EmailTestResult;
      if (result.successful) {
        const version = ('. Kavita Email: ' + results[1] ? 'v' + results[1] : '');
        this.toastr.success(translate('toasts.email-service-reachable') + version);
      } else {
        this.toastr.error(translate('toasts.email-service-unresponsive') + result.errorMessage.split('(')[0]);
      }

    }, (err: any) => {
      console.error('error: ', err);
    });

  }

}
