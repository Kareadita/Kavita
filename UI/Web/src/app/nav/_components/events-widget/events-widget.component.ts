import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import {NgbPopover} from '@ng-bootstrap/ng-bootstrap';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {RouterLink} from "@angular/router";
import {ReadingSessionUpdateEvent} from "../../../_models/events/reading-session-close-event";
import {EVENTS, Message, MessageHubService} from "../../../_services/message-hub.service";
import {User} from "../../../_models/user/user";
import {LibraryService} from "../../../_services/library.service";
import {EventTitlePipe} from "../../../_pipes/event-title.pipe";
import {EventMessagePipe} from "../../../_pipes/event-message.pipe";
import {EventActionPipe} from "../../../_pipes/event-action.pipe";
import {ActivityAgePipe} from "../../../_pipes/activity-age.pipe";
import {ActivityStoreService, isFinished, isStopped} from "../../../_services/activity-store.service";
import {ActivitySnapshotService} from "../../../_services/activity-snapshot.service";
import {EventsWidgetIconComponent} from "../events-widget-icon/events-widget-icon.component";
import {ActivityJobRowComponent} from "../activity-job-row/activity-job-row.component";
import {ActivityEntryRowComponent} from "../activity-entry-row/activity-entry-row.component";
import {ActivityUpNextComponent} from "../activity-up-next/activity-up-next.component";
import {ActivityRow} from "../../../_models/activity/activity-row";
import {ActivityRowKind} from "../../../_models/activity/activity-row-kind";
import {ActivityEntry} from "../../../_models/activity/activity-entry";
import {ActivityJob} from "../../../_models/activity/activity-job";
import {ActivityFilter} from "../../../_models/activity/activity-filter";
import {ActivityProblemGroup, ActivityTimelineItem} from "../../../_models/activity/activity-timeline-item";
import {MessageEventPriority} from "../../../_models/events/core/message-event-priority";
import {DelayedScanCodes} from "../../../_models/activity/delayed-scan-codes";
import {jobProgress} from "../../../_helpers/activity-job-progress";
import {EventAction} from "../../../_models/events/event-action";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {KeyBindTarget} from "../../../_models/preferences/preferences";
import {KeyBindService} from "../../../_services/key-bind.service";

const AgeTickMs = 30_000;

@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './events-widget.component.html',
  styleUrls: ['./events-widget.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbPopover, TranslocoDirective, RouterLink, EventsWidgetIconComponent, ActivityJobRowComponent,
    ActivityEntryRowComponent, ActivityUpNextComponent, EventActionPipe, ActivityAgePipe]
})
export class EventsWidgetComponent implements OnInit {
  private readonly messageHub = inject(MessageHubService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly libraryService = inject(LibraryService);
  protected readonly activityStore = inject(ActivityStoreService);
  private readonly snapshotService = inject(ActivitySnapshotService);
  private readonly translocoService = inject(TranslocoService);
  private readonly keyBindService = inject(KeyBindService);
  private readonly eventMessagePipe = new EventMessagePipe();
  private readonly eventTitlePipe = new EventTitlePipe();

  readonly user = input.required<User>(); // TODO: Just get the user from AccountService

  private readonly popover = viewChild(NgbPopover);
  private readonly toggleButton = viewChild<ElementRef<HTMLButtonElement>>('toggle');

  protected activeReadingSessions = signal<Set<number>>(new Set());
  protected filter = signal(ActivityFilter.All);
  protected isOpen = signal(false);
  protected now = signal(Date.now());
  protected openGroups = signal<Set<string>>(new Set());
  /**
   * Hub is false until the first connect, so a short blip or page load does not dim the icon
   */
  protected offline = signal(false);

  private readonly isConnected = this.messageHub.isConnectedSignal;
  protected readonly libraryNames = toSignal(this.libraryService.getLibraryNames());
  protected readonly snapshot = this.snapshotService.snapshot;
  private readonly libraryCount = computed(() => Object.keys(this.libraryNames() ?? {}).length);

  private readonly entries = computed(() => this.activityStore.rows().filter(isEntry));
  protected readonly pinned = computed(() => this.entries().filter(e => e.priority === MessageEventPriority.Action || e.scheduleLost));
  protected readonly interruptedJobs = computed(() => this.activityStore.rows().filter(isStopped));
  private readonly pinnedCount = computed(() => this.pinned().length + this.interruptedJobs().length);
  protected readonly delayedEntries = computed(() => this.entries().filter(e => DelayedScanCodes.includes(e.code)));

  /**
   * A delayed scan still waiting shows in Up next, and once its time has passed the scan's own job row says what happened
   */
  private readonly timeline = computed<ActivityTimelineItem[]>(() => {
    const scheduled = new Set(this.snapshot()?.scheduled.map(s => s.runAtUtc) ?? []);
    const now = this.now();
    const isSettled = (scheduledForUtc: string | null) => scheduledForUtc !== null
      && (scheduled.has(scheduledForUtc) || Date.parse(scheduledForUtc) <= now);
    const rows = this.activityStore.rows().filter(r => r.kind === ActivityRowKind.Job
      ? !isStopped(r)
      : r.priority !== MessageEventPriority.Action && r.priority !== MessageEventPriority.Silent && !r.scheduleLost
        && !isSettled(r.scheduledForUtc));
    return groupProblems(rows);
  });

  protected readonly visibleTimeline = computed(() => this.timeline().filter(item => matchesFilter(item, this.filter())));
  protected readonly attentionChipCount = computed(() => this.pinnedCount() + this.timeline().filter(isAttention).length);
  protected readonly jobsChipCount = computed(() => this.activityStore.runningJobs().length);
  protected readonly showPinned = computed(() => this.pinnedCount() > 0 && this.filter() !== ActivityFilter.Jobs);
  protected readonly showUpNext = computed(() => this.snapshot() !== null && this.filter() !== ActivityFilter.Attention);
  protected readonly hasFinished = computed(() => this.activityStore.rows().some(isFinished));

  private readonly runningJobProgress = computed(() => this.activityStore.runningJobs().map(job => jobProgress(job, this.libraryCount())));
  protected readonly runningProgress = computed(() => {
    const determinate = this.runningJobProgress().filter(p => !p.indeterminate && p.value !== null).map(p => p.value!);
    return determinate.length > 0 ? Math.min(...determinate) : null;
  });
  protected readonly indeterminate = computed(() => this.runningJobProgress().some(p => p.indeterminate));
  protected readonly attentionCount = computed(() => this.interruptedJobs().length
    + this.entries().filter(e => e.priority >= MessageEventPriority.Action || e.scheduleLost).length);
  protected readonly hasError = computed(() => this.entries().some(e => e.priority === MessageEventPriority.Error));

  // Re-runs the label once the language file loads, translate() alone is not reactive
  private readonly translation = toSignal(this.translocoService.selectTranslation());

  protected readonly buttonAltLabel = computed(() => {
    this.translation();

    const parts = [this.translocoService.translate('events-widget.title-alt')];

    if (this.offline()) {
      parts.push(this.translocoService.translate('events-widget.status-offline-alt'));
    } else {
      const count = this.attentionCount();
      if (count > 0) {
        parts.push(this.hasError()
          ? this.translocoService.translate('events-widget.status-attention-error-alt', {count})
          : this.translocoService.translate('events-widget.status-attention-alt', {count}));
      }
      const running = this.activityStore.runningJobs().length;
      if (running > 0) {
        parts.push(this.translocoService.translate('events-widget.status-running-alt', {count: running}));
      }
      const reading = this.activeReadingSessions().size;
      if (reading > 0) {
        parts.push(this.translocoService.translate('events-widget.reading-now', {num: reading}));
      }
    }

    return parts.join(', ');
  });

  protected readonly announcement = computed(() => {
    this.translation();

    const entry = this.activityStore.announcement();
    if (!entry) return '';
    return entry.code
      ? this.eventMessagePipe.transform(entry, 'label')
      : this.eventTitlePipe.transform(entry, false, this.libraryNames());
  });

  private readonly snapshotKey = computed(() => `${this.activityStore.runningJobs().length}|${this.delayedEntries().length}`);

  constructor() {
    this.keyBindService.registerListener(
      this.destroyRef,
      (e) => this.toggleButton()?.nativeElement?.click(),
      [KeyBindTarget.OpenEventWidget],
      {fireInEditable: true},
    );

    effect(onCleanup => {
      if (this.isConnected() !== false) {
        this.offline.set(false);
        return;
      }
      // 10s matches the popover's server unreachable delay
      const timer = setTimeout(() => this.offline.set(true), 10_000);
      onCleanup(() => clearTimeout(timer));
    });

    effect(() => {
      if (!this.isOpen()) return;
      this.snapshotKey();
      untracked(() => this.snapshotService.refresh());
    });

    effect(onCleanup => {
      if (!this.isOpen()) return;
      this.now.set(Date.now());
      const timer = setInterval(() => this.now.set(Date.now()), AgeTickMs);
      onCleanup(() => clearInterval(timer));
    });
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event: Message<unknown>) => {
      if (event.event === EVENTS.ReadingSessionUpdate) {
        const data = event.payload as ReadingSessionUpdateEvent;
        this.activeReadingSessions.update(set => new Set([...set, data.sessionId]));
      } else if (event.event === EVENTS.ReadingSessionClose) {
        const data = event.payload as ReadingSessionUpdateEvent;
        this.activeReadingSessions.update(set => {
          const newSet = new Set(set);
          newSet.delete(data.sessionId);
          return newSet;
        });
      }
    });
  }

  protected toggleGroup(id: string) {
    this.openGroups.update(set => {
      const next = new Set(set);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  protected close() {
    this.popover()?.close();
  }

  // The popover is attached to body, so Tab from the button would skip it
  protected onShown() {
    this.isOpen.set(true);
    document.querySelector<HTMLElement>('.nav-events .activity')?.focus();
  }

  protected closeAndRefocus() {
    this.close();
    this.toggleButton()?.nativeElement.focus();
  }

  protected readonly ActivityFilter = ActivityFilter;
  protected readonly ActivityRowKind = ActivityRowKind;
  protected readonly EventAction = EventAction;
  protected readonly SettingsTabId = SettingsTabId;
}

function isEntry(row: ActivityRow): row is ActivityEntry {
  return row.kind === ActivityRowKind.Entry;
}

function isAttention(item: ActivityTimelineItem) {
  if (item.kind === 'group') return true;
  return item.kind === ActivityRowKind.Entry && item.priority >= MessageEventPriority.Info && !DelayedScanCodes.includes(item.code);
}

function matchesFilter(item: ActivityTimelineItem, filter: ActivityFilter) {
  switch (filter) {
    case ActivityFilter.All:
      return true;
    case ActivityFilter.Attention:
      return isAttention(item);
    case ActivityFilter.Jobs:
      return item.kind === ActivityRowKind.Job || (item.kind === ActivityRowKind.Entry && DelayedScanCodes.includes(item.code));
  }
}

function groupProblems(rows: ActivityRow[]): ActivityTimelineItem[] {
  const multiLibraryJobs = new Set(rows
    .filter((r): r is ActivityJob => r.kind === ActivityRowKind.Job && r.libraryIds.length > 1 && r.correlationId !== null)
    .map(j => j.correlationId));

  const byJob = new Map<string, ActivityEntry[]>();
  for (const row of rows) {
    if (row.kind !== ActivityRowKind.Entry || !row.correlationId || !multiLibraryJobs.has(row.correlationId) || !isAttention(row)) continue;
    byJob.set(row.correlationId, [...(byJob.get(row.correlationId) ?? []), row]);
  }

  const groups: ActivityProblemGroup[] = [];
  const grouped = new Set<string>();
  byJob.forEach((entries, correlationId) => {
    const libraryCount = new Set(entries.map(e => e.libraryId).filter(id => id !== null)).size;
    if (libraryCount < 2) return;

    entries.forEach(e => grouped.add(e.id));
    groups.push({
      kind: 'group',
      id: `group:${correlationId}`,
      entries,
      libraryCount,
      updatedUtc: entries.map(e => e.updatedUtc).sort().at(-1)!,
    });
  });

  if (groups.length === 0) return rows;

  return [...rows.filter(r => !grouped.has(r.id)), ...groups]
    .sort((a, b) => Date.parse(b.updatedUtc) - Date.parse(a.updatedUtc));
}
