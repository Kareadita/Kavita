import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { SettingsService } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';
import { catchError, shareReplay, take } from 'rxjs/operators';
import { forkJoin, Observable, of } from 'rxjs';
import { ServerService } from 'src/app/_services/server.service';
import { Job } from 'src/app/_models/job/job';

@Component({
  selector: 'app-manage-tasks-settings',
  templateUrl: './manage-tasks-settings.component.html',
  styleUrls: ['./manage-tasks-settings.component.scss']
})
export class ManageTasksSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  reoccuringTasks$: Observable<Array<Job>> = of([]);

  constructor(private settingsService: SettingsService, private toastr: ToastrService, private serverService: ServerService) { }

  ngOnInit(): void {
    forkJoin({
      frequencies: this.settingsService.getTaskFrequencies(),
      levels: this.settingsService.getLoggingLevels(),
      settings: this.settingsService.getServerSettings()
    }
      
    ).subscribe(result => {
      this.taskFrequencies = result.frequencies;
      this.logLevels = result.levels;
      this.serverSettings = result.settings;
      this.settingsForm.addControl('taskScan', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
    });

    this.reoccuringTasks$ = this.serverService.getReoccuringJobs().pipe(shareReplay());
  }

  resetForm() {
    this.settingsForm.get('taskScan')?.setValue(this.serverSettings.taskScan);
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup);
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.taskBackup = this.settingsForm.get('taskBackup')?.value;
    modelSettings.taskScan = this.settingsForm.get('taskScan')?.value;

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

  runAdhocConvert() {
    this.serverService.convertBookmarks().subscribe(() => {
      this.toastr.success('Conversion of Bookmarks has been queued.');
    });
  }


}
