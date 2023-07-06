import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { SettingsService } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';
import { shareReplay, take } from 'rxjs/operators';
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
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  recurringTasks$: Observable<Array<Job>> = of([]);
  adhocTasks: Array<AdhocTask> = [
    {
      name: 'Convert Media to Target Encoding',
      description: 'Runs a long-running task which will convert all kavita-managed media to the target encoding. This is slow (especially on ARM devices).',
      api: this.serverService.convertMedia(),
      successMessage: 'Conversion of Media to Target Encoding has been queued'
    },
    {
      name: 'Bust Cache',
      description: 'Busts the Kavita+ Cache - should only be used when debugging bad matches.',
      api: this.serverService.bustCache(),
      successMessage: 'Kavita+ Cache busted'
    },
    {
      name: 'Clear Reading Cache',
      description: 'Clears cached files for reading. Useful when you\'ve just updated a file that you were previously reading within the last 24 hours.',
      api: this.serverService.clearCache(),
      successMessage: 'Cache has been cleared'
    },
    {
      name: 'Clean up Want to Read',
      description: 'Removes any series that users have fully read that are within Want to Read and have a publication status of Completed. Runs every 24 hours.',
      api: this.serverService.cleanupWantToRead(),
      successMessage: 'Want to Read has been cleaned up'
    },
    {
      name: 'Backup Database',
      description: 'Takes a backup of the database, bookmarks, themes, manually uploaded covers, and config files.',
      api: this.serverService.backupDatabase(),
      successMessage: 'A job to backup the database has been queued'
    },
    {
      name: 'Download Logs',
      description: 'Compiles all log files into a zip and downloads it.',
      api: defer(() => of(this.downloadService.download('logs', undefined))),
      successMessage: ''
    },
    {
      name: 'Analyze Files',
      description: 'Runs a long-running task which will analyze files to generate extension and size. This should only be ran once for the v0.7 release. Not needed if you installed post v0.7.',
      api: this.serverService.analyzeFiles(),
      successMessage: 'File analysis has been queued'
    },
    {
      name: 'Check for Updates',
      description: 'See if there are any Stable releases ahead of your version.',
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
      this.settingsForm.addControl('taskScan', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
    });

    this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
  }

  resetForm() {
    this.settingsForm.get('taskScan')?.setValue(this.serverSettings.taskScan);
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup);
    this.settingsForm.markAsPristine();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.taskBackup = this.settingsForm.get('taskBackup')?.value;
    modelSettings.taskScan = this.settingsForm.get('taskScan')?.value;

    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
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

  runAdhoc(task: AdhocTask) {
    task.api.subscribe((data: any) => {
      if (task.successMessage.length > 0) {
        this.toastr.success(task.successMessage);
      }

      if (task.successFunction) {
        task.successFunction(data);
      }
    }, (err: any) => {
      console.error('error: ', err);
    });
  }


}
