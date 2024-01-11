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
import {translate} from "@ngneat/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SAVER, Saver} from "../../_providers/saver.provider";

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

  private downloadsSource: BehaviorSubject<DownloadEvent[]> = new BehaviorSubject<DownloadEvent[]>([]);
  public activeDownloads$ = this.downloadsSource.asObservable();

  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  private readonly httpClient = inject(HttpClient);

  constructor(@Inject(SAVER) private save: Saver) { }


  /**
   * Returns the entity subtitle (for the event widget) for a given entity
   * @param downloadEntityType
   * @param downloadEntity
   * @returns
   */
   downloadSubtitle(downloadEntityType: DownloadEntityType, downloadEntity: DownloadEntity | undefined) {
    switch (downloadEntityType) {
      case 'series':
        return (downloadEntity as Series).name;
      case 'volume':
        return (downloadEntity as Volume).number + '';
      case 'chapter':
        return (downloadEntity as Chapter).number;
      case 'bookmark':
        return '';
      case 'logs':
        return '';
    }
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
        break;
      case 'chapter':
        sizeCheckCall = this.downloadChapterSize((entity as Chapter).id);
        downloadCall = this.downloadChapter(entity as Chapter);
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
    }), switchMap(() => {
      return downloadCall.pipe(
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
      tap((d) => this.updateDownloadState(d, downloadType, subtitle)),
      finalize(() => this.finalizeDownloadState(downloadType, subtitle))
    );
  }

  private downloadSeries(series: Series) {
    const downloadType = 'series';
    const subtitle = this.downloadSubtitle(downloadType, series);
    return this.httpClient.get(this.baseUrl + 'download/series?seriesId=' + series.id,
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(
              throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
              download((blob, filename) => {
                this.save(blob, decodeURIComponent(filename));
              }),
              tap((d) => this.updateDownloadState(d, downloadType, subtitle)),
              finalize(() => this.finalizeDownloadState(downloadType, subtitle))
            );
  }

  private finalizeDownloadState(entityType: DownloadEntityType, entitySubtitle: string) {
    let values = this.downloadsSource.getValue();
    values = values.filter(v => !(v.entityType === entityType && v.subTitle === entitySubtitle));
    this.downloadsSource.next(values);
  }

  private updateDownloadState(d: Download, entityType: DownloadEntityType, entitySubtitle: string) {
    let values = this.downloadsSource.getValue();
    if (d.state === 'PENDING') {
      const index = values.findIndex(v => v.entityType === entityType && v.subTitle === entitySubtitle);
      if (index >= 0) return; // Don't let us duplicate add
      values.push({entityType: entityType, subTitle: entitySubtitle, progress: 0});
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
    const downloadType = 'chapter';
    const subtitle = this.downloadSubtitle(downloadType, chapter);
    return this.httpClient.get(this.baseUrl + 'download/chapter?chapterId=' + chapter.id,
                {observe: 'events', responseType: 'blob', reportProgress: true}
        ).pipe(
          throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
          download((blob, filename) => {
            this.save(blob, decodeURIComponent(filename));
          }),
          tap((d) => this.updateDownloadState(d, downloadType, subtitle)),
          finalize(() => this.finalizeDownloadState(downloadType, subtitle))
        );
  }

  private downloadVolume(volume: Volume): Observable<Download> {
    const downloadType = 'volume';
    const subtitle = this.downloadSubtitle(downloadType, volume);
    return this.httpClient.get(this.baseUrl + 'download/volume?volumeId=' + volume.id,
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(
              throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
              download((blob, filename) => {
                this.save(blob, decodeURIComponent(filename));
              }),
              tap((d) => this.updateDownloadState(d, downloadType, subtitle)),
              finalize(() => this.finalizeDownloadState(downloadType, subtitle))
            );
  }

  private async confirmSize(size: number, entityType: DownloadEntityType) {
    return (size < this.SIZE_WARNING ||
      await this.confirmService.confirm(translate('toasts.confirm-download-size', {entityType: translate('entity-type.' + entityType), size: bytesPipe.transform(size)})));
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
              tap((d) => this.updateDownloadState(d, downloadType, subtitle)),
              finalize(() => this.finalizeDownloadState(downloadType, subtitle))
            );
  }
}
