import {HttpClient} from '@angular/common/http';
import {computed, DestroyRef, effect, inject, Injectable, signal} from '@angular/core';
import {Series} from 'src/app/_models/series';
import {environment} from 'src/environments/environment';
import {ConfirmService} from '../confirm.service';
import {Chapter} from 'src/app/_models/chapter';
import {Volume} from 'src/app/_models/volume';
import {asyncScheduler, filter, firstValueFrom, forkJoin, of, tap} from 'rxjs';
import {download, parseContentDisposition} from '../_models/download';
import {PageBookmark} from 'src/app/_models/readers/page-bookmark';
import {map, switchMap, throttleTime} from 'rxjs/operators';
import {AccountService} from 'src/app/_services/account.service';
import {BytesPipe} from 'src/app/_pipes/bytes.pipe';
import {translate, TranslocoService} from "@jsverse/transloco";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {SAVER} from "../../_providers/saver.provider";
import {UtilityService} from "./utility.service";
import {DateTime} from 'luxon';
import {UtcToLocalDatePipe} from '../../_pipes/utc-to-locale-date.pipe';
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {NotificationProgressEvent} from "../../_models/events/notification-progress-event";
import {SeriesService} from "../../_services/series.service";
import {
  DistilledDownloadEntityType,
  DownloadEntityType,
  DownloadQueueItem,
  DownloadQueueStatus
} from '../_models/download-queue-item';
import {DownloadStorageService} from './download-storage.service';
import {normalizeTimestamp} from "../../../libs/download-timestamp";
import {ReadingList, ReadingListItem} from "../../_models/reading-list/reading-list";
import {ReadingListService} from "../../_services/reading-list.service";
import {UserCollection} from "../../_models/collection-tag";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {EntityTitleService} from "../../_services/entity-title.service";
import {LibraryService} from "../../_services/library.service";
import NoSleep from "nosleep.js";

export const DEBOUNCE_TIME = 100;

const bytesPipe = new BytesPipe();

/** Valid entities for downloading. Undefined exclusively for logs */
export type DownloadEntity = Series | Volume | Chapter | PageBookmark[] | ReadingList | ReadingListItem | UserCollection | undefined;

@Injectable({
  providedIn: 'root'
})
export class DownloadService {

  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  private readonly httpClient = inject(HttpClient);
  private readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly seriesService = inject(SeriesService);
  private readonly readingListService = inject(ReadingListService);
  private readonly storage = inject(DownloadStorageService);
  private readonly translocoService = inject(TranslocoService);
  private readonly save = inject(SAVER);
  private readonly entityTitleService = inject(EntityTitleService);
  private readonly libraryService = inject(LibraryService);

  private readonly SERIES_NAME_CACHE_MAX = 50;
  private _seriesNameCache = new Map<number, string>();
  private noSleep: NoSleep = new NoSleep();

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
  private readonly debug = false;

  private debugLog(...args: any[]) {
    if (this.debug) console.log('[DownloadService]', ...args);
  }

  // --- Signal-based queue (split: active vs completed) ---
  private _nextId = 0;
  /** Items that are queued/preparing/downloading/failed, mutated frequently during downloads */
  readonly activeQueue = signal<DownloadQueueItem[]>([]);
  /** Completed items from today's session, mutated only when a download finishes */
  readonly completedToday = signal<DownloadQueueItem[]>([]);

  // O(1) lookup for active items by entityType:entityId
  private _activeIndex = new Map<string, DownloadQueueItem>();
  // O(1) lookup: has this entity EVER been downloaded (completed, possibly older)?
  private _completedEntityIds = new Set<string>();

  private _indexKey(entityType: string, entityId: number): string {
    return `${entityType}:${entityId}`;
  }

  private _rebuildActiveIndex() {
    this._activeIndex.clear();
    for (const item of this.activeQueue()) {
      this._activeIndex.set(this._indexKey(item.entityType, item.entityId), item);
    }
  }

  /** Targeted single-item update: produces new signal array but patches the index incrementally. */
  private _updateItem(id: number, patch: Partial<DownloadQueueItem>) {
    const target = this.activeQueue().find(i => i.id === id);
    if (!target) return;

    const updated = { ...target, ...patch };
    this.activeQueue.update(q => q.map(i => i.id === id ? updated : i));
    this._activeIndex.set(this._indexKey(updated.entityType, updated.entityId), updated);
  }

  readonly activeItem = computed(() =>
    this.activeQueue().find(i => i.status === 'preparing' || i.status === 'downloading') ?? null
  );
  readonly queuedItems = computed(() => this.activeQueue().filter(i => i.status === 'queued'));
  readonly completedItems = computed(() =>
    [...this.completedToday()].sort((a, b) => normalizeTimestamp(b.completedAt).localeCompare(normalizeTimestamp(a.completedAt)))
  );
  readonly completedTodayCount = computed(() => this.completedToday().length);
  readonly failedItems = computed(() => this.activeQueue().filter(i => i.status === 'failed'));
  readonly totalActiveCount = computed(() =>
    (this.activeItem() ? 1 : 0) + this.queuedItems().length
  );
  readonly hasActiveDownloads = computed(() =>
    this.activeItem() !== null || this.queuedItems().length > 0
  );
  readonly isPaused = signal(false);

  // Older completed items (lazy-loaded from IDB)
  readonly _olderCompletedCount = signal(0);
  readonly olderCompletedCount = this._olderCompletedCount.asReadonly();
  private _olderItems = signal<DownloadQueueItem[]>([]);
  readonly olderCompletedItems = this._olderItems.asReadonly();
  private _olderLoaded = false;

  private readonly activeQueue$ = toObservable(this.activeQueue);

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
    // SignalR handler, only used as a safety net
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

    //
    effect(() => {
      const hasActiveDownloads = this.hasActiveDownloads();
      if (hasActiveDownloads) {
        this.noSleep.enable().catch(err => console.error(err));
        return;
      }

      this.noSleep.disable();
    });
  }

  /**
   * Restores the queue from IndexedDB. Call this after the user is authenticated.
   * Items that were in-progress when the page refreshed are marked as failed.
   */
  restoreQueue() {
    this.storage.open().then(items => {
      const startOfDayIso = this.getStartOfDay();

      // Mark interrupted items as failed
      const processed = items.map(i =>
        (i.status === 'preparing' || i.status === 'downloading')
          ? { ...i, status: 'failed' as DownloadQueueStatus, errorMessage: this.translocoService.translate('download-queue-drawer.failed-interrupted') }
          : i
      );

      // Split: active (non-completed) + today's completed → signals
      const active = processed.filter(i => i.status !== 'completed');
      const todayCompleted = processed.filter(i =>
        i.status === 'completed' && normalizeTimestamp(i.completedAt) >= startOfDayIso
      );
      const olderCompleted = processed.filter(i =>
        i.status === 'completed' && normalizeTimestamp(i.completedAt) < startOfDayIso
      );

      this.activeQueue.set(active);
      this.completedToday.set(todayCompleted);
      this._rebuildActiveIndex();

      // Populate the completed-entity-ID set (today + older)
      for (const item of [...todayCompleted, ...olderCompleted]) {
        this._completedEntityIds.add(this._indexKey(item.entityType, item.entityId));
      }

      this._olderCompletedCount.set(olderCompleted.length);

      // Persist failed items
      active.filter(i => i.status === 'failed').forEach(i => this.storage.save(i));

      // Advance _nextId past all restored IDs to prevent ID collisions with new items
      if (processed.length > 0) {
        this._nextId = Math.max(...processed.map(i => i.id)) + 1;
      }
      if (active.some(i => i.status === 'queued')) {
        this.isPaused.set(true);   // wait for user to hit Resume
      }
    });
  }

  /**
   * Returns the entity subtitle (for the event widget) for a given entity
   */
  downloadSubtitle(downloadEntityType: DownloadEntityType | undefined, downloadEntity: DownloadEntity | undefined) {
    switch (downloadEntityType) {
      case DownloadEntityType.Series:   return (downloadEntity as Series).name;
      case DownloadEntityType.Volume:   return (downloadEntity as Volume).minNumber + '';
      case DownloadEntityType.Chapter:  return (downloadEntity as Chapter).minNumber + '';
      case DownloadEntityType.Bookmark: return '';
      case DownloadEntityType.Logs:     return '';
      case DownloadEntityType.ReadingListItem: return (downloadEntity as ReadingListItem).title;
    }
    return '';
  }

  /**
   * Downloads the entity to the user's system.
   * - series → decomposes into volumes/chapters, each queued individually
   * - volume/chapter → size-checked then queued
   * - bookmark/logs → immediate blob download (bypasses queue)
   */
  download(entityType: DownloadEntityType, entity: DownloadEntity, libraryId: number, seriesId: number) {
    switch (entityType) {
      case DownloadEntityType.Series:
        this.downloadSeries(entity as Series);
        break;
      case DownloadEntityType.Volume:
        this.downloadVolume(entity as Volume, libraryId, seriesId);
        break;
      case DownloadEntityType.Chapter:
        this.enqueueSingle(entity as Chapter, DownloadEntityType.Chapter, '', libraryId, seriesId);
        break;
      case DownloadEntityType.Bookmark:
        this.downloadBookmarksBlob(entity as PageBookmark[]);
        break;
      case DownloadEntityType.Logs:
        this.downloadLogsBlob();
        break;
      case DownloadEntityType.ReadingList:
        this.downloadReadingList(entity as ReadingList);
        break;
      case DownloadEntityType.Collection:
        this.downloadCollection(entity as UserCollection);
        break;
    }
  }


  /**
   * Downloads multiple volumes and chapters in bulk, using only 2 HTTP size calls total.
   */
  downloadBulk(volumes: Volume[], chapters: Chapter[], libraryId = 0, seriesId = 0) {
    const items: Array<{ entity: Volume | Chapter; entityType: DownloadEntityType.Volume | DownloadEntityType.Chapter }> = [
      ...volumes.map(v => ({ entity: v as Volume, entityType: DownloadEntityType.Volume as const })),
      ...chapters.map(c => ({ entity: c as Chapter, entityType: DownloadEntityType.Chapter as const })),
    ];
    if (items.length === 0) return;
    this.enqueueItems(items, '', libraryId, seriesId);
  }

  cancelDownload(itemId: number) {
    const controller = this.activeAbortControllers.get(itemId);
    if (controller) {
      controller.abort();
      this.activeAbortControllers.delete(itemId);
    }
    this.activeQueue.update(q => q.filter(i => i.id !== itemId));
    this._rebuildActiveIndex();
    this.storage.delete(itemId);
    setTimeout(() => this.processQueue(), 100);
  }

  removeItem(item: DownloadQueueItem) {
    const id = item.id;

    // Check activeQueue first, then completedToday
    if (this.activeQueue().some(i => i.id === id)) {
      this.activeQueue.update(q => q.filter(i => i.id !== id));
      this._rebuildActiveIndex();
    } else {
      this.completedToday.update(q => q.filter(i => i.id !== id));
      this._olderItems.update(q => q.filter(i => i.id !== id));
    }

    // Only remove from _completedEntityIds if all instances of the item have been removed
    if (!this.completedToday().some(i => i.entityId === item.entityId && i.entityType === item.entityType)
    && !this._olderItems().some(i => i.entityId === item.entityId && i.entityType === item.entityType)
    ) {
      this._completedEntityIds.delete(this._indexKey(item.entityType, item.entityId));
    }

    this.storage.delete(id);
  }

  clearCompleted() {
    const items = this.completedToday();
    this.completedToday.set([]);
    for (const item of items) {
      this._completedEntityIds.delete(this._indexKey(item.entityType, item.entityId));
      this.storage.delete(item.id);
    }
  }

  clearCompletedByIds(ids: number[]) {
    const idSet = new Set(ids);
    // Remove matching IDs from completedToday and update entity ID set
    const removed = this.completedToday().filter(i => idSet.has(i.id));
    this.completedToday.update(q => q.filter(i => !idSet.has(i.id)));
    for (const item of removed) {
      this._completedEntityIds.delete(this._indexKey(item.entityType, item.entityId));
    }
    ids.forEach(id => this.storage.delete(id));
  }

  loadOlderCompleted() {
    if (this._olderLoaded) return;
    this._olderLoaded = true;
    const startOfDayIso = this.getStartOfDay();
    this.storage.getCompletedBefore(startOfDayIso).then(items => {
      this._olderItems.set(items.sort((a, b) => normalizeTimestamp(b.completedAt).localeCompare(normalizeTimestamp(a.completedAt))));
    });
  }

  clearOlderCompleted() {
    // Clear in-memory state (this may not be loaded as it's lazy-loaded)
    const loadedItems = this._olderItems();
    this._olderItems.set([]);
    this._olderCompletedCount.set(0);
    this._olderLoaded = false;
    for (const item of loadedItems) {
      this._completedEntityIds.delete(this._indexKey(item.entityType, item.entityId));
    }

    // Delete from storage directly in case _olderItems wasn't loaded yet
    const startOfDayIso = this.getStartOfDay();
    this.storage.getCompletedBefore(startOfDayIso).then(items => {
      for (const item of items) {
        this.storage.delete(item.id);
        this._completedEntityIds.delete(this._indexKey(item.entityType, item.entityId));
      }
    });
  }

  retryDownload(itemId: number) {
    const item = this.activeQueue().find(i => i.id === itemId);
    if (!item || item.retryCount >= 3) return;
    const retried = { ...item, status: 'queued' as DownloadQueueStatus, errorMessage: '', retryCount: item.retryCount + 1 };
    // Place retried item at the front of the queue (after any active item)
    this.activeQueue.update(q => {
      const without = q.filter(i => i.id !== itemId);
      const activeIdx = without.findIndex(i => i.status === 'preparing' || i.status === 'downloading');
      const insertAt = activeIdx >= 0 ? activeIdx + 1 : 0;
      return [...without.slice(0, insertAt), retried, ...without.slice(insertAt)];
    });
    this._rebuildActiveIndex();
    this.storage.save(retried);
    this.processQueue();
  }

  cancelAllQueued() {
    const ids = this.activeQueue().filter(i => i.status === 'queued').map(i => i.id);
    this.activeQueue.update(q => q.filter(i => i.status !== 'queued'));
    this._rebuildActiveIndex();
    ids.forEach(id => this.storage.delete(id));
    this.isPaused.set(false);  // don't block fresh downloads after cancelling all queued
  }

  clearAllFailed() {
    const ids = this.activeQueue().filter(i => i.status === 'failed').map(i => i.id);
    this.activeQueue.update(q => q.filter(i => i.status !== 'failed'));
    this._rebuildActiveIndex();
    ids.forEach(id => this.storage.delete(id));
  }

  pauseQueue() {
    this.isPaused.set(true);
  }

  retryAllFailed() {
    this.activeQueue.update(q => {
      const active = q.filter(i => i.status === 'preparing' || i.status === 'downloading');
      const retried = q.filter(i => i.status === 'failed' && i.retryCount < 3)
        .map(i => ({ ...i, status: 'queued' as DownloadQueueStatus, errorMessage: '', retryCount: i.retryCount + 1 }));
      const remainingFailed = q.filter(i => i.status === 'failed' && i.retryCount >= 3);
      const existingQueued = q.filter(i => i.status === 'queued');
      // Retried items go to the front of the queue, before existing queued items
      return [...active, ...retried, ...existingQueued, ...remainingFailed];
    });
    this._rebuildActiveIndex();
    this.activeQueue().filter(i => i.status === 'queued').forEach(i => this.storage.save(i));
    this.processQueue();
  }

  /**
   * Returns the active queue item for the given entity, or null if none.
   * Use this for card download indicators.
   */
  getItemForEntity(entity: DownloadEntity, includeCompleted = false): DownloadQueueItem | null {
    if (entity === undefined) return null;

    // Read both signals up front so Angular computed/effect tracks them as dependencies,
    // even for code paths that use the plain Map/Set for O(1) lookup.
    const aq = this.activeQueue();
    const ct = this.completedToday();

    // Series: aggregate across all active + completed items together so progress doesn't drop
    if (this.utilityService.isSeries(entity)) {
      const sId = (entity as Series).id;
      const items = aq.filter(i => ['queued', 'preparing', 'downloading'].includes(i.status) && i.seriesId === sId);

      if (includeCompleted) {
        items.push(...ct.filter(i => i.seriesId === sId));
      }
      return this._aggregateSeriesItems(items);
    }

    // ReadingList: aggregate across all active + completed items together so progress doesn't drop
    if (this.utilityService.isReadingList(entity)) {
      const rlId = (entity as ReadingList).id;

      const items = aq.filter(i => ['queued', 'preparing', 'downloading'].includes(i.status) && i.readingListId === rlId);

      if (includeCompleted) {
        items.push(...ct.filter(i => i.readingListId === rlId));
      }
      return this._aggregateSeriesItems(items);
    }

    if (this.utilityService.isUserCollection(entity)) {
      const cId = (entity as UserCollection).id;

      const items = aq.filter(i => ['queued', 'preparing', 'downloading'].includes(i.status) && i.collectionId === cId);

      if (includeCompleted) {
        items.push(...ct.filter(i => i.collectionId === cId));
      }
      return this._aggregateSeriesItems(items);
    }

    // Volume/Chapter: O(1) Map lookup for active
    const entityType = this.utilityService.isVolume(entity) ? DownloadEntityType.Volume : DownloadEntityType.Chapter;
    const key = this._indexKey(entityType, (entity as Volume | Chapter).id);
    const active = this._activeIndex.get(key);
    if (active && ['queued', 'preparing', 'downloading'].includes(active.status)) return active;

    // Check today's completed (small array)
    if (includeCompleted) {
      const todayMatch = ct.find(i => i.entityType === entityType && i.entityId === (entity as Volume | Chapter).id);
      if (todayMatch) return todayMatch;

      // Check if entity was ever downloaded (older, in IDB)
      if (this._completedEntityIds.has(key)) {
        return { status: 'completed', entityType, entityId: (entity as Volume | Chapter).id } as DownloadQueueItem;
      }
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

    // When between sequential downloads (some completed, rest queued), use 'preparing'
    // instead of 'queued' to prevent the indicator from flashing between active/queued states
    let aggregateStatus = representative.status;
    if (allCompleted) {
      aggregateStatus = 'completed';
    } else if (representative.status === 'queued' && items.some(i => i.status === 'completed')) {
      aggregateStatus = 'preparing';
    }

    return {
      ...representative,
      progress: Math.round(totalProgress / items.length),
      status: aggregateStatus,
    };
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


  exportReadingList(readingListId: number, readingListName: string, asV2 = false) {
    return this.httpClient.post(
      this.baseUrl + `readinglist/export-as-cbl?readingListId=${readingListId}&asV2=${asV2}`, {},
      { observe: 'response', responseType: 'blob' }
    ).pipe(
      tap((response) => {
        const disposition = response.headers.get('Content-Disposition') ?? '';
        const filename = parseContentDisposition(disposition, `${readingListName}.${asV2 ? 'json' : 'cbl'}`);
        const url = URL.createObjectURL(response.body!);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        URL.revokeObjectURL(url);
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  private getEntityDownloadSize(entityType: DownloadEntityType, id: number) {
    return this.httpClient.get<number>(this.baseUrl + `download/${entityType}-size?${entityType}Id=${id}`);
  }

  private getBulkEntityDownloadSize(entityType: DownloadEntityType.Series | DownloadEntityType.Volume | DownloadEntityType.Chapter, ids: number[]) {
    const data = {} as any;
    data[entityType + 'Ids'] = ids;
    return this.httpClient.post<Record<number, number>>(this.baseUrl + `download/bulk-${entityType}-size`, data);
  }

  private downloadSeriesSize(seriesId: number) {
    return this.getEntityDownloadSize(DownloadEntityType.Series, seriesId);
  }

  private downloadBulkVolumeSizes(volumeIds: number[]) {
    return this.getBulkEntityDownloadSize(DownloadEntityType.Volume, volumeIds);
  }

  private downloadBulkChapterSizes(chapterIds: number[]) {
    return this.getBulkEntityDownloadSize(DownloadEntityType.Chapter, chapterIds);
  }

  private downloadVolumeSize(volumeId: number) {
    return this.getEntityDownloadSize(DownloadEntityType.Volume, volumeId);
  }


  private downloadVolume(volume: Volume, libraryId: number, seriesId: number) {
    this.debugLog('downloadVolume()', volume.minNumber);

    // Volumes can be either a bunch of chapters or just 1
    if (volume.chapters.length === 1) {
      this.enqueueSingle(volume, DownloadEntityType.Volume, '', libraryId, seriesId);
      return;
    }
    this.debugLog(`downloadVolume() decomposed into ${volume.chapters.length} items`);

    const items = volume.chapters.map(c => ({ entity: c as Chapter, entityType: DownloadEntityType.Chapter as const }));

    const userPrefs = this.accountService.userPreferences();
    if (userPrefs?.promptForDownloadSize && items.length > 0) {
      // Single size call for the whole series, single confirm dialog
      this.downloadVolumeSize(volume.id).pipe(
        switchMap(async size => this.confirmSize(size, DownloadEntityType.Volume)),
        filter(confirmed => confirmed),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe(() => this.enqueueItems(items, '', libraryId, seriesId));
    } else {
      this.enqueueItems(items, '', libraryId, seriesId);
    }
  }

  private downloadCollection(collection: UserCollection) {
    this.debugLog('downloadCollection()', collection.title);

    const userPrefs = this.accountService.userPreferences();

    // A collection is just a set of series, so we can just call down
    this.seriesService.getAllSeriesV2(0, 0, {
      statements: [{field: FilterField.CollectionTags, value: collection.id + '', comparison: FilterComparison.Equal}],
      combination: FilterCombination.And,
      limitTo: 0
    }).subscribe(collectionSeries => {


      if (userPrefs?.promptForDownloadSize && collectionSeries.result.length > 0) {
        const seriesIds = collectionSeries.result.map(s => s.id);
        this.getBulkEntityDownloadSize(DownloadEntityType.Series, seriesIds).pipe(
          map(r => Object.values(r).reduce((acc, curr) => acc + curr, 0)),
          switchMap(async size => this.confirmSize(size, DownloadEntityType.Series)),
          filter(confirmed => confirmed),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => {

          collectionSeries.result.forEach(s => {
            this.downloadSeries(s, collection.id, true);
          });
        });
      } else {
        collectionSeries.result.forEach(s => {
          this.downloadSeries(s, collection.id);
        });
      }
    });
  }
  private downloadReadingList(readingList: ReadingList) {
    this.debugLog('downloadReadingList()', readingList.title);

    // We need to check if this instance has items or not
    let items$ = readingList.hasOwnProperty('items') ?
      of(readingList.items ?? []) :
      this.readingListService.getListItems(readingList.id);

    items$.subscribe((items: ReadingListItem[]) => {
      const rliItems = items.map(item => ({ entity: item as ReadingListItem, entityType: DownloadEntityType.ReadingListItem as const }));
      this.enqueueItems(rliItems, readingList.title, 0, 0, readingList.id);
    });
  }

  private downloadSeries(series: Series, collectionId = 0, skipSizePrompt = false) {
    this.debugLog('downloadSeries()', series.name);
    this.seriesService.getSeriesDetail(series.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(detail => {

      // Ensure that virtual volumes aren't downloaded
      const chapterIdsInRealVolumes = new Set<number>(
        detail.volumes
          .filter(v => v.chapters.length === 1)
          .flatMap(v => v.chapters.map(c => c.id))
      );

      const items: Array<{ entity: Volume | Chapter; entityType: DownloadEntityType.Volume | DownloadEntityType.Chapter }> = [
        // Real volumes (single-chapter) — download as volume
        ...detail.volumes
          .filter(v => v.chapters.length === 1)
          .map(v => ({ entity: v as Volume, entityType: DownloadEntityType.Volume as const })),
        // Chapters not already covered by a real volume
        ...detail.chapters
          .filter(c => !chapterIdsInRealVolumes.has(c.id))
          .map(c => ({ entity: c as Chapter, entityType: DownloadEntityType.Chapter as const })),
        // Specials — no overlap
        ...detail.specials.map(c => ({
          entity: c as Chapter,
          entityType: DownloadEntityType.Chapter as const,
        })),
      ];

      this.debugLog(`downloadSeries() decomposed into ${items.length} items (${detail.volumes.length} vols, ${detail.chapters.length + detail.specials.length} chapters)`);

      const userPrefs = this.accountService.userPreferences();
      if (!skipSizePrompt && userPrefs?.promptForDownloadSize && items.length > 0) {
        // Single size call for the whole series, single confirm dialog
        this.downloadSeriesSize(series.id).pipe(
          switchMap(async size => this.confirmSize(size, DownloadEntityType.Series)),
          filter(confirmed => confirmed),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => this.enqueueItems(items, series.name, series.libraryId, series.id, 0, collectionId));
      } else {
        this.enqueueItems(items, series.name, series.libraryId, series.id, 0, collectionId);
      }
    });
  }

  private enqueueItems(items: Array<{ entity: Volume | Chapter | ReadingListItem; entityType: DistilledDownloadEntityType }>, seriesName: string, libraryId: number, seriesId = 0, readingListId = 0, collectionId = 0) {
    this.debugLog(`enqueueItems() adding ${items.length} items for series "${seriesName}"`);

    const volumeItems = items.filter(i => i.entityType === DownloadEntityType.Volume);
    const chapterItems = items.filter(i => i.entityType === DownloadEntityType.Chapter);
    const rliItems = items.filter(i => i.entityType === DownloadEntityType.ReadingListItem);

    const volSizes$ = volumeItems.length > 0
      ? this.getBulkEntityDownloadSize(DownloadEntityType.Volume, volumeItems.map(i => i.entity.id))
      : of({} as Record<number, number>);
    const chSizes$ = chapterItems.length > 0
      ? this.getBulkEntityDownloadSize(DownloadEntityType.Chapter, chapterItems.map(i => i.entity.id))
      : of({} as Record<number, number>);
    // ReadingListItems download via the chapter endpoint, so fetch chapter sizes using chapterId
    const rliSizes$ = rliItems.length > 0
      ? this.getBulkEntityDownloadSize(DownloadEntityType.Chapter, rliItems.map(i => (i.entity as ReadingListItem).chapterId))
      : of({} as Record<number, number>);

    forkJoin([volSizes$, chSizes$, rliSizes$]).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(async ([volMap, chMap, rlMap]) => {

      const hasReDownloads = items.some(i => {
        const key = this._indexKey(i.entityType, i.entity.id);
        return this._completedEntityIds.has(key);
      });

      const reDownload = hasReDownloads && await this.confirmService.confirm(
        translate('toasts.redownload-confirm-bulk', { title: seriesName })
      );

      for (const item of items) {
        let size: number;

        switch (item.entityType) {
          case DownloadEntityType.Volume:
            size = volMap[item.entity.id] ?? 0;
            break;
          case DownloadEntityType.Chapter:
            size = chMap[item.entity.id] ?? 0;
            break;
          case DownloadEntityType.ReadingListItem:
            size = rlMap[(item.entity as ReadingListItem).chapterId] ?? 0;
            break;
        }

        await this.addToQueue(item.entity, item.entityType, seriesName, libraryId, size, seriesId, readingListId, collectionId, true, reDownload);
      }
      this.processQueue();
    });
  }

  private enqueueSingle(entity: Volume | Chapter, entityType: DownloadEntityType.Volume | DownloadEntityType.Chapter, seriesName: string, libraryId: number, seriesId = 0, readingListId = 0, collectionId = 0) {
    const user = this.accountService.currentUser();
    const sizeCall$ = entityType === DownloadEntityType.Volume
      ? this.downloadBulkVolumeSizes([entity.id]).pipe(map(m => m[entity.id] ?? 0))
      : this.downloadBulkChapterSizes([entity.id]).pipe(map(m => m[entity.id] ?? 0));

    // Always fetch size to populate estimatedSize; only prompt if user preference is set
    sizeCall$.pipe(
      switchMap(async size => {
        const promptForSize = user && user.preferences.promptForDownloadSize;
        const confirmed = promptForSize ? await this.confirmSize(size, entityType) : true;
        return { size, confirmed };
      }),
      filter(result => result.confirmed),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(async ({ size }) => {
      await this.addToQueue(entity, entityType, seriesName, libraryId, size, seriesId, readingListId, collectionId);
      this.processQueue();
    });
  }

  private async resolveSeriesName(seriesName: string, seriesId: number): Promise<string> {
    if (seriesName) return seriesName;
    if (seriesId <= 0) return '';
    const cached = this._seriesNameCache.get(seriesId);
    if (cached !== undefined) {
      // Move to end (most-recently-used)
      this._seriesNameCache.delete(seriesId);
      this._seriesNameCache.set(seriesId, cached);
      return cached;
    }
    try {
      const series = await firstValueFrom(this.seriesService.getSeries(seriesId));
      // Evict oldest, if at capacity
      if (this._seriesNameCache.size >= this.SERIES_NAME_CACHE_MAX) {
        const oldest = this._seriesNameCache.keys().next().value!;
        this._seriesNameCache.delete(oldest);
      }
      this._seriesNameCache.set(seriesId, series.name);
      return series.name;
    } catch {
      return '';
    }
  }

  private async addToQueue(entity: Volume | Chapter | ReadingListItem, entityType: DistilledDownloadEntityType,
                           seriesName: string, libraryId: number, estimatedSize = 0, seriesId = 0, readingListId = 0,
                           collectionId = 0, isBulk = false, reDownloadInBulk = false) {
    seriesName = await this.resolveSeriesName(seriesName, seriesId);
    const entityId = entity.id;
    const key = this._indexKey(entityType, entityId);

    // 1. Already queued/active → silently drop
    if (this._activeIndex.has(key)) {
      this.debugLog(`addToQueue() duplicate active - silently dropping ${key}`);
      return;
    }

    // 2. Previously completed → skip silently in bulk, prompt for single downloads
    if (this._completedEntityIds.has(key) && (!isBulk || !reDownloadInBulk)) {
      if (isBulk) {
        this.debugLog(`addToQueue() already completed, skipping in bulk - ${key}`);
        return;
      }

      const todayMatch = this.completedToday().find(i => i.entityType === entityType && i.entityId === entityId);
      const olderMatch = !todayMatch ? this._olderItems().find(i => i.entityType === entityType && i.entityId === entityId) : undefined;
      const match = todayMatch ?? olderMatch;

      const utcPipe = new UtcToLocalDatePipe();
      const localDate = match?.completedAt ? utcPipe.transform(normalizeTimestamp(match.completedAt)) : null;
      const dateStr = localDate?.toLocaleDateString() ?? '';
      const titleLabel = match?.label ?? `${entityType} ${entityId}`;

      const confirmed = await this.confirmService.confirm(
        translate('toasts.redownload-confirm', { title: titleLabel, date: dateStr })
      );
      if (!confirmed) {
        this.debugLog(`addToQueue() re-download declined for ${key}`);
        return;
      }
    }

    const id = this._nextId++;
    this.debugLog(`addToQueue() id=${id} type=${entityType} entityId=${entityId} series="${seriesName}"`);

    // Resolve ReadingListItem overrides BEFORE fetching libType
    let chapterId: number | undefined;
    if (entityType === DownloadEntityType.ReadingListItem) {
      const rli = entity as ReadingListItem;
      chapterId = rli.chapterId;
      libraryId = rli.libraryId;
      seriesId = rli.seriesId;
    }

    let subLabel: string;
    let downloadName: string;

    const libType = await firstValueFrom(this.libraryService.getLibraryType(libraryId));
    if (entityType === DownloadEntityType.Volume) {
      const vol = entity as Volume;
      subLabel = vol.minNumber + '';
      downloadName = this.entityTitleService.computeTitle(vol, libType, {includeVolume: true});
    } else if (entityType === DownloadEntityType.ReadingListItem) {
      const rli = entity as ReadingListItem;
      subLabel = rli.title;
      downloadName = seriesName ? `${seriesName} - ${rli.title}` : rli.title;
    } else {
      const ch = entity as Chapter;
      subLabel = ch.minNumber + '';
      const chName = this.entityTitleService.computeTitle(ch, libType, {prioritizeTitleName: false});
      downloadName = seriesName ? `${seriesName} - ${chName}` : chName;
    }

    const label = downloadName;

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
      queuedAt: DateTime.utc().toISO()!,
      entity,
      downloadName,
      readingListId,
      collectionId,
      ...(chapterId !== undefined ? { chapterId } : {}),
    };

    this.activeQueue.update(q => [...q, item]);
    this._rebuildActiveIndex();
    this.storage.save(item);
  }

  resumeQueue() {
    this.isPaused.set(false);
    this.processQueue();
  }

  private processQueue() {
    if (this.isPaused()) return;
    if (this.activeItem()) {
      this.debugLog('processQueue() - already active, skipping');
      return;
    }

    const nextItem = this.activeQueue().find(i => i.status === 'queued');
    if (!nextItem) {
      this.debugLog('processQueue() - queue empty, nothing to do');
      return;
    }

    this.debugLog(`processQueue() - starting item id=${nextItem.id} "${nextItem.label}"`);
    this.setStatus(nextItem.id, 'preparing');
    this.triggerDownload(nextItem);
  }

  /** Active AbortControllers keyed by item id, for cancellation support */
  private activeAbortControllers = new Map<number, AbortController>();

  private triggerDownload(item: DownloadQueueItem) {
    const apiKey = this.accountService.currentUserGenericApiKey();
    if (!apiKey) {
      this.debugLog(`triggerDownload() - no API key for id=${item.id}`);
      this.markFailed(item.id, this.translocoService.translate('download-queue-drawer.failed-from-auth'));
      return;
    }

    // readingListItem downloads via the chapter endpoint using chapterId
    const endpoint = item.entityType === DownloadEntityType.ReadingListItem ? DownloadEntityType.Chapter : item.entityType;
    const idKey = endpoint === DownloadEntityType.Volume ? 'volumeId' : 'chapterId';
    const idValue = item.entityType === DownloadEntityType.ReadingListItem ? item.chapterId! : item.entityId;
    const url = `${this.baseUrl}download/${endpoint}` +
                `?${idKey}=${idValue}` +
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
      const filename = parseContentDisposition(response.headers.get('Content-Disposition') || '', item.downloadName);

      this.setStatus(item.id, 'downloading');

      const reader = response.body.getReader();
      const chunks: BlobPart[] = [];
      let received = 0;
      let lastProgressUpdate = 0;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
        received += value.length;

        if (contentLength > 0) {
          const now = Date.now();

          // Throttle signal updates to ~4/sec max (250ms)
          if ((now - lastProgressUpdate) < 250) continue;
          lastProgressUpdate = now;

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

          this._updateItem(item.id, {
            progress,
            ...(speedBps !== undefined ? { speedBps } : {}),
            ...(etaSeconds !== undefined ? { etaSeconds } : {}),
          });
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

  /** Updates activeQueue signal and persists to IDB on status changes. */
  private setStatus(id: number, status: DownloadQueueStatus, extra?: Partial<DownloadQueueItem>) {
    this._updateItem(id, { status, ...extra });
    const item = this.activeQueue().find(i => i.id === id);
    if (item) this.storage.save(item);
  }

  /**
   * Computes aggregate download progress for all items belonging to a series.
   * Returns a synthetic DownloadQueueItem with averaged progress, or null if no active items.
   */
  private getSeriesDownloadProgress(seriesName: string): DownloadQueueItem | null {
    const activeItems = this.activeQueue().filter(i =>
      i.seriesName === seriesName &&
      i.status !== 'cancelled' && i.status !== 'failed'
    );
    const completedItems = this.completedToday().filter(i => i.seriesName === seriesName);
    const allItems = [...activeItems, ...completedItems];
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

    // Find the item in activeQueue, move it to completedToday
    const item = this.activeQueue().find(i => i.id === itemId);
    if (item) {
      const completed = { ...item, status: 'completed' as DownloadQueueStatus, progress: 100, completedAt: DateTime.utc().toISO()! };
      this.activeQueue.update(q => q.filter(i => i.id !== itemId));
      this._rebuildActiveIndex();
      this.completedToday.update(q => [...q, completed]);
      this._completedEntityIds.add(this._indexKey(completed.entityType, completed.entityId));
      this.storage.save(completed);
    }

    // Give GC time to reclaim the previous download's blob before starting the next one
    setTimeout(() => this.processQueue(), 1500);
  }

  private markFailed(itemId: number, error: string) {
    this.debugLog(`markFailed() id=${itemId} error="${error}"`);
    this._speedSamples.delete(itemId);
    this._smoothedSpeed.delete(itemId);
    this._updateItem(itemId, { status: 'failed' as DownloadQueueStatus, errorMessage: error, completedAt: DateTime.utc().toISO()! });
    const item = this.activeQueue().find(i => i.id === itemId);
    if (item) this.storage.save(item);
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

  private getStartOfDay() {
    return DateTime.utc().startOf('day').toISO()!;
  }
}
