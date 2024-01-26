import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective, NgbAccordionHeader, NgbAccordionItem,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {NgForOf, NgIf, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule} from "@ngneat/transloco";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ManageAlertsComponent} from "../manage-alerts/manage-alerts.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter} from "rxjs/operators";

@Component({
    selector: 'app-manage-email-settings',
    templateUrl: './manage-email-settings.component.html',
    styleUrls: ['./manage-email-settings.component.scss'],
    standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, TranslocoModule, SafeHtmlPipe,
    ManageAlertsComponent, NgbAccordionBody, NgbAccordionButton, NgbAccordionCollapse, NgbAccordionDirective,
    NgbAccordionHeader, NgbAccordionItem, NgForOf, TitleCasePipe]
})
export class ManageEmailSettingsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, []));

      this.settingsForm.addControl('host', new FormControl(this.serverSettings.smtpConfig.host, []));
      this.settingsForm.addControl('port', new FormControl(this.serverSettings.smtpConfig.port, []));
      this.settingsForm.addControl('userName', new FormControl(this.serverSettings.smtpConfig.userName, []));
      this.settingsForm.addControl('enableSsl', new FormControl(this.serverSettings.smtpConfig.enableSsl, []));
      this.settingsForm.addControl('password', new FormControl(this.serverSettings.smtpConfig.password, []));
      this.settingsForm.addControl('senderAddress', new FormControl(this.serverSettings.smtpConfig.senderAddress, []));
      this.settingsForm.addControl('senderDisplayName', new FormControl(this.serverSettings.smtpConfig.senderDisplayName, []));
      this.settingsForm.addControl('sizeLimit', new FormControl(this.serverSettings.smtpConfig.sizeLimit, [Validators.min(1)]));
      this.settingsForm.addControl('customizedTemplates', new FormControl(this.serverSettings.smtpConfig.customizedTemplates, [Validators.min(1)]));

      this.cdRef.markForCheck();
    });
  }

  resetForm() {
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName);

    this.settingsForm.addControl('host', new FormControl(this.serverSettings.smtpConfig.host, []));
    this.settingsForm.addControl('port', new FormControl(this.serverSettings.smtpConfig.port, []));
    this.settingsForm.addControl('userName', new FormControl(this.serverSettings.smtpConfig.userName, []));
    this.settingsForm.addControl('enableSsl', new FormControl(this.serverSettings.smtpConfig.enableSsl, []));
    this.settingsForm.addControl('password', new FormControl(this.serverSettings.smtpConfig.password, []));
    this.settingsForm.addControl('senderAddress', new FormControl(this.serverSettings.smtpConfig.senderAddress, []));
    this.settingsForm.addControl('senderDisplayName', new FormControl(this.serverSettings.smtpConfig.senderDisplayName, []));
    this.settingsForm.addControl('sizeLimit', new FormControl(this.serverSettings.smtpConfig.sizeLimit, [Validators.min(1)]));
    this.settingsForm.addControl('customizedTemplates', new FormControl(this.serverSettings.smtpConfig.customizedTemplates, [Validators.min(1)]));
    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  autofillGmail() {
    this.settingsForm.get('host')?.setValue('smtp.gmail.com');
    this.settingsForm.get('port')?.setValue(587);
    this.settingsForm.get('sizeLimit')?.setValue(26214400);
    this.settingsForm.get('enableSsl')?.setValue(true);
    this.cdRef.markForCheck();
  }

  autofillOutlook() {
    this.settingsForm.get('host')?.setValue('smtp-mail.outlook.com');
    this.settingsForm.get('port')?.setValue(587 );
    this.settingsForm.get('sizeLimit')?.setValue(1048576);
    this.settingsForm.get('enableSsl')?.setValue(true);
    this.cdRef.markForCheck();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.emailServiceUrl = this.settingsForm.get('emailServiceUrl')?.value;
    modelSettings.hostName = this.settingsForm.get('hostName')?.value;

    modelSettings.smtpConfig.host = this.settingsForm.get('host')?.value;
    modelSettings.smtpConfig.port = this.settingsForm.get('port')?.value;
    modelSettings.smtpConfig.userName = this.settingsForm.get('userName')?.value;
    modelSettings.smtpConfig.enableSsl = this.settingsForm.get('enableSsl')?.value;
    modelSettings.smtpConfig.password = this.settingsForm.get('password')?.value;
    modelSettings.smtpConfig.senderAddress = this.settingsForm.get('senderAddress')?.value;
    modelSettings.smtpConfig.senderDisplayName = this.settingsForm.get('senderDisplayName')?.value;
    modelSettings.smtpConfig.sizeLimit = this.settingsForm.get('sizeLimit')?.value;
    modelSettings.smtpConfig.customizedTemplates = this.settingsForm.get('customizedTemplates')?.value;

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

  test() {
    this.settingsService.testEmailServerSettings().subscribe(res => {
      if (res.successful) {
        this.toastr.success(translate('toasts.email-sent', {email: res.emailAddress}));
      } else {
        this.toastr.error(translate('toasts.email-not-sent-test'))
      }
    });
  }
}
