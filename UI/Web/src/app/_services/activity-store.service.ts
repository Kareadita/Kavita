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

const RowTtlMs = 4 * 60 * 60 * 1000;
const MaxRows = 100;
const TextThrottleMs = 750;
const WriteDebounceMs = 1000;

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
  private readonly destroyRef = inject(DestroyRef);

  private _rows = signal<ActivityRow[]>([]);
  private dismissed: DismissedActivity[] = [];
  private storageKey: string | null = null;
  private writeTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly pendingUpdates = new Map<string, PendingUpdate>();
  private readonly lastApplied = new Map<string, number>();

  /**
   * Newest first
   */
  readonly rows = computed(() => [...this._rows()].sort((a, b) => Date.parse(b.updatedUtc) - Date.parse(a.updatedUtc)));
  readonly runningJobs = computed(() => this._rows()
    .filter((r): r is ActivityJob => r.kind === ActivityRowKind.Job && r.endedUtc === null));

  constructor() {
    effect(() => {
      const userId = this.accountService.userId();
      untracked(() => this.switchUser(userId));
    });

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(message => this.ingest(message));

    // pagehide fires on refresh, tab close and navigating away, so the pending write is not lost. Unlike beforeunload it also fires on mobile
    const flush = () => this.flushWrite();
    window.addEventListener('pagehide', flush);
    this.destroyRef.onDestroy(() => window.removeEventListener('pagehide', flush));
  }

  dismiss(id: string) {
    this._rows.update(rows => rows.filter(r => r.id !== id));
    this.dismissed = [...this.dismissed.filter(d => d.id !== id), {id, dismissedUtc: new Date().toISOString()}];
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
        if (message.meta) this.addEntry({...message.meta, body: message.payload});
        break;
    }
  }

  private ingestProgress(message: SignalRMessage) {
    if (isOneOff(message)) {
      this.addEntry(message);
      return;
    }

    const id = jobIdOf(message);
    const throttleKey = `${id}|${message.name}`;

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

  private applyStep(id: string, message: SignalRMessage) {
    const job = this.findJob(id);
    const previous = job?.steps[message.name];
    const isEnded = message.eventType === 'ended';

    // An ended never creates or reopens a job (ScanProgress sends ended twice per scan, plan Phase 13b)
    if (isEnded && (!previous || previous.eventType === 'ended')) return;
    if (!isEnded) this.dismissed = this.dismissed.filter(d => d.id !== id);

    const updatedUtc = toUtc(message.eventTimeUtc);
    const step: ActivityStep = isEnded
      ? {...previous!, eventType: 'ended', updatedUtc}
      : {
        name: message.name,
        code: message.code,
        eventType: message.eventType,
        progressType: message.progress,
        title: message.title,
        subTitle: message.subTitle,
        progress: nextProgress(previous, message),
        body: message.body,
        updatedUtc,
      };

    const steps = {...job?.steps, [message.name]: step};
    const allEnded = Object.values(steps).every(s => s.eventType === 'ended');

    const next: ActivityJob = {
      kind: ActivityRowKind.Job,
      id,
      correlationId: message.correlationId,
      libraryId: job?.libraryId ?? numberOf(bodyField(message.body, 'libraryId')),
      priority: job?.priority ?? priorityOf(message),
      startedUtc: job?.startedUtc ?? updatedUtc,
      updatedUtc,
      endedUtc: allEnded ? updatedUtc : null,
      steps,
    };

    this._rows.update(rows => job ? rows.map(r => r.id === id ? next : r) : [...rows, next]);
    this.scheduleWrite();
  }

  private addEntry(message: SignalRMessage) {
    const id = `entry:${message.name}|${message.code ?? message.title}|${message.eventTimeUtc}`;
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
      updatedUtc: toUtc(message.eventTimeUtc),
    };

    this._rows.update(rows => [...rows, entry]);
    this.scheduleWrite();
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
    this._rows.set(pruneRows(persisted?.rows ?? []));
  }

  private cancelTimers() {
    clearTimeout(this.writeTimer);
    this.writeTimer = undefined;
    this.pendingUpdates.forEach(p => clearTimeout(p.timer));
    this.pendingUpdates.clear();
    this.lastApplied.clear();
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

  private write(key: string) {
    const rows = pruneRows(this._rows());
    if (rows.length !== this._rows().length) this._rows.set(rows);

    this.dismissed = pruneDismissed(this.dismissed);
    writeStorage(key, {rows, dismissed: this.dismissed});
  }
}

/**
 * CleanupOnHold is an Error sent on NotificationProgress as 'started' with no progress (plan Phase 13a)
 */
function isOneOff(message: SignalRMessage) {
  return message.progress !== 'determinate' && message.progress !== 'indeterminate';
}

function jobIdOf(message: SignalRMessage) {
  if (message.correlationId) return `job:${message.correlationId}`;
  return `job:${message.name}|${numberOf(bodyField(message.body, 'libraryId')) ?? ''}`;
}

function nextProgress(previous: ActivityStep | undefined, message: SignalRMessage) {
  const value = numberOf(bodyField(message.body, 'progress'));
  if (value === null) return null;

  const clamped = Math.min(Math.max(value, 0), 1);
  const sameRun = previous && message.eventType !== 'started' && previous.code === message.code && previous.progress !== null;
  return sameRun ? Math.max(previous.progress!, clamped) : clamped;
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
