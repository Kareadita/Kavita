import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {shareReplay, take} from 'rxjs/operators';
import {debounceTime, defer, distinctUntilChanged, forkJoin, Observable, of, switchMap, tap} from 'rxjs';
import {ServerService} from 'src/app/_services/server.service';
import {Job} from 'src/app/_models/job/job';
import {UpdateNotificationModalComponent} from 'src/app/shared/update-notification/update-notification-modal.component';
import {NgbModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {DownloadService} from 'src/app/shared/_services/download.service';
import {DefaultValuePipe} from '../../_pipes/default-value.pipe';
import {AsyncPipe, DatePipe, NgFor, NgIf, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule} from "@ngneat/transloco";
import {TranslocoLocaleModule} from "@ngneat/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";

import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

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
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgFor, AsyncPipe, TitleCasePipe, DatePipe, DefaultValuePipe,
    TranslocoModule, NgTemplateOutlet, TranslocoLocaleModule, UtcToLocalTimePipe]
})
export class ManageTasksSettingsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  taskFrequenciesForCleanup: Array<string> = [];
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
      name: 'bust-locale-task',
      description: 'bust-locale-task-desc',
      api: defer(() => {
        localStorage.removeItem('@transloco/translations/timestamp');
        localStorage.removeItem('@transloco/translations');
        localStorage.removeItem('translocoLang');
        return of();
      }),
      successMessage: 'bust-locale-task-success',
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
          this.toastr.info(translate('toasts.no-updates'));
          return;
        }
        const modalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
        modalRef.componentInstance.updateData = update;
      }
    },
  ];
  customOption = 'custom';

  constructor(private settingsService: SettingsService, private toastr: ToastrService,
    private serverService: ServerService, private modalService: NgbModal,
    private downloadService: DownloadService) { }

  ngOnInit(): void {
    forkJoin({
      frequencies: this.settingsService.getTaskFrequencies(),
      levels: this.settingsService.getLoggingLevels(),
      settings: this.settingsService.getServerSettings()
    }).subscribe(result => {
      this.taskFrequencies = result.frequencies;
      this.taskFrequencies.push(this.customOption);

      this.taskFrequenciesForCleanup = this.taskFrequencies.filter(f => f !== 'disabled');

      this.logLevels = result.levels;
      this.serverSettings = result.settings;
      this.settingsForm.addControl('taskScan', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
      this.settingsForm.addControl('taskCleanup', new FormControl(this.serverSettings.taskCleanup, [Validators.required]));

      if (!this.taskFrequencies.includes(this.serverSettings.taskScan)) {
        this.settingsForm.get('taskScan')?.setValue(this.customOption);
        this.settingsForm.addControl('taskScanCustom', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      } else {
        this.settingsForm.addControl('taskScanCustom', new FormControl('', [Validators.required]));
      }

      if (!this.taskFrequencies.includes(this.serverSettings.taskBackup)) {
        this.settingsForm.get('taskBackup')?.setValue(this.customOption);
        this.settingsForm.addControl('taskBackupCustom', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
      } else {
        this.settingsForm.addControl('taskBackupCustom', new FormControl('', [Validators.required]));
      }

      if (!this.taskFrequenciesForCleanup.includes(this.serverSettings.taskCleanup)) {
        this.settingsForm.get('taskCleanup')?.setValue(this.customOption);
        this.settingsForm.addControl('taskCleanupCustom', new FormControl(this.serverSettings.taskCleanup, [Validators.required]));
      } else {
        this.settingsForm.addControl('taskCleanupCustom', new FormControl('', [Validators.required]));
      }

      this.settingsForm.get('taskScanCustom')?.valueChanges.pipe(
        debounceTime(100),
        switchMap(val => this.settingsService.isValidCronExpression(val)),
        tap(isValid => {
          if (isValid) {
            this.settingsForm.get('taskScanCustom')?.setErrors(null);
          } else {
            this.settingsForm.get('taskScanCustom')?.setErrors({invalidCron: true})
          }
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe();

      this.settingsForm.get('taskBackupCustom')?.valueChanges.pipe(
        debounceTime(100),
        switchMap(val => this.settingsService.isValidCronExpression(val)),
        tap(isValid => {
          if (isValid) {
            this.settingsForm.get('taskBackupCustom')?.setErrors(null);
          } else {
            this.settingsForm.get('taskBackupCustom')?.setErrors({invalidCron: true})
          }
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe();

      this.settingsForm.get('taskCleanupCustom')?.valueChanges.pipe(
        debounceTime(100),
        switchMap(val => this.settingsService.isValidCronExpression(val)),
        tap(isValid => {
          if (isValid) {
            this.settingsForm.get('taskCleanupCustom')?.setErrors(null);
          } else {
            this.settingsForm.get('taskCleanupCustom')?.setErrors({invalidCron: true})
          }
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe();

      this.cdRef.markForCheck();
    });

    this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
    this.cdRef.markForCheck();
  }


  resetForm() {
    this.settingsForm.get('taskScan')?.setValue(this.serverSettings.taskScan);
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup);
    this.settingsForm.get('taskCleanup')?.setValue(this.serverSettings.taskCleanup);

    if (!this.taskFrequencies.includes(this.serverSettings.taskScan)) {
      this.settingsForm.get('taskScanCustom')?.setValue(this.serverSettings.taskScan);
    } else {
      this.settingsForm.get('taskScanCustom')?.setValue('');
    }

    if (!this.taskFrequencies.includes(this.serverSettings.taskBackup)) {
      this.settingsForm.get('taskBackupCustom')?.setValue(this.serverSettings.taskBackup);
    } else {
      this.settingsForm.get('taskBackupCustom')?.setValue('');
    }

    if (!this.taskFrequencies.includes(this.serverSettings.taskCleanup)) {
      this.settingsForm.get('taskCleanupCustom')?.setValue(this.serverSettings.taskCleanup);
    } else {
      this.settingsForm.get('taskCleanupCustom')?.setValue('');
    }

    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  async saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.taskBackup = this.settingsForm.get('taskBackup')?.value;
    modelSettings.taskScan = this.settingsForm.get('taskScan')?.value;
    modelSettings.taskCleanup = this.settingsForm.get('taskCleanup')?.value;

    if (this.serverSettings.taskBackup === this.customOption) {
      modelSettings.taskBackup = this.settingsForm.get('taskBackupCustom')?.value;
    }

    if (this.serverSettings.taskScan === this.customOption) {
      modelSettings.taskScan = this.settingsForm.get('taskScanCustom')?.value;
    }

    if (this.serverSettings.taskScan === this.customOption) {
      modelSettings.taskCleanup = this.settingsForm.get('taskCleanupCustom')?.value;
    }


    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
      this.toastr.success(translate('toasts.server-settings-updated'));
      this.cdRef.markForCheck();
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  runAdhoc(task: AdhocTask) {
    task.api.subscribe((data: any) => {
      if (task.successMessage.length > 0) {
        this.toastr.success(translate('manage-tasks-settings.' + task.successMessage));
      }

      if (task.successFunction) {
        task.successFunction(data);
      }
    }, (err: any) => {
      console.error('error: ', err);
    });
  }


}
