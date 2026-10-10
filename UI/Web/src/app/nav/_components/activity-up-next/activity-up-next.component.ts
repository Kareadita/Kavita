import {ChangeDetectionStrategy, Component, computed, input, signal} from '@angular/core';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {ActivitySnapshot} from '../../../_models/activity/activity-snapshot';
import {ActivityEntry} from '../../../_models/activity/activity-entry';
import {ScheduledScan} from '../../../_models/activity/scheduled-scan';
import {ScheduledFolderScan} from '../../../_models/activity/scheduled-folder-scan';
import {UpcomingTask} from '../../../_models/activity/upcoming-task';
import {UtcToLocalTimePipe} from '../../../_pipes/utc-to-local-time.pipe';
import {UpcomingTaskNamePipe} from '../../../_pipes/upcoming-task-name.pipe';
import {CronFrequencyPipe} from '../../../_pipes/cron-frequency.pipe';
import {isDelayedEntryFor} from '../../../_helpers/delayed-scan';

const CollapsedCount = 2;
const WaitingGroupAt = 4;
const WaitingNamesShown = 5;

type UpNextItem =
  | {kind: 'task'; id: string; runAtUtc: string; task: UpcomingTask; frequency: CronFrequency | null}
  | {kind: 'scan'; id: string; runAtUtc: string; title: string; queued: boolean}
  | {kind: 'waiting'; id: string; runAtUtc: string; count: number; names: string[]; rest: number}
  | {kind: 'folders'; id: string; runAtUtc: string; queued: boolean; title: string; fromFolderWatcher: boolean; folders: string[]; rest: number};

type CronFrequency = 'daily' | 'weekly';

type ScheduledBody = Partial<{libraryName: string; seriesName: string}>;

@Component({
  selector: 'app-activity-up-next',
  templateUrl: './activity-up-next.component.html',
  styleUrl: './activity-up-next.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, UtcToLocalTimePipe, UpcomingTaskNamePipe, CronFrequencyPipe]
})
export class ActivityUpNextComponent {
  readonly snapshot = input.required<ActivitySnapshot>();
  /**
   * Delayed scan Infos, the only place a scheduled series scan's name is known
   */
  readonly delayedEntries = input<ActivityEntry[]>([]);
  readonly libraryNames = input<Record<number, string>>();

  protected expanded = signal(false);
  protected waitingOpen = signal(false);
  protected openFolderRows = signal<string[]>([]);

  protected readonly items = computed<UpNextItem[]>(() => {
    const snapshot = this.snapshot();
    const scanItems = this.scanItems(snapshot.scheduled, snapshot.scheduledTotal);
    const folderItems = snapshot.scheduledFolderScans.map(scan => this.folderItem(scan));

    const taskItems: UpNextItem[] = snapshot.upcoming.map(task => ({
      kind: 'task',
      id: `task:${task.taskId}`,
      runAtUtc: task.nextRunUtc,
      task,
      frequency: frequencyOf(task.cron),
    }));

    return [...scanItems, ...folderItems, ...taskItems].sort((a, b) => Date.parse(a.runAtUtc) - Date.parse(b.runAtUtc));
  });

  protected readonly visibleItems = computed(() => this.expanded() ? this.items() : this.items().slice(0, CollapsedCount));
  protected readonly hiddenCount = computed(() => Math.max(0, this.items().length - CollapsedCount));

  /**
   * One row per scan, or a single collapsible group once WaitingGroupAt scans are waiting
   */
  private scanItems(scans: ScheduledScan[], total: number): UpNextItem[] {
    if (scans.length === 0) return [];

    if (total < WaitingGroupAt) {
      return scans.map((scan, index) => ({
        kind: 'scan',
        id: `scan:${scan.jobId}`,
        runAtUtc: scan.runAtUtc,
        title: this.scanTitle(scan),
        queued: index > 0,
      }));
    }

    const shown = scans.slice(0, WaitingNamesShown);
    return [{
      kind: 'waiting',
      id: 'waiting',
      runAtUtc: scans[0].runAtUtc,
      count: total,
      names: shown.map(scan => this.scanTitle(scan)),
      rest: total - shown.length,
    }];
  }

  protected toggleFolderRow(id: string) {
    this.openFolderRows.update(ids => ids.includes(id) ? ids.filter(i => i !== id) : [...ids, id]);
  }

  private folderItem(scan: ScheduledFolderScan): UpNextItem {
    const libraryName = scan.libraryId !== null ? this.libraryNames()?.[scan.libraryId] : undefined;
    const title = scan.folderCount === 1
      ? translate('events-widget.scan-folder-scheduled', {folder: scan.folders[0]})
      : libraryName
        ? translate('events-widget.scan-folders-in-library-scheduled', {count: scan.folderCount, libraryName})
        : translate('events-widget.scan-folders-scheduled', {count: scan.folderCount});

    return {
      kind: 'folders',
      id: `folders:${scan.libraryId}:${scan.fromFolderWatcher}`,
      runAtUtc: scan.runAtUtc ?? new Date().toISOString(),
      queued: scan.runAtUtc === null,
      title,
      fromFolderWatcher: scan.fromFolderWatcher,
      folders: scan.folderCount === 1 ? [] : scan.folders,
      rest: scan.folderCount - scan.folders.length,
    };
  }

  private scanTitle(scan: ScheduledScan) {
    const body = (this.delayedEntries().find(e => isDelayedEntryFor(e, scan))?.body ?? {}) as ScheduledBody;
    const libraryName = body.libraryName || (scan.libraryId !== null ? this.libraryNames()?.[scan.libraryId] : undefined);

    if (scan.libraryId === null) return translate('events-widget.scan-libraries-scheduled');
    if (scan.seriesId !== null) {
      return body.seriesName
        ? translate('events-widget.scan-series-scheduled', {seriesName: body.seriesName})
        : translate('events-widget.scan-series-in-library-scheduled', {libraryName: libraryName ?? ''});
    }
    return translate('events-widget.scan-scheduled', {libraryName: libraryName ?? ''});
  }

}

/**
 * A 5 field cron that fires once at a fixed time, as the frequency names cronFrequency translates
 */
function frequencyOf(cron: string | null | undefined): CronFrequency | null {
  const fields = (cron ?? '').trim().split(/\s+/);
  if (fields.length !== 5) return null;

  const [minute, hour, dayOfMonth, month, dayOfWeek] = fields;
  if (!isNumber(minute) || !isNumber(hour) || dayOfMonth !== '*' || month !== '*') return null;
  if (dayOfWeek === '*') return 'daily';
  return isNumber(dayOfWeek) ? 'weekly' : null;
}

function isNumber(field: string) {
  return /^\d+$/.test(field);
}
