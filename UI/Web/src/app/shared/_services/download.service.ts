import {HttpClient} from '@angular/common/http';
import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {Series} from 'src/app/_models/series';
import {environment} from 'src/environments/environment';
import {ConfirmService} from '../confirm.service';
import {Chapter} from 'src/app/_models/chapter';
import {Volume} from 'src/app/_models/volume';
import {asyncScheduler, filter, forkJoin, Observable, of, tap} from 'rxjs';
import {download, Download} from '../_models/download';
import {PageBookmark} from 'src/app/_models/readers/page-bookmark';
import {map, switchMap, throttleTime} from 'rxjs/operators';
import {AccountService} from 'src/app/_services/account.service';
import {BytesPipe} from 'src/app/_pipes/bytes.pipe';
import {translate, TranslocoService} from "@jsverse/transloco";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {SAVER} from "../../_providers/saver.provider";
import {UtilityService} from "./utility.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {NotificationProgressEvent} from "../../_models/events/notification-progress-event";
import {SeriesService} from "../../_services/series.service";
import {DownloadQueueItem, DownloadQueueStatus} from '../_models/download-queue-item';
import {DownloadStorageService} from './download-storage.service';
import {EntityTitleService} from "../../_services/entity-title.service";
import {LibraryService} from "../../_services/library.service";

export const DEBOUNCE_TIME = 100;

const bytesPipe = new BytesPipe();

/**
 * Valid entity types for downloading
 */
export type DownloadEntityType = 'volume' | 'chapter' | 'series' | 'bookmark' | 'logs';
/**
 * Valid entities for downloading. Undefined exclusively for logs.
 */
export type DownloadEntity = Series | Volume | Chapter | PageBookmark[] | undefined;

@Injectable({
  providedIn: 'root'
})
export class DownloadService {

  private readonly entityTitleService = inject(EntityTitleService);
  private readonly libraryService = inject(LibraryService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  private readonly httpClient = inject(HttpClient);
  private readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly seriesService = inject(SeriesService);
  private readonly storage = inject(DownloadStorageService);
  private readonly translocoService = inject(TranslocoService);
  private readonly save = inject(SAVER);

  private baseUrl = environment.apiUrl;
  /**
   * Size in bytes in which to inform the user for confirmation before download starts. Defaults to 100 MB.
   */
  public SIZE_WARNING = 104_857_600;
  /**
   * Size in bytes in which to inform the user that anything above may fail on iOS due to device limits. (200MB)
   */
  private IOS_SIZE_WARNING = 209_715_200;

  /** Set to true to enable verbose download queue logging in the browser console. */
  private readonly debug = true;

  private debugLog(...args: any[]) {
    if (this.debug) console.log('[DownloadService]', ...args);
  }

  // --- Signal-based queue ---
  private _nextId = 0;
  readonly queue = signal<DownloadQueueItem[]>([]);
  readonly activeItem = computed(() =>
    this.queue().find(i => i.status === 'preparing' || i.status === 'downloading') ?? null
  );
  readonly queuedItems = computed(() => this.queue().filter(i => i.status === 'queued'));
  readonly completedItems = computed(() =>
    this.queue().filter(i => i.status === 'completed')
      .sort((a, b) => (b.completedAt ?? 0) - (a.completedAt ?? 0))
  );
  readonly failedItems = computed(() => this.queue().filter(i => i.status === 'failed'));
  readonly totalActiveCount = computed(() =>
    (this.activeItem() ? 1 : 0) + this.queuedItems().length
  );
  readonly hasActiveDownloads = computed(() =>
    this.activeItem() !== null || this.queuedItems().length > 0
  );
  readonly isPaused = signal(false);

  private readonly queue$ = toObservable(this.queue);

  /**
   * Sliding window of recent byte snapshots for smoothed speed calculation.
   * Keeps the last ~8 seconds of samples per item.
   */
  private _speedSamples = new Map<number, Array<{ bytes: number; time: number }>>();
  private readonly SPEED_WINDOW_MS = 8000;
  /** EMA-smoothed speed per item, to dampen rapid fluctuations */
  private _smoothedSpeed = new Map<number, number>();
  private readonly EMA_ALPHA = 0.15;

  constructor() {
    // SignalR handler — only used as a safety net.
    // Real progress comes from fetch + ReadableStream in streamDownload/blobDownload.
    this.messageHub.messages$.pipe(
      filter(evt => evt.event === EVENTS.DownloadProgress),
      map(evt => evt.payload as NotificationProgressEvent),
      tap(evt => {
        this.debugLog(`DownloadProgress type="${evt.eventType}" body=`, evt.body);

        const correlationId: string | undefined = evt.body?.correlationId ?? evt.body?.CorrelationId;
        const downloadName: string | undefined = evt.body?.DownloadName ?? evt.body?.downloadName;

        const active = this.activeItem();
        if (!active) return;

        const isMatch = (correlationId && String(active.id) === correlationId)
                     || (!correlationId && downloadName === active.downloadName);
        if (!isMatch) return;

        if (evt.eventType === 'started') {
          this.debugLog(`DownloadProgress started for id=${active.id}`);
        } else if (evt.eventType === 'ended') {
          // Safety net: if the stream somehow missed completion, mark it done
          if (active.status !== 'completed') {
            this.debugLog(`DownloadProgress ended (fallback) for id=${active.id}`);
          }
        }
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

  }

  /**
   * Restores the queue from IndexedDB. Call this after the user is authenticated.
   * Items that were in-progress when the page refreshed are marked as failed.
   */
  restoreQueue() {
    this.storage.open().then(items => {
      const restored = items.map(i =>
        (i.status === 'preparing' || i.status === 'downloading')
          ? { ...i, status: 'failed' as DownloadQueueStatus, errorMessage: this.translocoService.translate('download-queue-drawer.failed-interrupted') }
          : i
      );
      this.queue.set(restored);
      restored.filter(i => i.status === 'failed').forEach(i => this.storage.save(i));
      // Advance _nextId past all restored IDs to prevent ID collisions with new items
      if (restored.length > 0) {
        this._nextId = Math.max(...restored.map(i => i.id)) + 1;
      }
      if (restored.some(i => i.status === 'queued')) {
        this.isPaused.set(true);   // wait for user to hit Resume
      }
    });
  }

  /**
   * Returns the entity subtitle (for the event widget) for a given entity
   */
  downloadSubtitle(downloadEntityType: DownloadEntityType | undefined, downloadEntity: DownloadEntity | undefined) {
    switch (downloadEntityType) {
      case 'series':   return (downloadEntity as Series).name;
      case 'volume':   return (downloadEntity as Volume).minNumber + '';
      case 'chapter':  return (downloadEntity as Chapter).minNumber + '';
      case 'bookmark': return '';
      case 'logs':     return '';
    }
    return '';
  }

  /**
   * Downloads the entity to the user's system.
   * - series → decomposes into volumes/chapters, each queued individually
   * - volume/chapter → size-checked then queued
   * - bookmark/logs → immediate blob download (bypasses queue)
   */
  download(entityType: DownloadEntityType, entity: DownloadEntity, callback?: (d: Download | undefined) => void, libraryId = 0, seriesId = 0) {
    switch (entityType) {
      case 'series':
        this.downloadSeries(entity as Series);
        break;
      case 'volume':
        this.enqueueSingle(entity as Volume, 'volume', '', libraryId, seriesId);
        break;
      case 'chapter':
        this.enqueueSingle(entity as Chapter, 'chapter', '', libraryId, seriesId);
        break;
      case 'bookmark':
        this.downloadBookmarksBlob(entity as PageBookmark[]);
        break;
      case 'logs':
        this.downloadLogsBlob();
        break;
    }
  }


  cancelDownload(itemId: number) {
    const controller = this.activeAbortControllers.get(itemId);
    if (controller) {
      controller.abort();
      this.activeAbortControllers.delete(itemId);
    }
    this.queue.update(q => q.filter(i => i.id !== itemId));
    this.storage.delete(itemId);
    setTimeout(() => this.processQueue(), 100);
  }

  removeItem(id: number) {
    this.queue.update(q => q.filter(i => i.id !== id));
    this.storage.delete(id);
  }

  clearCompleted() {
    const ids = this.queue().filter(i => i.status === 'completed').map(i => i.id);
    this.queue.update(q => q.filter(i => i.status !== 'completed'));
    ids.forEach(id => this.storage.delete(id));
  }

  clearCompletedByIds(ids: number[]) {
    const idSet = new Set(ids);
    this.queue.update(q => q.filter(i => !idSet.has(i.id)));
    ids.forEach(id => this.storage.delete(id));
  }

  retryDownload(itemId: number) {
    const item = this.queue().find(i => i.id === itemId);
    if (!item || item.retryCount >= 3) return;
    const retried = { ...item, status: 'queued' as DownloadQueueStatus, errorMessage: '', retryCount: item.retryCount + 1 };
    // Place retried item at the front of the queue (after any active item)
    this.queue.update(q => {
      const without = q.filter(i => i.id !== itemId);
      const activeIdx = without.findIndex(i => i.status === 'preparing' || i.status === 'downloading');
      const insertAt = activeIdx >= 0 ? activeIdx + 1 : 0;
      return [...without.slice(0, insertAt), retried, ...without.slice(insertAt)];
    });
    this.storage.save(retried);
    this.processQueue();
  }

  cancelAllQueued() {
    const ids = this.queue().filter(i => i.status === 'queued').map(i => i.id);
    this.queue.update(q => q.filter(i => i.status !== 'queued'));
    ids.forEach(id => this.storage.delete(id));
    this.isPaused.set(false);  // don't block fresh downloads after cancelling all queued
  }

  clearAllFailed() {
    const ids = this.queue().filter(i => i.status === 'failed').map(i => i.id);
    this.queue.update(q => q.filter(i => i.status !== 'failed'));
    ids.forEach(id => this.storage.delete(id));
  }

  pauseQueue() {
    this.isPaused.set(true);
  }

  retryAllFailed() {
    this.queue.update(q => {
      const active = q.filter(i => i.status === 'preparing' || i.status === 'downloading');
      const retried = q.filter(i => i.status === 'failed')
        .map(i => ({ ...i, status: 'queued' as DownloadQueueStatus, errorMessage: '', retryCount: i.retryCount + 1 }));
      const existingQueued = q.filter(i => i.status === 'queued');
      const rest = q.filter(i => i.status === 'completed' || i.status === 'cancelled');
      // Retried items go to the front of the queue, before existing queued items
      return [...active, ...retried, ...existingQueued, ...rest];
    });
    this.queue().filter(i => i.status === 'queued').forEach(i => this.storage.save(i));
    this.processQueue();
  }

  /**
   * Returns the active queue item for the given entity, or null if none.
   * Use this for card download indicators.
   */
  getItemForEntity(entity: Series | Volume | Chapter | PageBookmark[], includeCompleted = false): DownloadQueueItem | null {
    const q = this.queue();

    // Series: aggregate across all active + completed items together so progress doesn't drop
    if (this.utilityService.isSeries(entity)) {
      const statuses: DownloadQueueStatus[] = ['queued', 'preparing', 'downloading'];
      if (includeCompleted) statuses.push('completed');
      const items = q.filter(i => statuses.includes(i.status) && i.seriesName === (entity as Series).name);
      return this._aggregateSeriesItems(items);
    }

    // Volume/Chapter: check active first, then completed
    const activeItems = q.filter(i => i.status === 'queued' || i.status === 'preparing' || i.status === 'downloading');
    const active = this._findEntityInList(activeItems, entity);
    if (active) return active;

    if (includeCompleted) {
      return this._findEntityInList(q.filter(i => i.status === 'completed'), entity);
    }
    return null;
  }

  private _findEntityInList(items: DownloadQueueItem[], entity: Series | Volume | Chapter | PageBookmark[]): DownloadQueueItem | null {
    if (this.utilityService.isVolume(entity)) {
      return items.find(i => i.entityType === 'volume' && i.entityId === (entity as Volume).id) ?? null;
    }
    if (this.utilityService.isChapter(entity)) {
      return items.find(i => i.entityType === 'chapter' && i.entityId === (entity as Chapter).id) ?? null;
    }
    return null;
  }

  private _aggregateSeriesItems(items: DownloadQueueItem[]): DownloadQueueItem | null {
    if (items.length === 0) return null;

    const totalProgress = items.reduce((sum, i) => {
      if (i.status === 'completed') return sum + 100;
      if (i.status === 'downloading' || i.status === 'preparing') return sum + i.progress;
      return sum;
    }, 0);

    const allCompleted = items.every(i => i.status === 'completed');
    const representative = items.find(i => i.status === 'downloading')
      ?? items.find(i => i.status === 'preparing')
      ?? items.find(i => i.status === 'queued')
      ?? items[0];

    return {
      ...representative,
      progress: Math.round(totalProgress / items.length),
      status: allCompleted ? 'completed' : representative.status,
    };
  }

  /**
   * Returns an observable of the queue item for the given entity, or null if none.
   * Emits on every queue change. Use this for card download indicators.
   */
  getEntityDownload$(entity: Series | Volume | Chapter | PageBookmark[]): Observable<DownloadQueueItem | null> {
    if (!entity.hasOwnProperty('id')) return of(null);
    return this.queue$.pipe(
      map(() => this.getItemForEntity(entity))
    );
  }

  /**
   * Download the given data as a JSON file
   */
  downloadObjectAsJson(data: any, title: string) {
    const json = JSON.stringify(data, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = title.endsWith('.json') ? title : title + '.json';
    a.click();
    URL.revokeObjectURL(url);
  }

  private downloadSeriesSize(seriesId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/series-size?seriesId=' + seriesId);
  }

  private downloadVolumeSize(volumeId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/volume-size?volumeId=' + volumeId);
  }

  private downloadChapterSize(chapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/chapter-size?chapterId=' + chapterId);
  }

  private downloadSeries(series: Series) {
    this.debugLog('downloadSeries()', series.name);
    this.seriesService.getSeriesDetail(series.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(detail => {
      const items: Array<{ entity: Volume | Chapter; entityType: 'volume' | 'chapter' }> = [
        ...detail.volumes.map(v => ({ entity: v as Volume, entityType: 'volume' as const })),
        ...detail.chapters.map(c => ({ entity: c as Chapter, entityType: 'chapter' as const })),
        ...detail.specials.map(c => ({ entity: c as Chapter, entityType: 'chapter' as const })),
      ];
      this.debugLog(`downloadSeries() decomposed into ${items.length} items (${detail.volumes.length} vols, ${detail.chapters.length + detail.specials.length} chapters)`);

      const userPrefs = this.accountService.userPreferences();
      if (userPrefs?.promptForDownloadSize && items.length > 0) {
        // Single size call for the whole series, single confirm dialog
        this.downloadSeriesSize(series.id).pipe(
          switchMap(async size => this.confirmSize(size, 'series')),
          filter(confirmed => confirmed),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => this.enqueueItems(items, series.name, series.libraryId, series.id));
      } else {
        this.enqueueItems(items, series.name, series.libraryId, series.id);
      }
    });
  }

  private enqueueItems(items: Array<{ entity: Volume | Chapter; entityType: 'volume' | 'chapter' }>, seriesName: string, libraryId: number, seriesId = 0) {
    this.debugLog(`enqueueItems() adding ${items.length} items for series "${seriesName}"`);

    // Fetch individual sizes in parallel, then enqueue with sizes
    const sizeRequests = items.map(item =>
      item.entityType === 'volume'
        ? this.downloadVolumeSize((item.entity as Volume).id)
        : this.downloadChapterSize((item.entity as Chapter).id)
    );

    forkJoin(sizeRequests).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(sizes => {
      for (let i = 0; i < items.length; i++) {
        this.addToQueue(items[i].entity, items[i].entityType, seriesName, libraryId, sizes[i], seriesId);
      }
      this.processQueue();
    });
  }

  private enqueueSingle(entity: Volume | Chapter, entityType: 'volume' | 'chapter', seriesName: string, libraryId: number, seriesId = 0) {
    const user = this.accountService.currentUser();
    const sizeCall = entityType === 'volume'
      ? this.downloadVolumeSize((entity as Volume).id)
      : this.downloadChapterSize((entity as Chapter).id);

    // Always fetch size to populate estimatedSize; only prompt if user preference is set
    sizeCall.pipe(
      switchMap(async size => {
        const promptForSize = user && user.preferences.promptForDownloadSize;
        const confirmed = promptForSize ? await this.confirmSize(size, entityType) : true;
        return { size, confirmed };
      }),
      filter(result => result.confirmed),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ size }) => {
      this.addToQueue(entity, entityType, seriesName, libraryId, size, seriesId);
      this.processQueue();
    });
  }

  private addToQueue(entity: Volume | Chapter, entityType: 'volume' | 'chapter', seriesName: string, libraryId: number, estimatedSize = 0, seriesId = 0) {
    const id = this._nextId++;
    const entityId = entity.id;
    this.debugLog(`addToQueue() id=${id} type=${entityType} entityId=${entityId} series="${seriesName}"`);

    const libraryType = this.libraryService.getLibraryTypeSync(libraryId) ?? 0;
    const entityTitle = this.entityTitleService.computeTitle(entity, libraryType, { prioritizeTitleName: true });
    const label = seriesName ? `${seriesName} - ${entityTitle}` : entityTitle;

    let subLabel: string;
    let downloadName: string;

    if (entityType === 'volume') {
      const vol = entity as Volume;
      subLabel = vol.minNumber + '';
      downloadName = seriesName ? `${seriesName} - Volume ${vol.name}` : `Volume ${vol.name}`;
    } else {
      const ch = entity as Chapter;
      subLabel = ch.minNumber + '';
      downloadName = seriesName ? `${seriesName} - Chapter ${ch.minNumber}` : `Chapter ${ch.minNumber}`;
    }

    const item: DownloadQueueItem = {
      id,
      entityType,
      entityId,
      libraryId,
      seriesId,
      label,
      subLabel,
      seriesName,
      estimatedSize,
      status: 'queued',
      progress: 0,
      errorMessage: '',
      retryCount: 0,
      queuedAt: Date.now(),
      entity,
      downloadName,
    };

    this.queue.update(q => [...q, item]);
    this.storage.save(item);
  }

  resumeQueue() {
    this.isPaused.set(false);
    this.processQueue();
  }

  private processQueue() {
    if (this.isPaused()) return;
    if (this.activeItem()) {
      this.debugLog('processQueue() — already active, skipping');
      return;
    }

    const nextItem = this.queue().find(i => i.status === 'queued');
    if (!nextItem) {
      this.debugLog('processQueue() — queue empty, nothing to do');
      return;
    }

    this.debugLog(`processQueue() — starting item id=${nextItem.id} "${nextItem.label}"`);
    this.setStatus(nextItem.id, 'preparing');
    this.triggerDownload(nextItem);
  }

  /** Active AbortControllers keyed by item id, for cancellation support */
  private activeAbortControllers = new Map<number, AbortController>();

  private triggerDownload(item: DownloadQueueItem) {
    const apiKey = this.accountService.currentUserGenericApiKey();
    if (!apiKey) {
      this.debugLog(`triggerDownload() — no API key for id=${item.id}`);
      this.setStatus(item.id, 'failed', { errorMessage: this.translocoService.translate('download-queue-drawer.failed-from-auth') });
      return;
    }

    const idKey = item.entityType === 'volume' ? 'volumeId' : 'chapterId';
    const url = `${this.baseUrl}download/${item.entityType}` +
                `?${idKey}=${item.entityId}` +
                `&correlationId=${item.id}` +
                `&_t=${Date.now()}` +
                `&apiKey=${encodeURIComponent(apiKey)}`;

    this.debugLog(`triggerDownload() id=${item.id} url=${url}`);
    this.fetchDownload(item, url);
  }

  /**
   * Download using fetch + ReadableStream for real byte-level progress, then saveAs via blob.
   */
  private async fetchDownload(item: DownloadQueueItem, url: string) {
    const abortController = new AbortController();
    this.activeAbortControllers.set(item.id, abortController);

    try {
      const response = await fetch(url, { signal: abortController.signal });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      if (!response.body) throw new Error('No response body');

      const contentLength = +(response.headers.get('Content-Length') || 0);
      const filename = this.parseContentDisposition(
        response.headers.get('Content-Disposition') || '', item.downloadName
      );

      this.setStatus(item.id, 'downloading');

      const reader = response.body.getReader();
      const chunks: BlobPart[] = [];
      let received = 0;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
        received += value.length;

        if (contentLength > 0) {
          const now = Date.now();
          const progress = Math.round((received / contentLength) * 100);

          // Sliding-window speed: keep samples from the last SPEED_WINDOW_MS
          let samples = this._speedSamples.get(item.id);
          if (!samples) {
            samples = [];
            this._speedSamples.set(item.id, samples);
          }
          samples.push({ bytes: received, time: now });
          const cutoff = now - this.SPEED_WINDOW_MS;
          while (samples.length > 1 && samples[0].time < cutoff) {
            samples.shift();
          }

          let speedBps: number | undefined;
          let etaSeconds: number | undefined;
          if (samples.length >= 2) {
            const oldest = samples[0];
            const timeDelta = (now - oldest.time) / 1000;
            const bytesDelta = received - oldest.bytes;
            if (timeDelta > 0) {
              const rawSpeed = bytesDelta / timeDelta;
              const prev = this._smoothedSpeed.get(item.id);
              speedBps = prev !== undefined
                ? this.EMA_ALPHA * rawSpeed + (1 - this.EMA_ALPHA) * prev
                : rawSpeed;
              this._smoothedSpeed.set(item.id, speedBps);
              const remaining = contentLength - received;
              etaSeconds = speedBps > 0 ? Math.round(remaining / speedBps) : undefined;
            }
          }

          this.queue.update(q => q.map(i =>
            i.id === item.id
              ? { ...i, progress,
                  ...(speedBps !== undefined ? { speedBps } : {}),
                  ...(etaSeconds !== undefined ? { etaSeconds } : {}) }
              : i
          ));
        }
      }

      const blob = new Blob(chunks);
      chunks.length = 0; // release chunk references before saveAs to halve peak memory
      this.save(blob, filename);
      this.activeAbortControllers.delete(item.id);
      this.markCompleted(item.id);
    } catch (err: any) {
      this.activeAbortControllers.delete(item.id);
      if (err.name === 'AbortError') {
        this.debugLog(`blobDownload() cancelled for id=${item.id}`);
      } else {
        this.markFailed(item.id, err.message || 'Download failed');
      }
    }
  }

  /**
   * Parse Content-Disposition header to extract filename, with fallback.
   */
  private parseContentDisposition(header: string, fallbackName: string): string {
    if (!header) return fallbackName || 'download';
    const tokens = header.split(';');
    if (tokens.length < 2) return fallbackName || 'download';
    let filename = tokens[1].replace('filename=', '').replace(/"/ig, '').trim();
    if (filename.startsWith('download_') || filename.startsWith('kavita_download_')) {
      const ext = filename.substring(filename.lastIndexOf('.'), filename.length);
      if (fallbackName) return fallbackName + ext;
      return filename.replace('kavita_', '').replace('download_', '');
    }
    return decodeURIComponent(filename) || fallbackName || 'download';
  }

  /** Updates queue signal and persists to IDB on status changes. */
  private setStatus(id: number, status: DownloadQueueStatus, extra?: Partial<DownloadQueueItem>) {
    this.queue.update(q => q.map(i => i.id === id ? { ...i, status, ...extra } : i));
    const item = this.queue().find(i => i.id === id);
    if (item) this.storage.save(item);
  }

  /**
   * Computes aggregate download progress for all items belonging to a series.
   * Returns a synthetic DownloadQueueItem with averaged progress, or null if no active items.
   */
  private getSeriesDownloadProgress(seriesName: string): DownloadQueueItem | null {
    const allItems = this.queue().filter(i =>
      i.seriesName === seriesName &&
      i.status !== 'cancelled' && i.status !== 'failed'
    );
    if (allItems.length === 0) return null;

    const hasActive = allItems.some(i =>
      i.status === 'queued' || i.status === 'preparing' || i.status === 'downloading'
    );
    if (!hasActive) return null;

    const totalProgress = allItems.reduce((sum, i) => {
      if (i.status === 'completed') return sum + 100;
      if (i.status === 'downloading' || i.status === 'preparing') return sum + i.progress;
      return sum; // queued = 0
    }, 0);

    const representative = allItems.find(i => i.status === 'downloading')
      ?? allItems.find(i => i.status === 'preparing')
      ?? allItems.find(i => i.status === 'queued')!;

    return { ...representative, progress: Math.round(totalProgress / allItems.length) };
  }

  private markCompleted(itemId: number) {
    this.debugLog(`markCompleted() id=${itemId}`);
    this._speedSamples.delete(itemId);
    this._smoothedSpeed.delete(itemId);
    this.setStatus(itemId, 'completed', { progress: 100, completedAt: Date.now() });
    setTimeout(() => this.removeItem(itemId), 5 * 60 * 1000);
    // Give GC time to reclaim the previous download's blob before starting the next one
    setTimeout(() => this.processQueue(), 1500);
  }

  private markFailed(itemId: number, error: string) {
    this.debugLog(`markFailed() id=${itemId} error="${error}"`);
    this._speedSamples.delete(itemId);
    this._smoothedSpeed.delete(itemId);
    this.setStatus(itemId, 'failed', { errorMessage: error, completedAt: Date.now() });
    // Give GC time to reclaim memory before starting next download
    setTimeout(() => this.processQueue(), 1500);
  }

  // --- Blob-based downloads (bookmarks, logs) ---

  private downloadBookmarksBlob(bookmarks: PageBookmark[]) {
    this.httpClient.post(this.baseUrl + 'download/bookmarks', { bookmarks },
      { observe: 'events', responseType: 'blob', reportProgress: true }
    ).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  private downloadLogsBlob() {
    this.httpClient.get(this.baseUrl + 'server/logs',
      { observe: 'events', responseType: 'blob', reportProgress: true }
    ).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  private async confirmSize(size: number, entityType: DownloadEntityType) {
    const showIosWarning = size > this.IOS_SIZE_WARNING && /iPad|iPhone|iPod/.test(navigator.userAgent);
    return (size < this.SIZE_WARNING ||
      await this.confirmService.confirm(translate('toasts.confirm-download-size',
        { entityType: translate('entity-type.' + entityType), size: bytesPipe.transform(size) })
        + (!showIosWarning ? '' : '<br/><br/>' + translate('toasts.confirm-download-size-ios'))));
  }
}
