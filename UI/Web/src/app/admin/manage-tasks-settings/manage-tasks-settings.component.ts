import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {ToastrService} from 'ngx-toastr';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {shareReplay} from 'rxjs/operators';
import {catchError, combineLatest, debounceTime, defer, Observable, of, skip, switchMap, tap} from 'rxjs';
import {ServerService} from 'src/app/_services/server.service';
import {Job} from 'src/app/_models/job/job';
import {DownloadService} from 'src/app/shared/_services/download.service';
import {DownloadEntityType} from 'src/app/shared/_models/download-queue-item';
import {DefaultValuePipe} from '../../_pipes/default-value.pipe';
import {AsyncPipe, TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";

import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {VersionService} from "../../_services/version.service";
import {CronFrequency} from "../../shared/_models/cron-frequency";
import {SettingCronItemComponent} from "../../settings/_components/setting-cron-item/setting-cron-item.component";

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
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AsyncPipe, TitleCasePipe, DefaultValuePipe,
    TranslocoModule, TranslocoLocaleModule, UtcToLocalTimePipe,
    SettingButtonComponent, NgxDatatableModule, ResponsiveTableComponent, SettingCronItemComponent]
})
export class ManageTasksSettingsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly serverService = inject(ServerService);
  private readonly versionService = inject(VersionService);
  private readonly downloadService = inject(DownloadService);

  serverSettings!: ServerSettings;

  taskScan = signal('');
  taskBackup = signal('');
  taskCleanup = signal('');
  taskCblSync = signal('');

  cleanupFrequencies: CronFrequency[] = [CronFrequency.Daily, CronFrequency.Weekly, CronFrequency.Custom];

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
      api: defer(() => of(this.downloadService.download(DownloadEntityType.Logs, undefined, 0, 0))),
      successMessage: ''
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
        this.versionService.showUpdateModal('update-available', { update }, true);
      }
    },
  ];

  trackBy = (index: number, item: Job) => `${item.id}`;

  private readonly taskScan$ = toObservable(this.taskScan);
  private readonly taskBackup$ = toObservable(this.taskBackup);
  private readonly taskCleanup$ = toObservable(this.taskCleanup);
  private readonly taskCblSync$ = toObservable(this.taskCblSync);

  constructor() {}

  ngOnInit(): void {
    this.settingsService.getServerSettings().subscribe(settings => {
      this.serverSettings = settings;

      this.taskScan.set(settings.taskScan);
      this.taskBackup.set(settings.taskBackup);
      this.taskCleanup.set(settings.taskCleanup);
      this.taskCblSync.set(settings.taskCblSync);

      combineLatest([
        this.taskScan$,
        this.taskBackup$,
        this.taskCleanup$,
        this.taskCblSync$
      ]).pipe(
        skip(1), // Skips the initial values being saved so we avoid a save with no changes
        debounceTime(500),
        switchMap(([scan, backup, cleanup, cblSync]) => {
          const data = this.packData(scan, backup, cleanup, cblSync);
          return this.settingsService.updateServerSettings(data).pipe(catchError(err => {
            console.error(err);
            return of(null);
          }));
        }),
        tap(settings => {
          if (!settings) return;
          this.serverSettings = settings;
          this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe();

      this.cdRef.markForCheck();
    });

    this.recurringTasks$ = this.serverService.getRecurringJobs().pipe(shareReplay());
    this.cdRef.markForCheck();
  }

  packData(scan: string, backup: string, cleanup: string, cblSync: string) {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.taskScan = scan;
    modelSettings.taskBackup = backup;
    modelSettings.taskCleanup = cleanup;
    modelSettings.taskCblSync = cblSync;
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
