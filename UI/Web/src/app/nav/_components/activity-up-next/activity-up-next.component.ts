import {ChangeDetectionStrategy, Component, computed, input, signal} from '@angular/core';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {ActivitySnapshot} from '../../../_models/activity/activity-snapshot';
import {ActivityEntry} from '../../../_models/activity/activity-entry';
import {ScheduledScan} from '../../../_models/activity/scheduled-scan';
import {UpcomingTask} from '../../../_models/activity/upcoming-task';
import {UtcToLocalTimePipe} from '../../../_pipes/utc-to-local-time.pipe';
import {UpcomingTaskNamePipe} from '../../../_pipes/upcoming-task-name.pipe';
import {CronFrequencyPipe} from '../../../_pipes/cron-frequency.pipe';

const CollapsedCount = 2;
const WaitingGroupAt = 4;
const WaitingNamesShown = 5;

type UpNextItem =
  | {kind: 'task'; id: string; runAtUtc: string; task: UpcomingTask; frequency: CronFrequency | null}
  | {kind: 'scan'; id: string; runAtUtc: string; title: string}
  | {kind: 'waiting'; id: string; runAtUtc: string; count: number; names: string[]; rest: number};

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

  protected readonly items = computed<UpNextItem[]>(() => {
    const snapshot = this.snapshot();
    const scans = snapshot.scheduled;

    const scanItems: UpNextItem[] = snapshot.scheduledTotal >= WaitingGroupAt && scans.length > 0
      ? [{
        kind: 'waiting',
        id: 'waiting',
        runAtUtc: scans[0].runAtUtc,
        count: snapshot.scheduledTotal,
        names: scans.slice(0, WaitingNamesShown).map(s => this.scanTitle(s)),
        rest: snapshot.scheduledTotal - Math.min(scans.length, WaitingNamesShown),
      }]
      : scans.map(s => ({kind: 'scan', id: `scan:${s.jobId}`, runAtUtc: s.runAtUtc, title: this.scanTitle(s)}));

    const taskItems: UpNextItem[] = snapshot.upcoming.map(task => ({
      kind: 'task',
      id: `task:${task.taskId}`,
      runAtUtc: task.nextRunUtc,
      task,
      frequency: frequencyOf(task.cron),
    }));

    return [...scanItems, ...taskItems].sort((a, b) => Date.parse(a.runAtUtc) - Date.parse(b.runAtUtc));
  });

  protected readonly visibleItems = computed(() => this.expanded() ? this.items() : this.items().slice(0, CollapsedCount));
  protected readonly hiddenCount = computed(() => Math.max(0, this.items().length - CollapsedCount));

  private scanTitle(scan: ScheduledScan) {
    const body = (this.delayedEntries().find(e => e.scheduledForUtc === scan.runAtUtc)?.body ?? {}) as ScheduledBody;
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
