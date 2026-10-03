import {computed, DestroyRef, effect, inject, Injectable, signal, untracked} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {EVENTS, Message, MessageHubService} from './message-hub.service';
import {AccountService} from './account.service';
import {SignalRMessage} from '../_models/events/core/signalr-message';
import {MessageEventPriority} from '../_models/events/core/message-event-priority';
import {ActivityRow} from '../_models/activity/activity-row';
import {ActivityRowKind} from '../_models/activity/activity-row-kind';
import {ActivityJob} from '../_models/activity/activity-job';
import {ActivityEntry} from '../_models/activity/activity-entry';
import {ActivityStep} from '../_models/activity/activity-step';
import {DismissedActivity} from '../_models/activity/dismissed-activity';
import {PersistedActivity} from '../_models/activity/persisted-activity';
import {ActivityEndReason} from '../_models/activity/activity-end-reason';
import {ActivitySnapshotResult} from '../_models/activity/activity-snapshot-result';
import {DelayedScanCodes} from '../_models/activity/delayed-scan-codes';
import {ActivitySnapshotService} from './activity-snapshot.service';
import {RecentJob} from '../_models/activity/recent-job';

const RowTtlMs = 24 * 60 * 60 * 1000;
const MaxRows = 100;
const TextThrottleMs = 750;
const WriteDebounceMs = 1000;
// FileScan ends before ScanProgress's first update, and CoverUpdate ends once per series, so all-ended is not yet done
const FinishGraceMs = 10_000;
const RateLimitWindowMs = 30 * 60 * 1000;

const ActionNames: string[] = [EVENTS.UpdateAvailable, EVENTS.ScrobblingKeyExpired, EVENTS.ExternalMatchRateLimitError];

interface PendingUpdate {
  message: SignalRMessage;
  timer: ReturnType<typeof setTimeout>;
}

@Injectable({
  providedIn: 'root'
})
export class ActivityStoreService {
  private readonly messageHub = inject(MessageHubService);
  private readonly accountService = inject(AccountService);
  private readonly snapshotService = inject(ActivitySnapshotService);
  private readonly destroyRef = inject(DestroyRef);

  private _rows = signal<ActivityRow[]>([]);
  private _announcement = signal<ActivityEntry | null>(null);
  private dismissed: DismissedActivity[] = [];
  private storageKey: string | null = null;
  private writeTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly pendingUpdates = new Map<string, PendingUpdate>();
  private readonly lastApplied = new Map<string, number>();
  private readonly finishTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private readonly lastFrameMs = new Map<string, number>();

  /**
   * Newest first
   */
  readonly rows = computed(() => [...this._rows()].sort((a, b) => Date.parse(b.updatedUtc) - Date.parse(a.updatedUtc)));
  readonly runningJobs = computed(() => this._rows()
    .filter((r): r is ActivityJob => r.kind === ActivityRowKind.Job && r.endedUtc === null));
  /**
   * Latest Action or Error entry that arrived live, never one restored from storage
   */
  readonly announcement = this._announcement.asReadonly();

  constructor() {
    effect(() => {
      const userId = this.accountService.userId();
      untracked(() => this.switchUser(userId));
    });

    // Covers page load and every reconnect, including one after a server restart
    effect(() => {
      if (this.messageHub.isConnectedSignal() && this.accountService.hasAdminRole()) {
        untracked(() => this.snapshotService.refresh());
      }
    });

    effect(() => {
      const result = this.snapshotService.result();
      if (result) untracked(() => this.reconcile(result));
    });

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(message => this.ingest(message));

    // pagehide fires on refresh, tab close and navigating away, so the pending write is not lost. Unlike beforeunload it also fires on mobile
    const flush = () => this.flushWrite();
    window.addEventListener('pagehide', flush);

    const sync = (event: StorageEvent) => this.onStorage(event);
    window.addEventListener('storage', sync);

    this.destroyRef.onDestroy(() => {
      window.removeEventListener('pagehide', flush);
      window.removeEventListener('storage', sync);
    });
  }

  dismiss(id: string) {
    this.dismissMany([id]);
  }

  clearFinished() {
    this.dismissMany(this._rows().filter(isFinished).map(r => r.id));
  }

  private dismissMany(ids: string[]) {
    if (ids.length === 0) return;

    const dismissedUtc = new Date().toISOString();
    ids.forEach(id => this.cancelFinish(id));
    this._rows.update(rows => rows.filter(r => !ids.includes(r.id)));
    this.dismissed = [...this.dismissed.filter(d => !ids.includes(d.id)), ...ids.map(id => ({id, dismissedUtc}))];
    this.scheduleWrite();
  }

  private ingest(message: Message<unknown>) {
    if (!this.storageKey) return;

    switch (message.event) {
      case EVENTS.NotificationProgress:
        this.ingestProgress(message.payload as SignalRMessage);
        break;
      case EVENTS.Info:
      case EVENTS.Error:
      case EVENTS.UpdateAvailable:
      case EVENTS.ScrobblingKeyExpired:
        if (message.meta) this.addEntry({...message.meta, body: message.payload});
        break;
      case EVENTS.ExternalMatchRateLimitError:
        if (message.meta) this.addRateLimit({...message.meta, body: message.payload});
        break;
      case EVENTS.SeriesAdded:
        this.countSeries(message.meta?.correlationId, 'seriesAdded');
        break;
      case EVENTS.SeriesRemoved:
        this.countSeries(message.meta?.correlationId, 'seriesRemoved');
        break;
    }
  }

  private countSeries(correlationId: string | null | undefined, field: 'seriesAdded' | 'seriesRemoved') {
    if (!correlationId) return;

    const job = this.findJob(`job:${correlationId}`);
    if (!job) return;

    this._rows.update(rows => rows.map(r => r === job ? {...job, [field]: job[field] + 1} : r));
    this.scheduleWrite();
  }

  private addRateLimit(message: SignalRMessage, announce = true) {
    const cutoff = Date.parse(toUtc(message.eventTimeUtc)) - RateLimitWindowMs;
    const open = this._rows().find((r): r is ActivityEntry => r.kind === ActivityRowKind.Entry
      && r.name === message.name && Date.parse(r.updatedUtc) >= cutoff);

    if (!open) {
      this.addEntry(message, `entry:${message.name}|${message.eventTimeUtc}`, announce);
      return;
    }

    const next: ActivityEntry = {...open, count: open.count + 1, updatedUtc: toUtc(message.eventTimeUtc)};
    this._rows.update(rows => rows.map(r => r === open ? next : r));
    this.scheduleWrite();
  }

  private ingestProgress(message: SignalRMessage) {
    if (isOneOff(message)) {
      this.addEntry(message);
      return;
    }

    const id = jobIdOf(message);
    const throttleKey = `${id}|${message.name}`;
    this.lastFrameMs.set(id, Date.now());

    if (message.eventType === 'ended') {
      this.cancelPending(throttleKey);
      this.lastApplied.delete(throttleKey);
      this.applyStep(id, message);
      return;
    }

    const step = this.findJob(id)?.steps[message.name];
    const sinceLast = Date.now() - (this.lastApplied.get(throttleKey) ?? 0);
    const applyNow = message.eventType !== 'updated' || !step || step.code !== message.code || sinceLast >= TextThrottleMs;

    if (applyNow) {
      this.cancelPending(throttleKey);
      this.lastApplied.set(throttleKey, Date.now());
      this.applyStep(id, message);
      return;
    }

    const pending = this.pendingUpdates.get(throttleKey);
    if (pending) {
      pending.message = message;
      return;
    }

    const timer = setTimeout(() => {
      const latest = this.pendingUpdates.get(throttleKey);
      this.pendingUpdates.delete(throttleKey);
      if (!latest) return;

      this.lastApplied.set(throttleKey, Date.now());
      this.applyStep(id, latest.message);
    }, TextThrottleMs - sinceLast);
    this.pendingUpdates.set(throttleKey, {message, timer});
  }

  private applyStep(id: string, message: SignalRMessage, live = true) {
    const job = this.findJob(id);
    const previous = job?.steps[message.name];
    const isEnded = message.eventType === 'ended';

    // An ended never creates or reopens a job (a scan with no changes sends ScanProgress ended with no prior step)
    if (isEnded && (!previous || previous.eventType === 'ended')) return;
    if (!isEnded) this.dismissed = this.dismissed.filter(d => d.id !== id);

    const updatedUtc = toUtc(message.eventTimeUtc);
    const step: ActivityStep = isEnded ? {...previous!, eventType: 'ended', updatedUtc} : stepOf(message, previous);

    const steps = {...job?.steps, [message.name]: step};
    const libraryId = numberOf(bodyField(message.body, 'libraryId'));

    const next: ActivityJob = {
      kind: ActivityRowKind.Job,
      id,
      correlationId: message.correlationId,
      libraryId: job?.libraryId ?? libraryId,
      libraryIds: withLibrary(job?.libraryIds ?? [], libraryId),
      priority: job?.priority ?? priorityOf(message),
      startedUtc: job?.startedUtc ?? updatedUtc,
      updatedUtc,
      endedUtc: null,
      endReason: null,
      steps,
      seriesAdded: job?.seriesAdded ?? 0,
      seriesRemoved: job?.seriesRemoved ?? 0,
      seenFromStart: job?.seenFromStart ?? (live && message.eventType === 'started'),
    };

    this._rows.update(rows => job ? rows.map(r => r.id === id ? next : r) : [...rows, next]);
    this.scheduleWrite();

    if (allStepsEnded(next)) {
      this.scheduleFinish(id);
    } else {
      this.cancelFinish(id);
    }
  }

  private scheduleFinish(id: string) {
    this.cancelFinish(id);
    this.finishTimers.set(id, setTimeout(() => {
      this.finishTimers.delete(id);
      const job = this.findJob(id);
      if (!job || job.endedUtc !== null || !allStepsEnded(job)) return;

      this._rows.update(rows => rows.map(r => r === job ? {...job, endedUtc: job.updatedUtc} : r));
      this.scheduleWrite();
    }, FinishGraceMs));
  }

  private cancelFinish(id: string) {
    clearTimeout(this.finishTimers.get(id));
    this.finishTimers.delete(id);
  }

  private addEntry(message: SignalRMessage, id = entryIdOf(message), announce = true) {
    if (this.dismissed.some(d => d.id === id) || this._rows().some(r => r.id === id)) return;

    const entry: ActivityEntry = {
      kind: ActivityRowKind.Entry,
      id,
      name: message.name,
      code: message.code,
      priority: priorityOf(message),
      title: message.title,
      subTitle: message.subTitle,
      body: message.body,
      correlationId: message.correlationId,
      libraryId: numberOf(bodyField(message.body, 'libraryId')),
      seriesId: numberOf(bodyField(message.body, 'seriesId')),
      scheduledForUtc: stringOf(bodyField(message.body, 'scheduledForUtc')),
      scheduleLost: false,
      count: 1,
      updatedUtc: toUtc(message.eventTimeUtc),
    };

    this._rows.update(rows => [...rows, entry]);
    this.scheduleWrite();
    if (announce && entry.priority >= MessageEventPriority.Action) this._announcement.set(entry);
  }

  private reconcile({snapshot, requestedAtMs}: ActivitySnapshotResult) {
    if (!this.storageKey) return;

    const runningIds = new Set<string>();
    for (const message of snapshot.running) {
      const id = jobIdOf(message);
      runningIds.add(id);

      const step = this.findJob(id)?.steps[message.name];
      if (step && Date.parse(step.updatedUtc) >= Date.parse(toUtc(message.eventTimeUtc))) continue;
      this.applyStep(id, message, false);
    }

    snapshot.recentJobs.forEach(recent => this.applyRecentJob(recent));
    [...snapshot.recentEntries].reverse().forEach(message => this.replayEntry(message));

    const bootPrefix = `${snapshot.bootId.replace(/-/g, '').slice(0, 8)}.`;
    const startedMs = Date.parse(snapshot.startedUtc);
    const isFromEarlierBoot = (row: ActivityRow) => row.correlationId
      ? !row.correlationId.startsWith(bootPrefix)
      : Date.parse(row.updatedUtc) < startedMs;

    const changed = new Map<string, ActivityRow>();
    for (const row of this._rows()) {
      if (row.kind === ActivityRowKind.Entry) {
        if (!row.scheduleLost && isLostSchedule(row, startedMs)) changed.set(row.id, {...row, scheduleLost: true});
        continue;
      }

      // All steps ended means the finish grace is running, and the tracker drops a step on ended, so an absent job may be mid-scan
      if (row.endedUtc !== null || runningIds.has(row.id) || allStepsEnded(row)) continue;

      if (isFromEarlierBoot(row)) {
        changed.set(row.id, {...row, endedUtc: row.updatedUtc, endReason: ActivityEndReason.Restart, seenFromStart: false});
      } else if ((this.lastFrameMs.get(row.id) ?? 0) < requestedAtMs) {
        changed.set(row.id, {...row, endedUtc: row.updatedUtc, endReason: ActivityEndReason.Away, seenFromStart: false});
      }
    }

    if (changed.size === 0) return;

    changed.forEach((_, id) => this.cancelFinish(id));
    this._rows.update(rows => rows.map(r => changed.get(r.id) ?? r));
    this.scheduleWrite();
  }

  private applyRecentJob(recent: RecentJob) {
    const id = `job:${recent.correlationId}`;
    const job = this.findJob(id);
    const finishedLive = job && job.endedUtc !== null && job.endReason === null && job.seenFromStart;
    if (finishedLive || this.dismissed.some(d => d.id === id)) return;
    if (!job && this.wouldBeEvicted(recent.endedUtc)) return;

    const steps: Record<string, ActivityStep> = {};
    let libraryIds = job?.libraryIds ?? [];
    for (const message of recent.steps) {
      const step = stepOf(message, job?.steps[message.name]);
      steps[message.name] = recent.completed ? {...step, eventType: 'ended'} : step;
      libraryIds = withLibrary(libraryIds, numberOf(bodyField(message.body, 'libraryId')));
    }

    const first = recent.steps[0];
    const endedUtc = toUtc(recent.endedUtc);
    const next: ActivityJob = {
      kind: ActivityRowKind.Job,
      id,
      correlationId: recent.correlationId,
      libraryId: job?.libraryId ?? libraryIds[0] ?? null,
      libraryIds,
      priority: job?.priority ?? (first ? priorityOf(first) : MessageEventPriority.Activity),
      startedUtc: job?.startedUtc ?? toUtc(recent.startedUtc),
      updatedUtc: endedUtc,
      endedUtc,
      endReason: recent.completed ? null : ActivityEndReason.Failed,
      steps: {...job?.steps, ...steps},
      seriesAdded: recent.seriesAdded,
      seriesRemoved: recent.seriesRemoved,
      seenFromStart: true,
    };

    this.cancelFinish(id);
    this._rows.update(rows => job ? rows.map(r => r.id === id ? next : r) : [...rows, next]);
    this.scheduleWrite();
  }

  private replayEntry(message: SignalRMessage) {
    const time = Date.parse(toUtc(message.eventTimeUtc));
    if (time < Date.now() - RowTtlMs || this.wouldBeEvicted(message.eventTimeUtc)) return;

    if (message.name !== EVENTS.ExternalMatchRateLimitError) {
      this.addEntry(message, undefined, false);
      return;
    }

    // One rate limit row folds many hits under the first hit's id, so a dismissal covers every hit up to it
    const prefix = `entry:${message.name}|`;
    const dismissed = this.dismissed.some(d => d.id.startsWith(prefix) && Date.parse(d.dismissedUtc) >= time);
    const seen = this._rows().some(r => r.kind === ActivityRowKind.Entry && r.name === message.name && Date.parse(r.updatedUtc) >= time);
    if (!dismissed && !seen) this.addRateLimit(message, false);
  }

  /**
   * Replaying a row the next write drops for the cap would make it flicker in on every snapshot
   */
  private wouldBeEvicted(eventTimeUtc: string) {
    const rows = this._rows();
    if (rows.length < MaxRows) return false;

    const oldest = Math.min(...rows.filter(r => !isRunning(r)).map(r => Date.parse(r.updatedUtc)));
    return Date.parse(toUtc(eventTimeUtc)) <= oldest;
  }

  private findJob(id: string) {
    return this._rows().find((r): r is ActivityJob => r.id === id && r.kind === ActivityRowKind.Job);
  }

  private cancelPending(throttleKey: string) {
    const pending = this.pendingUpdates.get(throttleKey);
    if (!pending) return;

    clearTimeout(pending.timer);
    this.pendingUpdates.delete(throttleKey);
  }

  private switchUser(userId: number | undefined) {
    const key = userId === undefined ? null : `kavita-activity-${userId}`;
    if (key === this.storageKey) return;

    this.cancelTimers();
    if (key === null && this.storageKey !== null) removeStorage(this.storageKey);

    this.storageKey = key;
    const persisted = key ? readStorage(key) : null;
    this.dismissed = pruneDismissed(persisted?.dismissed ?? []);
    this._rows.set(pruneRows((persisted?.rows ?? []).map(restoreRow)));
    this._announcement.set(null);
  }

  private cancelTimers() {
    clearTimeout(this.writeTimer);
    this.writeTimer = undefined;
    this.pendingUpdates.forEach(p => clearTimeout(p.timer));
    this.pendingUpdates.clear();
    this.lastApplied.clear();
    this.finishTimers.forEach(t => clearTimeout(t));
    this.finishTimers.clear();
    this.lastFrameMs.clear();
  }

  private scheduleWrite() {
    if (!this.storageKey || this.writeTimer) return;

    // A timer that fires after a logout or user switch must not write into the new key
    const key = this.storageKey;
    this.writeTimer = setTimeout(() => {
      this.writeTimer = undefined;
      if (key === this.storageKey) this.write(key);
    }, WriteDebounceMs);
  }

  private flushWrite() {
    if (!this.writeTimer || !this.storageKey) return;

    clearTimeout(this.writeTimer);
    this.writeTimer = undefined;
    this.write(this.storageKey);
  }

  // Every open tab writes its whole row list to the same key, so without merging, one tab's write undoes another's dismiss
  private onStorage(event: StorageEvent) {
    if (!this.storageKey || event.key !== this.storageKey || event.newValue === null) return;
    this.applyDismissed(readStorage(this.storageKey)?.dismissed ?? []);
  }

  private applyDismissed(others: DismissedActivity[]) {
    const known = new Set(this.dismissed.map(d => d.id));
    const rows = new Map(this._rows().map(r => [r.id, r]));
    // A job that started again after the other tab dismissed it stays
    const added = others.filter(d => !known.has(d.id)
      && Date.parse(d.dismissedUtc) >= Date.parse(rows.get(d.id)?.updatedUtc ?? d.dismissedUtc));
    if (added.length === 0) return;

    const ids = new Set(added.map(d => d.id));
    this.dismissed = pruneDismissed([...this.dismissed, ...added]);
    ids.forEach(id => this.cancelFinish(id));
    this._rows.update(rows => rows.filter(r => !ids.has(r.id)));
  }

  private write(key: string) {
    this.applyDismissed(readStorage(key)?.dismissed ?? []);

    const rows = pruneRows(this._rows());
    if (rows.length !== this._rows().length) this._rows.set(rows);

    this.dismissed = pruneDismissed(this.dismissed);
    writeStorage(key, {rows, dismissed: this.dismissed});
  }
}

// No sender does this since CleanupOnHold moved to Info, but one would otherwise become a job that never ends
function isOneOff(message: SignalRMessage) {
  return message.progress !== 'determinate' && message.progress !== 'indeterminate';
}

/**
 * Live and replayed copies of an entry share this id, so a dismissal holds across reconnects
 */
function entryIdOf(message: SignalRMessage) {
  switch (message.name) {
    case EVENTS.UpdateAvailable:
      return `entry:${message.name}|${stringOf(bodyField(message.body, 'updateVersion'))}`;
    case EVENTS.ScrobblingKeyExpired:
      return `entry:${message.name}|${numberOf(bodyField(message.body, 'provider'))}`;
    default:
      return `entry:${message.name}|${message.code ?? message.title}|${message.eventTimeUtc}`;
  }
}

function jobIdOf(message: SignalRMessage) {
  if (message.correlationId) return `job:${message.correlationId}`;
  return `job:${message.name}|${numberOf(bodyField(message.body, 'libraryId')) ?? ''}`;
}

function nextProgress(previous: ActivityStep | undefined, message: SignalRMessage) {
  const value = numberOf(bodyField(message.body, 'progress'));
  if (value === null) return null;

  const clamped = Math.min(Math.max(value, 0), 1);
  const sameRun = previous && message.eventType !== 'started' && previous.eventType !== 'ended' && previous.code === message.code && previous.progress !== null;
  return sameRun ? Math.max(previous.progress!, clamped) : clamped;
}

function stepOf(message: SignalRMessage, previous: ActivityStep | undefined): ActivityStep {
  return {
    name: message.name,
    code: message.code,
    eventType: message.eventType,
    progressType: message.progress,
    title: message.title,
    subTitle: message.subTitle,
    progress: nextProgress(previous, message),
    body: message.body,
    updatedUtc: toUtc(message.eventTimeUtc),
  };
}

function withLibrary(libraryIds: number[], libraryId: number | null) {
  return libraryId === null || libraryIds.includes(libraryId) ? libraryIds : [...libraryIds, libraryId];
}

function priorityOf(message: SignalRMessage): MessageEventPriority {
  if (message.priority !== null && message.priority !== undefined) return message.priority;

  if (message.name === EVENTS.Error) return MessageEventPriority.Error;
  if (message.name === EVENTS.Info) return MessageEventPriority.Info;
  if (ActionNames.includes(message.name)) return MessageEventPriority.Action;
  return isOneOff(message) ? MessageEventPriority.Silent : MessageEventPriority.Activity;
}

function numberOf(value: unknown) {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function stringOf(value: unknown) {
  return typeof value === 'string' ? value : null;
}

function bodyField(body: unknown, key: string): unknown {
  return typeof body === 'object' && body !== null ? (body as Record<string, unknown>)[key] : undefined;
}

function toUtc(value: string | undefined) {
  const time = value ? Date.parse(value) : NaN;
  return (Number.isNaN(time) ? new Date() : new Date(time)).toISOString();
}

function allStepsEnded(job: ActivityJob) {
  return Object.values(job.steps).every(s => s.eventType === 'ended');
}

/**
 * Fills fields added after the row was stored. A job that was mid-scan when the page unloaded missed frames, so its
 * series counts are partial, and one that was waiting out the finish grace is finished
 */
function restoreRow(row: ActivityRow): ActivityRow {
  if (row.kind === ActivityRowKind.Entry) return {...row, count: row.count ?? 1, scheduleLost: row.scheduleLost ?? false};

  const job: ActivityJob = {
    ...row,
    libraryIds: row.libraryIds ?? (row.libraryId === null ? [] : [row.libraryId]),
    seriesAdded: row.seriesAdded ?? 0,
    seriesRemoved: row.seriesRemoved ?? 0,
    endReason: row.endReason ?? null,
    seenFromStart: row.endedUtc !== null && (row.seenFromStart ?? false),
  };
  return job.endedUtc === null && allStepsEnded(job) ? {...job, endedUtc: job.updatedUtc} : job;
}

/**
 * Misses a scan whose time fell while the server was down, the client cannot know when the old boot stopped
 */
function isLostSchedule(entry: ActivityEntry, startedMs: number) {
  return DelayedScanCodes.includes(entry.code)
    && entry.scheduledForUtc !== null
    && Date.parse(entry.updatedUtc) < startedMs
    && Date.parse(entry.scheduledForUtc) > startedMs;
}

/**
 * A stopped job still needs a Rescan, so Clear finished leaves it
 */
export function isFinished(row: ActivityRow): boolean {
  return row.kind === ActivityRowKind.Job && row.endedUtc !== null && !isStopped(row);
}

export function isStopped(row: ActivityRow): row is ActivityJob {
  return row.kind === ActivityRowKind.Job
    && (row.endReason === ActivityEndReason.Restart || row.endReason === ActivityEndReason.Failed);
}

function isRunning(row: ActivityRow) {
  return row.kind === ActivityRowKind.Job && row.endedUtc === null;
}

function pruneRows(rows: ActivityRow[]) {
  const cutoff = Date.now() - RowTtlMs;
  const fresh = rows.filter(r => Date.parse(r.updatedUtc) >= cutoff);
  if (fresh.length <= MaxRows) return fresh;

  const evictable = fresh
    .filter(r => !isRunning(r))
    .sort((a, b) => Date.parse(a.updatedUtc) - Date.parse(b.updatedUtc))
    .slice(0, fresh.length - MaxRows)
    .map(r => r.id);

  return fresh.filter(r => !evictable.includes(r.id));
}

function pruneDismissed(dismissed: DismissedActivity[]) {
  const cutoff = Date.now() - RowTtlMs;
  return dismissed.filter(d => Date.parse(d.dismissedUtc) >= cutoff);
}

function readStorage(key: string): PersistedActivity | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as PersistedActivity;
    if (!Array.isArray(parsed?.rows) || !Array.isArray(parsed?.dismissed)) return null;
    return parsed;
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: PersistedActivity) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Storage full or blocked, the widget still works from memory
  }
}

function removeStorage(key: string) {
  try {
    localStorage.removeItem(key);
  } catch {
    // Blocked storage has nothing to remove
  }
}
