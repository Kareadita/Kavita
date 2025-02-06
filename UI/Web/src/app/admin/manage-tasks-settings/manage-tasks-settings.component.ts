import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {shareReplay} from 'rxjs/operators';
import {debounceTime, defer, distinctUntilChanged, filter, forkJoin, Observable, of, switchMap, tap} from 'rxjs';
import {ServerService} from 'src/app/_services/server.service';
import {Job} from 'src/app/_models/job/job';
import {UpdateNotificationModalComponent} from 'src/app/shared/update-notification/update-notification-modal.component';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {DownloadService} from 'src/app/shared/_services/download.service';
import {DefaultValuePipe} from '../../_pipes/default-value.pipe';
import {AsyncPipe, TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";

import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

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
    imports: [ReactiveFormsModule, AsyncPipe, TitleCasePipe, DefaultValuePipe,
        TranslocoModule, TranslocoLocaleModule, UtcToLocalTimePipe, SettingItemComponent,
      SettingButtonComponent, NgxDatatableModule]
})
export class ManageTasksSettingsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly serverService = inject(ServerService);
  private readonly modalService = inject(NgbModal);
  private readonly downloadService = inject(DownloadService);

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
      name: 'bust-locale-task',
      description: 'bust-locale-task-desc',
      api: defer(() => {
        localStorage.removeItem('@transloco/translations/timestamp');
        localStorage.removeItem('@transloco/translations');
        location.reload();
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
      name: 'clean-up-task',
      description: 'clean-up-task-desc',
      api: this.serverService.cleanup(),
      successMessage: 'clean-up-task-success'
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
    // TODO: Remove this in v0.9. Users should have all updated by then
    {
      name: 'analyze-files-task',
      description: 'analyze-files-task-desc',
      api: this.serverService.analyzeFiles(),
      successMessage: 'analyze-files-task-success'
    },
    {
      name: 'sync-themes-task',
      description: 'sync-themes-task-desc',
      api: this.serverService.syncThemes(),
      successMessage: 'sync-themes-success'
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
        const modalRef = this.modalService.open(UpdateNotificationModalComponent, DefaultModalOptions);
        modalRef.componentInstance.updateData = update;
      }
    },
  ];
  customOption = 'custom';


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

      // Create base controls for taskScan, taskBackup, taskCleanup
      this.settingsForm.addControl('taskScan', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
      this.settingsForm.addControl('taskCleanup', new FormControl(this.serverSettings.taskCleanup, [Validators.required]));


      this.updateCustomFields('taskScan', 'taskScanCustom', this.taskFrequencies, this.serverSettings.taskScan);
      this.updateCustomFields('taskBackup', 'taskBackupCustom', this.taskFrequencies, this.serverSettings.taskBackup);
      this.updateCustomFields('taskCleanup', 'taskCleanupCustom', this.taskFrequenciesForCleanup, this.serverSettings.taskCleanup);

      // Call the validation method for each custom control
      this.validateCronExpression('taskScanCustom');
      this.validateCronExpression('taskBackupCustom');
      this.validateCronExpression('taskCleanupCustom');

      // Setup individual pipelines to save the changes automatically


      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(500),
        filter(_ => this.isFormValid()),
        takeUntilDestroyed(this.destroyRef),
        // switchMap(_ => {
        //   // There can be a timing issue between isValidCron API and the form being valid. I currently solved by upping the debounceTime
        // }),
        switchMap(_ => {
          const data = this.packData();
          return this.settingsService.updateServerSettings(data);
        }),
        tap(settings => {
          this.serverSettings = settings;

          this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
          this.cdRef.markForCheck();
        })
      ).subscribe();

      this.cdRef.markForCheck();
    });

    this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
    this.cdRef.markForCheck();
  }

  // Custom logic to dynamically handle custom fields and validators
  updateCustomFields(controlName: string, customControlName: string, frequencyList: string[], currentSetting: string) {
    if (!frequencyList.includes(currentSetting)) {
      // If the setting is not in the predefined list, it's a custom value
      this.settingsForm.get(controlName)?.setValue(this.customOption);
      this.settingsForm.addControl(customControlName, new FormControl(currentSetting, [Validators.required]));
    } else {
      // Otherwise, reset the custom control (no need for Validators.required here)
      this.settingsForm.addControl(customControlName, new FormControl(''));
    }
  }



  // Validate the custom fields for cron expressions
  validateCronExpression(controlName: string) {
    this.settingsForm.get(controlName)?.valueChanges.pipe(
      debounceTime(100),
      switchMap(val => this.settingsService.isValidCronExpression(val)),
      tap(isValid => {
        if (isValid) {
          this.settingsForm.get(controlName)?.setErrors(null);
        } else {
          this.settingsForm.get(controlName)?.setErrors({ invalidCron: true });
        }

        this.settingsForm.updateValueAndValidity(); // Ensure form validity reflects changes
        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  isFormValid(): boolean {
    // Check if the main form is valid
    if (!this.settingsForm.valid) {
      return false;
    }

    // List of pairs for main control and corresponding custom control
    const customChecks: { mainControl: string; customControl: string }[] = [
      { mainControl: 'taskScan', customControl: 'taskScanCustom' },
      { mainControl: 'taskBackup', customControl: 'taskBackupCustom' },
      { mainControl: 'taskCleanup', customControl: 'taskCleanupCustom' }
    ];

    for (const check of customChecks) {
      const mainControlValue = this.settingsForm.get(check.mainControl)?.value;
      const customControl = this.settingsForm.get(check.customControl);

      // Only validate the custom control if the main control is set to the custom option
      if (mainControlValue === this.customOption) {
        // Ensure custom control has a value and passes validation
        if (customControl?.invalid || !customControl?.value) {
          return false; // Form is invalid if custom option is selected but custom control is invalid or empty
        }
      }
    }

    return true; // Return true only if both main form and any necessary custom fields are valid
  }


  resetForm() {
    this.settingsForm.get('taskScan')?.setValue(this.serverSettings.taskScan, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('taskCleanup')?.setValue(this.serverSettings.taskCleanup, {onlySelf: true, emitEvent: false});

    if (!this.taskFrequencies.includes(this.serverSettings.taskScan)) {
      this.settingsForm.get('taskScanCustom')?.setValue(this.serverSettings.taskScan, {onlySelf: true, emitEvent: false});
    } else {
      this.settingsForm.get('taskScanCustom')?.setValue('', {onlySelf: true, emitEvent: false});
    }

    if (!this.taskFrequencies.includes(this.serverSettings.taskBackup)) {
      this.settingsForm.get('taskBackupCustom')?.setValue(this.serverSettings.taskBackup, {onlySelf: true, emitEvent: false});
    } else {
      this.settingsForm.get('taskBackupCustom')?.setValue('', {onlySelf: true, emitEvent: false});
    }

    if (!this.taskFrequencies.includes(this.serverSettings.taskCleanup)) {
      this.settingsForm.get('taskCleanupCustom')?.setValue(this.serverSettings.taskCleanup, {onlySelf: true, emitEvent: false});
    } else {
      this.settingsForm.get('taskCleanupCustom')?.setValue('', {onlySelf: true, emitEvent: false});
    }

    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  packData() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.taskBackup = this.settingsForm.get('taskBackup')?.value;
    modelSettings.taskScan = this.settingsForm.get('taskScan')?.value;
    modelSettings.taskCleanup = this.settingsForm.get('taskCleanup')?.value;

    if (modelSettings.taskBackup === this.customOption) {
      modelSettings.taskBackup = this.settingsForm.get('taskBackupCustom')?.value;
    }

    if (modelSettings.taskScan === this.customOption) {
      modelSettings.taskScan = this.settingsForm.get('taskScanCustom')?.value;
    }

    if (modelSettings.taskCleanup === this.customOption) {
      modelSettings.taskCleanup = this.settingsForm.get('taskCleanupCustom')?.value;
    }

    console.log('modelSettings: ', modelSettings);
    return modelSettings;
  }


  runAdhoc(task: AdhocTask) {
    task.api.subscribe((data: any) => {
      if (task.successMessage.length > 0) {
        this.toastr.success(translate('manage-tasks-settings.' + task.successMessage));
      }

      if (task.successFunction) {
        task.successFunction(data);
      }
    });
  }


    protected readonly ColumnMode = ColumnMode;
}
