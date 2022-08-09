import { Component, OnInit } from '@angular/core';
import { UntypedFormGroup, UntypedFormControl, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { SettingsService } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';
import { catchError, finalize, shareReplay, take, takeWhile } from 'rxjs/operators';
import { defer, forkJoin, Observable, of } from 'rxjs';
import { ServerService } from 'src/app/_services/server.service';
import { Job } from 'src/app/_models/job/job';
import { UpdateNotificationModalComponent } from 'src/app/shared/update-notification/update-notification-modal.component';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { DownloadService } from 'src/app/shared/_services/download.service';

interface AdhocTask {
  name: string;
  description: string; 
  api: Observable<any>; 
  successMessage: string;
  successFunction?: (data: any) => void;
}

@Component({
  selector: 'app-manage-tasks-settings',
  templateUrl: './manage-tasks-settings.component.html',
  styleUrls: ['./manage-tasks-settings.component.scss']
})
export class ManageTasksSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: UntypedFormGroup = new UntypedFormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  reoccuringTasks$: Observable<Array<Job>> = of([]);
  adhocTasks: Array<AdhocTask> = [
    {
      name: 'Convert Bookmarks to WebP', 
      description: 'Runs a long-running task which will convert all bookmarks to WebP. This is slow (especially on ARM devices).',
      api: this.serverService.convertBookmarks(), 
      successMessage: 'Conversion of Bookmarks has been queued'
    },
    {
      name: 'Clear Cache', 
      description: 'Clears cached files for reading. Usefull when you\'ve just updated a file that you were previously reading within last 24 hours.',
      api: this.serverService.clearCache(), 
      successMessage: 'Cache has been cleared'
    },
    {
      name: 'Backup Database', 
      description: 'Takes a backup of the database, bookmarks, themes, manually uploaded covers, and config files',
      api: this.serverService.backupDatabase(), 
      successMessage: 'A job to backup the database has been queued'
    },
    {
      name: 'Download Logs', 
      description: 'Compiles all log files into a zip and downloads it',
      api: defer(() => of(this.downloadService.download('logs', undefined))), 
      successMessage: ''
    },
    {
      name: 'Check for Updates', 
      description: 'See if there are any Stable releases ahead of your version',
      api: this.serverService.checkForUpdate(), 
      successMessage: '',
      successFunction: (update) => {
        if (update === null) {
          this.toastr.info('No updates available');
          return;
        }
        const modalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
        modalRef.componentInstance.updateData = update;
      }
    },
  ];

  constructor(private settingsService: SettingsService, private toastr: ToastrService, 
    private serverService: ServerService, private modalService: NgbModal,
    private downloadService: DownloadService) { }

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
      this.settingsForm.addControl('taskScan', new UntypedFormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new UntypedFormControl(this.serverSettings.taskBackup, [Validators.required]));
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
      this.reoccuringTasks$ = this.serverService.getReoccuringJobs().pipe(shareReplay());
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

  runAdhoc(task: AdhocTask) {
    task.api.subscribe((data: any) => {
      if (task.successMessage.length > 0) {
        this.toastr.success(task.successMessage);
      }

      if (task.successFunction) {
        task.successFunction(data);
      }
    });
  }


}
