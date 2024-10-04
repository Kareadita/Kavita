import { HttpClient } from '@angular/common/http';
import {DestroyRef, inject, Inject, Injectable} from '@angular/core';
import { Series } from 'src/app/_models/series';
import { environment } from 'src/environments/environment';
import { ConfirmService } from '../confirm.service';
import { Chapter } from 'src/app/_models/chapter';
import { Volume } from 'src/app/_models/volume';
import {
  asyncScheduler,
  BehaviorSubject,
  Observable,
  tap,
  finalize,
  of,
  filter,
} from 'rxjs';
import { download, Download } from '../_models/download';
import { PageBookmark } from 'src/app/_models/readers/page-bookmark';
import {switchMap, take, takeWhile, throttleTime} from 'rxjs/operators';
import { AccountService } from 'src/app/_services/account.service';
import { BytesPipe } from 'src/app/_pipes/bytes.pipe';
import {translate} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SAVER, Saver} from "../../_providers/saver.provider";
import {UtilityService} from "./utility.service";
import {UserCollection} from "../../_models/collection-tag";
import {RecentlyAddedItem} from "../../_models/recently-added-item";
import {NextExpectedChapter} from "../../_models/series-detail/next-expected-chapter";
import {BrowsePerson} from "../../_models/person/browse-person";

export const DEBOUNCE_TIME = 100;

const bytesPipe = new BytesPipe();

export interface DownloadEvent {
  /**
   * Type of entity being downloaded
   */
  entityType: DownloadEntityType;
  /**
   * What to show user. For example, for Series, we might show series name.
   */
  subTitle: string;
  /**
   * Progress of the download itself
   */
  progress: number;
  /**
   * Entity id. For entities without id like logs or bookmarks, uses 0 instead
   */
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

export type QueueableDownloadType = Chapter | Volume;

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
   * Sie in bytes in which to inform the user that anything above may fail on iOS due to device limits. (200MB)
   */
  private IOS_SIZE_WARNING = 209_715_200;

  private downloadsSource: BehaviorSubject<DownloadEvent[]> = new BehaviorSubject<DownloadEvent[]>([]);
  /**
   * Active Downloads
   */
  public activeDownloads$ = this.downloadsSource.asObservable();

  private downloadQueue: BehaviorSubject<QueueableDownloadType[]> = new BehaviorSubject<QueueableDownloadType[]>([]);
  /**
   * Queued Downloads
   */
  public queuedDownloads$ = this.downloadQueue.asObservable();

  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  private readonly httpClient = inject(HttpClient);
  private readonly utilityService = inject(UtilityService);

  constructor(@Inject(SAVER) private save: Saver) {
    this.downloadQueue.subscribe((queue) => {
      if (queue.length > 0) {
        const entity = queue.shift();
        console.log('Download Queue shifting entity: ', entity);
        if (entity === undefined) return;
        this.processDownload(entity);
      }
    });
  }


  /**
   * Returns the entity subtitle (for the event widget) for a given entity
   * @param downloadEntityType
   * @param downloadEntity
   * @returns
   */
   downloadSubtitle(downloadEntityType: DownloadEntityType | undefined, downloadEntity: DownloadEntity | undefined) {
    switch (downloadEntityType) {
      case 'series':
        return (downloadEntity as Series).name;
      case 'volume':
        return (downloadEntity as Volume).minNumber + '';
      case 'chapter':
        return (downloadEntity as Chapter).minNumber + '';
      case 'bookmark':
        return '';
      case 'logs':
        return '';
    }
    return '';
  }

  /**
   * Downloads the entity to the user's system. This handles everything around downloads. This will prompt the user based on size checks and UserPreferences.PromptForDownload.
   * This will perform the download at a global level, if you need a handle to the download in question, use downloadService.activeDownloads$ and perform a filter on it.
   * @param entityType
   * @param entity
   * @param callback Optional callback. Returns the download or undefined (if the download is complete).
   */
  download(entityType: DownloadEntityType, entity: DownloadEntity, callback?: (d: Download | undefined) => void) {
    let sizeCheckCall: Observable<number>;
    let downloadCall: Observable<Download>;
    switch (entityType) {
      case 'series':
        sizeCheckCall = this.downloadSeriesSize((entity as Series).id);
        downloadCall = this.downloadSeries(entity as Series);
        break;
      case 'volume':
        sizeCheckCall = this.downloadVolumeSize((entity as Volume).id);
        downloadCall = this.downloadVolume(entity as Volume);
        //this.enqueueDownload(entity as Volume);
        break;
      case 'chapter':
        sizeCheckCall = this.downloadChapterSize((entity as Chapter).id);
        downloadCall = this.downloadChapter(entity as Chapter);
        //this.enqueueDownload(entity as Chapter);
        break;
      case 'bookmark':
        sizeCheckCall = of(0);
        downloadCall = this.downloadBookmarks(entity as PageBookmark[]);
        break;
      case 'logs':
        sizeCheckCall = of(0);
        downloadCall = this.downloadLogs();
        break;
      default:
        return;
    }


    this.accountService.currentUser$.pipe(take(1), switchMap(user => {
      if (user && user.preferences.promptForDownloadSize) {
        return sizeCheckCall;
      }
      return of(0);
    }), switchMap(async (size) => {
      return await this.confirmSize(size, entityType);
    })
    ).pipe(filter(wantsToDownload => {
      return wantsToDownload;
    }),
      filter(_ => downloadCall !== undefined),
      switchMap(() => {
      return (downloadCall || of(undefined)).pipe(
        tap((d) => {
          if (callback) callback(d);
        }),
        takeWhile((val: Download) => {
          return val.state != 'DONE';
        }),
        finalize(() => {
          if (callback) callback(undefined);
        }))
    }), takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {});
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

  private downloadLogs() {
    const downloadType = 'logs';
    const subtitle = this.downloadSubtitle(downloadType, undefined);
    return this.httpClient.get(this.baseUrl + 'server/logs',
      {observe: 'events', responseType: 'blob', reportProgress: true}
    ).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      }),
      tap((d) => this.updateDownloadState(d, downloadType, subtitle, 0)),
      finalize(() => this.finalizeDownloadState(downloadType, subtitle))
    );
  }


  private getIdKey(entity: Chapter | Volume) {
    if (this.utilityService.isVolume(entity)) return 'volumeId';
    if (this.utilityService.isChapter(entity)) return 'chapterId';
    if (this.utilityService.isSeries(entity)) return 'seriesId';
    return 'id';
  }

  private getDownloadEntityType(entity: Chapter | Volume): DownloadEntityType {
    if (this.utilityService.isVolume(entity)) return 'volume';
    if (this.utilityService.isChapter(entity)) return 'chapter';
    if (this.utilityService.isSeries(entity)) return 'series';
    return 'logs'; // This is a hack but it will never occur
  }

  private downloadEntity<T>(entity: Chapter | Volume): Observable<any> {
    const downloadEntityType = this.getDownloadEntityType(entity);
    const subtitle = this.downloadSubtitle(downloadEntityType, entity);
    const idKey = this.getIdKey(entity);
    const url = `${this.baseUrl}download/${downloadEntityType}?${idKey}=${entity.id}`;

    return this.httpClient.get(url, { observe: 'events', responseType: 'blob', reportProgress: true }).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      }),
      tap((d) => this.updateDownloadState(d, downloadEntityType, subtitle, entity.id)),
      finalize(() => this.finalizeDownloadState(downloadEntityType, subtitle))
    );
  }

  private downloadSeries(series: Series) {

    // TODO: Call backend for all the volumes and loose leaf chapters then enqueque them all

    const downloadType = 'series';
    const subtitle = this.downloadSubtitle(downloadType, series);
    return this.httpClient.get(this.baseUrl + 'download/series?seriesId=' + series.id,
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(
              throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
              download((blob, filename) => {
                this.save(blob, decodeURIComponent(filename));
              }),
              tap((d) => this.updateDownloadState(d, downloadType, subtitle, series.id)),
              finalize(() => this.finalizeDownloadState(downloadType, subtitle))
            );
  }

  private finalizeDownloadState(entityType: DownloadEntityType, entitySubtitle: string) {
    let values = this.downloadsSource.getValue();
    values = values.filter(v => !(v.entityType === entityType && v.subTitle === entitySubtitle));
    this.downloadsSource.next(values);
  }

  private updateDownloadState(d: Download, entityType: DownloadEntityType, entitySubtitle: string, id: number) {
    let values = this.downloadsSource.getValue();
    if (d.state === 'PENDING') {
      const index = values.findIndex(v => v.entityType === entityType && v.subTitle === entitySubtitle);
      if (index >= 0) return; // Don't let us duplicate add
      values.push({entityType: entityType, subTitle: entitySubtitle, progress: 0, id});
    } else if (d.state === 'IN_PROGRESS') {
      const index = values.findIndex(v => v.entityType === entityType && v.subTitle === entitySubtitle);
      if (index >= 0) {
        values[index].progress = d.progress;
      }
    } else if (d.state === 'DONE') {
      values = values.filter(v => !(v.entityType === entityType && v.subTitle === entitySubtitle));
    }
    this.downloadsSource.next(values);

  }

  private downloadChapter(chapter: Chapter) {
    return this.downloadEntity(chapter);
  }

  private downloadVolume(volume: Volume) {
    return this.downloadEntity(volume);
  }

  private async confirmSize(size: number, entityType: DownloadEntityType) {
    const showIosWarning = size > this.IOS_SIZE_WARNING && /iPad|iPhone|iPod/.test(navigator.userAgent);
    return (size < this.SIZE_WARNING ||
      await this.confirmService.confirm(translate('toasts.confirm-download-size',
        {entityType: translate('entity-type.' + entityType), size: bytesPipe.transform(size)})
      + (!showIosWarning ? '' : '<br/><br/>' + translate('toasts.confirm-download-size-ios'))));
  }

  private downloadBookmarks(bookmarks: PageBookmark[]) {
    const downloadType = 'bookmark';
    const subtitle = this.downloadSubtitle(downloadType, bookmarks);

    return this.httpClient.post(this.baseUrl + 'download/bookmarks', {bookmarks},
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(
              throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
              download((blob, filename) => {
                this.save(blob, decodeURIComponent(filename));
              }),
              tap((d) => this.updateDownloadState(d, downloadType, subtitle, 0)),
              finalize(() => this.finalizeDownloadState(downloadType, subtitle))
            );
  }



  private processDownload(entity: QueueableDownloadType): void {
    const downloadObservable = this.downloadEntity(entity);
    console.log('Process Download called for entity: ', entity);

    // When we consume one, we need to take it off the queue

    downloadObservable.subscribe((downloadEvent) => {
      // Download completed, process the next item in the queue
      if (downloadEvent.state === 'DONE') {
        this.processNextDownload();
      }
    });
  }

  private processNextDownload(): void {
    const currentQueue = this.downloadQueue.value;
    if (currentQueue.length > 0) {
      const nextEntity = currentQueue[0];
      this.processDownload(nextEntity);
    }
  }

  private enqueueDownload(entity: QueueableDownloadType): void {
    const currentQueue = this.downloadQueue.value;
    const newQueue = [...currentQueue, entity];
    this.downloadQueue.next(newQueue);

    // If the queue was empty, start processing the download
    if (currentQueue.length === 0) {
      this.processNextDownload();
    }
  }

  mapToEntityType(events: DownloadEvent[], entity: Series | Volume | Chapter | UserCollection | PageBookmark | RecentlyAddedItem | NextExpectedChapter | BrowsePerson) {
    if(this.utilityService.isSeries(entity)) {
      return events.find(e => e.entityType === 'series' && e.id == entity.id
        && e.subTitle === this.downloadSubtitle('series', (entity as Series))) || null;
    }

    if(this.utilityService.isVolume(entity)) {
      return events.find(e => e.entityType === 'volume' && e.id == entity.id
        && e.subTitle === this.downloadSubtitle('volume', (entity as Volume))) || null;
    }

    if(this.utilityService.isChapter(entity)) {
      return events.find(e => e.entityType === 'chapter'  && e.id == entity.id
        && e.subTitle === this.downloadSubtitle('chapter', (entity as Chapter))) || null;
    }

    // Is PageBookmark[]
    if(entity.hasOwnProperty('length')) {
      return events.find(e => e.entityType === 'bookmark'
        && e.subTitle === this.downloadSubtitle('bookmark', [(entity as PageBookmark)])) || null;
    }

    return null;
  }
}
