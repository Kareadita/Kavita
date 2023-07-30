import {Component, inject, OnInit} from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { SettingsService } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';
import { shareReplay, take } from 'rxjs/operators';
import { defer, forkJoin, Observable, of } from 'rxjs';
import { ServerService } from 'src/app/_services/server.service';
import { Job } from 'src/app/_models/job/job';
import { UpdateNotificationModalComponent } from 'src/app/shared/update-notification/update-notification-modal.component';
import { NgbModal, NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { DefaultValuePipe } from '../../pipe/default-value.pipe';
import {NgIf, NgFor, AsyncPipe, TitleCasePipe, DatePipe, NgTemplateOutlet} from '@angular/common';
import {TranslocoModule, TranslocoService} from "@ngneat/transloco";

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
    styleUrls: ['./manage-tasks-settings.component.scss'],
    standalone: true,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgFor, AsyncPipe, TitleCasePipe, DatePipe, DefaultValuePipe, TranslocoModule, NgTemplateOutlet]
})
export class ManageTasksSettingsComponent implements OnInit {

  translocoService = inject(TranslocoService);
  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  recurringTasks$: Observable<Array<Job>> = of([]);
  // noinspection JSVoidFunctionReturnValueUsed
  adhocTasks: Array<AdhocTask> = [
    {
      name: 'convert-media-task',
      description: 'convert-media-task-desc',
      api: this.serverService.convertMedia(),
      successMessage: 'convert-media-task-success'
    },
    {
      name: 'bust-cache-task',
      description: 'bust-cache-task-desc',
      api: this.serverService.bustCache(),
      successMessage: 'bust-cache-task-success'
    },
    {
      name: 'clear-reading-cache-task',
      description: 'clear-reading-cache-task-desc',
      api: this.serverService.clearCache(),
      successMessage: 'clear-reading-cache-task-success'
    },
    {
      name: 'clean-up-want-to-read-task',
      description: 'clean-up-want-to-read-task-desc',
      api: this.serverService.cleanupWantToRead(),
      successMessage: 'clean-up-want-to-read-task-success'
    },
    {
      name: 'backup-database-task',
      description: 'backup-database-task-desc',
      api: this.serverService.backupDatabase(),
      successMessage: 'backup-database-task-success'
    },
    {
      name: 'download-logs-task',
      description: 'download-logs-task-desc',
      api: defer(() => of(this.downloadService.download('logs', undefined))),
      successMessage: ''
    },
    {
      name: 'analyze-files-task',
      description: 'analyze-files-task-desc',
      api: this.serverService.analyzeFiles(),
      successMessage: 'analyze-files-task-success'
    },
    {
      name: 'check-for-updates-task',
      description: 'check-for-updates-task-desc',
      api: this.serverService.checkForUpdate(),
      successMessage: '',
      successFunction: (update) => {
        if (update === null) {
          this.toastr.info(this.translocoService.translate('toasts.no-updates'));
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
      this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  runAdhoc(task: AdhocTask) {
    task.api.subscribe((data: any) => {
      if (task.successMessage.length > 0) {
        this.toastr.success(this.translocoService.translate('manage-tasks-settings.' + task.successMessage));
      }

      if (task.successFunction) {
        task.successFunction(data);
      }
    }, (err: any) => {
      console.error('error: ', err);
    });
  }


}
