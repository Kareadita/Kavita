import { HttpClient, HttpErrorResponse, HttpEventType } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Series } from 'src/app/_models/series';
import { environment } from 'src/environments/environment';
import { ConfirmService } from '../confirm.service';
import { Chapter } from 'src/app/_models/chapter';
import { Volume } from 'src/app/_models/volume';
import { ToastrService } from 'ngx-toastr';
import { asyncScheduler, BehaviorSubject, Observable, ReplaySubject, tap, finalize, of, map, filter } from 'rxjs';
import { SAVER, Saver } from '../_providers/saver.provider';
import { download, Download } from '../_models/download';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { catchError, switchMap, takeWhile, throttleTime } from 'rxjs/operators';
import { AccountService } from 'src/app/_services/account.service';

export const DEBOUNCE_TIME = 100;

export interface DownloadEvent {
  /**
   * Type of entity being downloaded
   */
  entityType: 'series' | 'volume' | 'chapter' | 'bookmarks';
  /**
   * What to show user. For example, for Series, we might show series name.
   */
  subTitle: string;
  /**
   * Progress of the download itself
   */
  progress: number; 
}

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

  constructor(private httpClient: HttpClient, private confirmService: ConfirmService, 
    private toastr: ToastrService, @Inject(SAVER) private save: Saver, 
    private accountService: AccountService) { }


  public downloadSeriesSize(seriesId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/series-size?seriesId=' + seriesId);
  }

  public downloadVolumeSize(volumeId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/volume-size?volumeId=' + volumeId);
  }

  public downloadChapterSize(chapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/chapter-size?chapterId=' + chapterId);
  }

  downloadLogs() {
    return this.httpClient.get(this.baseUrl + 'server/logs',
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }), download((blob, filename) => {
              this.save(blob, filename)
            }));

  }

  downloadSeries(series: Series) {
    return this.httpClient.get(this.baseUrl + 'download/series?seriesId=' + series.id, 
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(
              throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }), 
              download((blob, filename) => {
                this.save(blob, filename);
              }),
              tap((d) => {
                const values = this.downloadsSource.getValue();
                console.log('download state: ', d.state);
                if (d.state === 'PENDING') {
                  // type 0 is the first event to pass through, so use that to track a new download
                  values.push({entityType: 'series', subTitle: series.name, progress: 0});
                  this.downloadsSource.next(values);
                } else {
                  const index = values.findIndex(v => v.entityType === 'series' && v.subTitle === series.name);
                  if (index >= 0) {
                    values[index].progress = d.progress;
                    this.downloadsSource.next(values);
                    console.log('Updating state to ', d.progress);
                  }
                }
              }),
              finalize(() => {
                let values = this.downloadsSource.getValue();
                values = values.filter(v => !(v.entityType === 'series' && v.subTitle === series.name));
                this.downloadsSource.next(values);
              })
            );
  }

  downloadChapter(chapter: Chapter) {
    return this.httpClient.get(this.baseUrl + 'download/chapter?chapterId=' + chapter.id, 
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }), download((blob, filename) => {
              this.save(blob, filename)
            }));
  }

  downloadVolume(volume: Volume): Observable<Download> {
    return this.httpClient.get(this.baseUrl + 'download/volume?volumeId=' + volume.id, {observe: 'events', responseType: 'blob', reportProgress: true})
      .pipe(throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }), 
            download((blob, filename) => {
              this.save(blob, filename)
            })
      );
  }
  
  async promptForSize(entityType: 'volume' | 'chapter' | 'series', id: number) {
    let sizeCheckCall: Observable<number>; 
    switch (entityType) {
      case 'series':
        sizeCheckCall = this.downloadSeriesSize(id);
        break;
      case 'volume':
        sizeCheckCall = this.downloadVolumeSize(id);
        break;
      case 'chapter':
        sizeCheckCall = this.downloadChapterSize(id);
        break;
    }

    this.accountService.currentUser$.pipe(switchMap(user => {
      if (!user) return of(0);
      // user.preferences.promptForDownloadSize
      if (true) {
        return sizeCheckCall;
      }
    }), map(async (size) => {
        return of(await this.confirmSize(size, entityType));
    })
    );

  }

  async confirmSize(size: number, entityType: 'volume' | 'chapter' | 'series') {
    // TODO: Hook in and check if CheckSizeBeforeDownload enabled
    return (size < this.SIZE_WARNING || await this.confirmService.confirm('The ' + entityType + '  is ' + this.humanFileSize(size) + '. Are you sure you want to continue?'));
  }

  downloadBookmarks(bookmarks: PageBookmark[]) {
    return this.httpClient.post(this.baseUrl + 'download/bookmarks', {bookmarks},
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }), download((blob, filename) => {
              this.save(blob, filename)
            }));
  }

  

  /**
 * Format bytes as human-readable text.
 * 
 * @param bytes Number of bytes.
 * @param si True to use metric (SI) units, aka powers of 1000. False to use 
 *           binary (IEC), aka powers of 1024.
 * @param dp Number of decimal places to display.
 * 
 * @return Formatted string.
 * 
 * Credit: https://stackoverflow.com/questions/10420352/converting-file-size-in-bytes-to-human-readable-string
 */
  private humanFileSize(bytes: number, si=true, dp=0) {
    const thresh = si ? 1000 : 1024;

    if (Math.abs(bytes) < thresh) {
      return bytes + ' B';
    }

    const units = si 
      ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'] 
      : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];
    let u = -1;
    const r = 10**dp;

    do {
      bytes /= thresh;
      ++u;
    } while (Math.round(Math.abs(bytes) * r) / r >= thresh && u < units.length - 1);


    return bytes.toFixed(dp) + ' ' + units[u];
  }
}
