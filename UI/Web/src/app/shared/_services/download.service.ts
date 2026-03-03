import {HttpClient} from '@angular/common/http';
import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {Series} from 'src/app/_models/series';
import {environment} from 'src/environments/environment';
import {ConfirmService} from '../confirm.service';
import {Chapter} from 'src/app/_models/chapter';
import {Volume} from 'src/app/_models/volume';
import {asyncScheduler, filter, Observable, of, tap} from 'rxjs';
import {download, Download} from '../_models/download';
import {PageBookmark} from 'src/app/_models/readers/page-bookmark';
import {finalize, map, switchMap, throttleTime} from 'rxjs/operators';
import {AccountService} from 'src/app/_services/account.service';
import {BytesPipe} from 'src/app/_pipes/bytes.pipe';
import {translate} from "@jsverse/transloco";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {SAVER} from "../../_providers/saver.provider";
import {UtilityService} from "./utility.service";
import {UserCollection} from "../../_models/collection-tag";
import {RecentlyAddedItem} from "../../_models/recently-added-item";
import {NextExpectedChapter} from "../../_models/series-detail/next-expected-chapter";
import {BrowsePerson} from "../../_models/metadata/browse/browse-person";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {NotificationProgressEvent} from "../../_models/events/notification-progress-event";
import {SeriesService} from "../../_services/series.service";
import {DownloadQueueItem, DownloadQueueStatus} from '../_models/download-queue-item';

export const DEBOUNCE_TIME = 100;

const bytesPipe = new BytesPipe();

export interface DownloadEvent {
  /** Type of entity being downloaded */
  entityType: DownloadEntityType;
  /** What to show user. For example, for Series, we might show series name. */
  subTitle: string;
  /** Progress of the download itself */
  progress: number;
  /** Entity id. For entities without id like logs or bookmarks, uses 0 instead */
  id: number;
}

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
  readonly completedItems = computed(() => this.queue().filter(i => i.status === 'completed'));
  readonly failedItems = computed(() => this.queue().filter(i => i.status === 'failed'));
  readonly totalActiveCount = computed(() =>
    (this.activeItem() ? 1 : 0) + this.queuedItems().length
  );
  readonly hasActiveDownloads = computed(() =>
    this.activeItem() !== null || this.queuedItems().length > 0
  );

  /**
   * Backward-compatible observable for existing consumers (card-config-factory, detail pages).
   * Emits active (preparing/downloading) items mapped to DownloadEvent shape.
   */
  readonly activeDownloads$: Observable<DownloadEvent[]> = toObservable(this.queue).pipe(
    map(items =>
      items
        .filter(i => i.status === 'preparing' || i.status === 'downloading')
        .map(i => ({
          entityType: i.entityType as DownloadEntityType,
          subTitle: i.subLabel,
          progress: i.progress,
          id: i.entityId
        }))
    )
  );

  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  private readonly httpClient = inject(HttpClient);
  private readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly seriesService = inject(SeriesService);
  private readonly save = inject(SAVER);

  constructor() {
    // Dedicated SignalR channel for download progress
    this.messageHub.messages$.pipe(
      filter(evt => evt.event === EVENTS.DownloadProgress),
      map(evt => evt.payload as NotificationProgressEvent),
      tap(evt => {
        this.debugLog(`DownloadProgress event: type="${evt.eventType}" body=`, evt.body);

        if (evt.eventType === 'ended') {
          // We only ever have one active download at a time, so mark it complete.
          const active = this.activeItem();
          if (active) {
            this.debugLog(`DownloadProgress ended — marking id=${active.id} complete in 3s`);
            this.queue.update(q => q.map(i =>
              i.id === active.id ? { ...i, status: 'downloading' as DownloadQueueStatus, progress: 100 } : i
            ));
            // Single file items (usually pdf/epubs) don't send DownloadEvent, thus we need a timeout (hack)
            setTimeout(() => this.markCompleted(active.id), 3000);
          }
          return;
        }

        // For started/updated events, attempt name-based matching for progress.
        // Field names vary by backend version — try both casing conventions.
        const downloadName: string = evt.body?.DownloadName ?? evt.body?.downloadName ?? '';
        const progressValue = Math.round((evt.body?.Progress ?? evt.body?.progress ?? 0) * 100);
        if (downloadName) {
          this.queue.update(q => q.map(item => {
            if (item.downloadName !== downloadName) return item;
            return { ...item, progress: progressValue };
          }));
        } else {
          // No name in body — update progress on the active item directly
          const active = this.activeItem();
          if (active && progressValue > 0) {
            this.queue.update(q => q.map(i =>
              i.id === active.id ? { ...i, progress: progressValue } : i
            ));
          }
        }
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    // Background Fetch completions from the service worker
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.addEventListener('message', (event) => {
        const data = event.data;
        if (data?.type === 'download-complete') {
          this.markCompleted(Number(data.id));
        } else if (data?.type === 'download-failed') {
          this.markFailed(Number(data.id), data.error ?? 'Download failed');
        }
      });
    }
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
  download(entityType: DownloadEntityType, entity: DownloadEntity, callback?: (d: Download | undefined) => void) {
    switch (entityType) {
      case 'series':
        this.downloadSeries(entity as Series);
        break;
      case 'volume':
        this.enqueueSingle(entity as Volume, 'volume', '');
        break;
      case 'chapter':
        this.enqueueSingle(entity as Chapter, 'chapter', '');
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
    this.queue.update(q => q.filter(i => i.id !== itemId));
    setTimeout(() => this.processQueue(), 100);
  }

  removeItem(itemId: number) {
    this.queue.update(q => q.filter(i => i.id !== itemId));
  }

  clearCompleted() {
    this.queue.update(q => q.filter(i => i.status !== 'completed'));
  }

  retryDownload(itemId: number) {
    const item = this.queue().find(i => i.id === itemId);
    if (!item || item.retryCount >= 3) return;
    this.queue.update(q => q.map(i =>
      i.id === itemId
        ? { ...i, status: 'queued' as DownloadQueueStatus, errorMessage: '', retryCount: i.retryCount + 1 }
        : i
    ));
    this.processQueue();
  }

  cancelAllQueued() {
    this.queue.update(q => q.filter(i => i.status !== 'queued'));
  }

  retryAllFailed() {
    this.queue.update(q => q.map(i =>
      i.status === 'failed'
        ? { ...i, status: 'queued' as DownloadQueueStatus, errorMessage: '', retryCount: i.retryCount + 1 }
        : i
    ));
    this.processQueue();
  }

  /**
   * Returns the active queue item for the given entity, or null if none.
   * Use this for card download indicators.
   */
  getItemForEntity(entity: Series | Volume | Chapter | PageBookmark[]): DownloadQueueItem | null {
    const q = this.queue();
    if (this.utilityService.isVolume(entity)) {
      return q.find(i => i.entityType === 'volume' && i.entityId === (entity as Volume).id) ?? null;
    }
    if (this.utilityService.isChapter(entity)) {
      return q.find(i => i.entityType === 'chapter' && i.entityId === (entity as Chapter).id) ?? null;
    }
    if (this.utilityService.isSeries(entity)) {
      return q.find(i => i.seriesName === (entity as Series).name
        && (i.status === 'preparing' || i.status === 'downloading')) ?? null;
    }
    return null;
  }

  /**
   * Maps a list of DownloadEvents to the one matching `entity`, for backward compatibility.
   */
  mapToEntityType(events: DownloadEvent[], entity: Series | Volume | Chapter | UserCollection | PageBookmark | RecentlyAddedItem | NextExpectedChapter | BrowsePerson) {
    if (this.utilityService.isSeries(entity)) {
      return events.find(e => e.entityType === 'series' && e.id == entity.id
        && e.subTitle === this.downloadSubtitle('series', (entity as Series))) || null;
    }
    if (this.utilityService.isVolume(entity)) {
      return events.find(e => e.entityType === 'volume' && e.id == entity.id
        && e.subTitle === this.downloadSubtitle('volume', (entity as Volume))) || null;
    }
    if (this.utilityService.isChapter(entity)) {
      return events.find(e => e.entityType === 'chapter' && e.id == entity.id
        && e.subTitle === this.downloadSubtitle('chapter', (entity as Chapter))) || null;
    }
    // PageBookmark[]
    if (entity.hasOwnProperty('length')) {
      return events.find(e => e.entityType === 'bookmark'
        && e.subTitle === this.downloadSubtitle('bookmark', [(entity as PageBookmark)])) || null;
    }
    return null;
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
        ).subscribe(() => this.enqueueItems(items, series.name));
      } else {
        this.enqueueItems(items, series.name);
      }
    });
  }

  private enqueueItems(items: Array<{ entity: Volume | Chapter; entityType: 'volume' | 'chapter' }>, seriesName: string) {
    this.debugLog(`enqueueItems() adding ${items.length} items for series "${seriesName}"`);
    for (const item of items) {
      this.addToQueue(item.entity, item.entityType, seriesName);
    }
    this.processQueue();
  }

  private enqueueSingle(entity: Volume | Chapter, entityType: 'volume' | 'chapter', seriesName: string) {
    const user = this.accountService.currentUser();
    const sizeCheckCall = entityType === 'volume'
      ? this.downloadVolumeSize((entity as Volume).id)
      : this.downloadChapterSize((entity as Chapter).id);

    const sizeCheck$ = (user && user.preferences.promptForDownloadSize) ? sizeCheckCall : of(0);

    sizeCheck$.pipe(
      switchMap(async size => this.confirmSize(size, entityType)),
      filter(wantsToDownload => wantsToDownload),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.addToQueue(entity, entityType, seriesName);
      this.processQueue();
    });
  }

  private addToQueue(entity: Volume | Chapter, entityType: 'volume' | 'chapter', seriesName: string) {
    const id = this._nextId++;
    const entityId = entity.id;
    this.debugLog(`addToQueue() id=${id} type=${entityType} entityId=${entityId} series="${seriesName}"`);

    let label: string;
    let subLabel: string;
    let downloadName: string;

    if (entityType === 'volume') {
      const vol = entity as Volume;
      label = seriesName ? `${seriesName} - Vol. ${vol.name}` : `Vol. ${vol.name}`;
      subLabel = vol.minNumber + '';
      downloadName = seriesName ? `${seriesName} - Volume ${vol.name}` : `Volume ${vol.name}`;
    } else {
      const ch = entity as Chapter;
      label = seriesName ? `${seriesName} - Ch. ${ch.minNumber}` : `Ch. ${ch.minNumber}`;
      subLabel = ch.minNumber + '';
      downloadName = seriesName ? `${seriesName} - Chapter ${ch.minNumber}` : `Chapter ${ch.minNumber}`;
    }

    const item: DownloadQueueItem = {
      id,
      entityType,
      entityId,
      label,
      subLabel,
      seriesName,
      estimatedSize: 0,
      status: 'queued',
      progress: 0,
      errorMessage: '',
      retryCount: 0,
      queuedAt: Date.now(),
      entity,
      downloadName,
    };

    this.queue.update(q => [...q, item]);
  }

  private processQueue() {
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
    this.queue.update(q =>
      q.map(i => i.id === nextItem.id ? { ...i, status: 'preparing' as DownloadQueueStatus } : i)
    );

    const apiKey = this.accountService.currentUserGenericApiKey();
    if (!apiKey) {
      this.debugLog(`processQueue() — no API key, falling back to blob download for id=${nextItem.id}`);
      this.downloadItemAsBlob(nextItem);
      return;
    }

    const idKey = nextItem.entityType === 'volume' ? 'volumeId' : 'chapterId';
    const url = `${this.baseUrl}download/${nextItem.entityType}?${idKey}=${nextItem.entityId}&apiKey=${encodeURIComponent(apiKey)}`;
    this.debugLog(`processQueue() — built URL for id=${nextItem.id}:`, url);

    // Use anchor-based download directly. <a download> to same-origin works without user
    // activation so this is safe from async contexts (subscribe callbacks, etc.).
    // NOTE: navigator.serviceWorker.ready must NOT be awaited here — it hangs indefinitely
    // when the service worker fails to activate (common in dev or after SW errors).
    this.downloadItemViaAnchor(nextItem, url);
  }

  private downloadItemViaAnchor(item: DownloadQueueItem, url: string) {
    this.debugLog(`downloadItemViaAnchor() id=${item.id} "${item.label}"`);
    this.queue.update(q =>
      q.map(i => i.id === item.id ? { ...i, status: 'downloading' as DownloadQueueStatus } : i)
    );
    const a = document.createElement('a');
    a.href = url;
    a.download = item.downloadName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    // SignalR 'ended' event marks complete; if SignalR is unavailable, markCompleted won't fire
    // automatically — that's acceptable, the item stays as 'downloading' until user dismisses.
  }

  private downloadItemAsBlob(item: DownloadQueueItem) {
    const idKey = item.entityType === 'volume' ? 'volumeId' : 'chapterId';
    const url = `${this.baseUrl}download/${item.entityType}?${idKey}=${item.entityId}`;

    this.queue.update(q =>
      q.map(i => i.id === item.id ? { ...i, status: 'downloading' as DownloadQueueStatus } : i)
    );

    this.httpClient.get(url, { observe: 'events', responseType: 'blob', reportProgress: true }).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      }),
      tap(d => {
        if (d.state === 'IN_PROGRESS') {
          this.queue.update(q =>
            q.map(i => i.id === item.id ? { ...i, progress: d.progress } : i)
          );
        }
      }),
      finalize(() => this.markCompleted(item.id)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  private markCompleted(itemId: number) {
    this.debugLog(`markCompleted() id=${itemId}`);
    this.queue.update(q =>
      q.map(i => i.id === itemId ? { ...i, status: 'completed' as DownloadQueueStatus, progress: 100 } : i)
    );
    // Auto-clear after 5 minutes
    setTimeout(() => this.removeItem(itemId), 5 * 60 * 1000);
    // Process next queued item
    setTimeout(() => this.processQueue(), 100);
  }

  private markFailed(itemId: number, error: string) {
    this.debugLog(`markFailed() id=${itemId} error="${error}"`);
    this.queue.update(q =>
      q.map(i => i.id === itemId ? { ...i, status: 'failed' as DownloadQueueStatus, errorMessage: error } : i)
    );
    setTimeout(() => this.processQueue(), 100);
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
