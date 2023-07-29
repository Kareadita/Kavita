import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs';
import { SettingsService, EmailTestResult } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { NgIf, NgTemplateOutlet } from '@angular/common';
import {TranslocoModule} from "@ngneat/transloco";

@Component({
    selector: 'app-manage-email-settings',
    templateUrl: './manage-email-settings.component.html',
    styleUrls: ['./manage-email-settings.component.scss'],
    standalone: true,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, TranslocoModule]
})
export class ManageEmailSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  link = '<a href="https://github.com/Kareadita/KavitaEmail" target="_blank" rel="noopener noreferrer">Kavita Email</a>';

  constructor(private settingsService: SettingsService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('emailServiceUrl', new FormControl(this.serverSettings.emailServiceUrl, [Validators.required]));
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, []));
    });
  }

  resetForm() {
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl);
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName);
    this.settingsForm.markAsPristine();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.emailServiceUrl = this.settingsForm.get('emailServiceUrl')?.value;
    modelSettings.hostName = this.settingsForm.get('hostName')?.value;


    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success('Server settings updated');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success('Server settings updated');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetEmailServiceUrl() {
    this.settingsService.resetEmailServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings.emailServiceUrl = settings.emailServiceUrl;
      this.resetForm();
      this.toastr.success('Email Service Reset');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  testEmailServiceUrl() {
    if (this.settingsForm.get('emailServiceUrl')?.value === '') return;
    this.settingsService.testEmailServerSettings(this.settingsForm.get('emailServiceUrl')?.value).pipe(take(1)).subscribe(async (result: EmailTestResult) => {
      if (result.successful) {
        this.toastr.success('Email Service was reachable');
      } else {
        this.toastr.error('Email Service Url did not respond. ' + result.errorMessage);
      }

    }, (err: any) => {
      console.error('error: ', err);
    });

  }

}
