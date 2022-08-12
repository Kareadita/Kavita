import { Component, OnInit } from '@angular/core';
import { UntypedFormControl, UntypedFormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs';
import { SettingsService, EmailTestResult } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';

@Component({
  selector: 'app-manage-email-settings',
  templateUrl: './manage-email-settings.component.html',
  styleUrls: ['./manage-email-settings.component.scss']
})
export class ManageEmailSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: UntypedFormGroup = new UntypedFormGroup({});
  
  constructor(private settingsService: SettingsService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('emailServiceUrl', new UntypedFormControl(this.serverSettings.emailServiceUrl, [Validators.required]));
    });
  }

  resetForm() {
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl);
    this.settingsForm.markAsPristine();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.emailServiceUrl = this.settingsForm.get('emailServiceUrl')?.value;

    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success('Server settings updated');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success('Server settings updated');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetEmailServiceUrl() {
    this.settingsService.resetEmailServerSettings().pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings.emailServiceUrl = settings.emailServiceUrl;
      this.resetForm();
      this.toastr.success('Email Service Reset');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  testEmailServiceUrl() {
    this.settingsService.testEmailServerSettings(this.settingsForm.get('emailServiceUrl')?.value || '').pipe(take(1)).subscribe(async (result: EmailTestResult) => {
      if (result.successful) {
        this.toastr.success('Email Service Url validated');
      } else {
        this.toastr.error('Email Service Url did not respond. ' + result.errorMessage);
      }
      
    }, (err: any) => {
      console.error('error: ', err);
    });
    
  }

}
