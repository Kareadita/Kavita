import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs';
import { SettingsService } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';

@Component({
  selector: 'app-manage-media-settings',
  templateUrl: './manage-media-settings.component.html',
  styleUrls: ['./manage-media-settings.component.scss']
})
export class ManageMediaSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  
  constructor(private settingsService: SettingsService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('convertBookmarkToWebP', new FormControl(this.serverSettings.convertBookmarkToWebP, [Validators.required]));
      this.settingsForm.addControl('convertCoverToWebP', new FormControl(this.serverSettings.convertCoverToWebP, [Validators.required]));
    });
  }

  resetForm() {
    this.settingsForm.get('convertBookmarkToWebP')?.setValue(this.serverSettings.convertBookmarkToWebP);
    this.settingsForm.get('convertCoverToWebP')?.setValue(this.serverSettings.convertCoverToWebP);
    this.settingsForm.markAsPristine();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.convertBookmarkToWebP = this.settingsForm.get('convertBookmarkToWebP')?.value;
    modelSettings.convertCoverToWebP = this.settingsForm.get('convertCoverToWebP')?.value;

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
}
