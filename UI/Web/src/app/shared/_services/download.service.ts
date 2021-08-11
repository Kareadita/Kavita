import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Series } from 'src/app/_models/series';
import { environment } from 'src/environments/environment';
import { ConfirmService } from '../confirm.service';
import { saveAs } from 'file-saver';
import { Chapter } from 'src/app/_models/chapter';
import { Volume } from 'src/app/_models/volume';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { SAVER, Saver } from '../_providers/saver.provider';
import { download, Download } from '../_models/download';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { debounce, debounceTime, map, take } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class DownloadService {

  private baseUrl = environment.apiUrl;
  /**
   * Size in bytes in which to inform the user for confirmation before download starts. Defaults to 100 MB.
   */
  public SIZE_WARNING = 104_857_600;

  constructor(private httpClient: HttpClient, private confirmService: ConfirmService, private toastr: ToastrService, @Inject(SAVER) private save: Saver) { }


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
    // this.httpClient.get(this.baseUrl + 'server/logs', {observe: 'response', responseType: 'blob' as 'text'}).subscribe(resp => {
    //   this.preformSave(resp.body || '', this.getFilenameFromHeader(resp.headers, 'logs'));
    // });

    return this.httpClient.get(this.baseUrl + 'server/logs',
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(debounceTime(300), download((blob, filename) => {
              this.save(blob, filename)
            }));

  }

  downloadSeries(series: Series) {
    return this.httpClient.get(this.baseUrl + 'download/series?seriesId=' + series.id, 
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(debounceTime(300), download((blob, filename) => {
              this.save(blob, filename)
            }));
  }

  downloadChapter(chapter: Chapter) {
    return this.httpClient.get(this.baseUrl + 'download/chapter?chapterId=' + chapter.id, 
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(debounceTime(300), download((blob, filename) => {
              this.save(blob, filename)
            }));
  }

  downloadVolume(volume: Volume): Observable<Download> {
    return this.httpClient.get(this.baseUrl + 'download/volume?volumeId=' + volume.id, 
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(debounceTime(300), download((blob, filename) => {
              this.save(blob, filename)
            }));
  }

  async confirmSize(size: number, entityType: 'volume' | 'chapter' | 'series') {
    return (size < this.SIZE_WARNING || await this.confirmService.confirm('The ' + entityType + '  is ' + this.humanFileSize(size) + '. Are you sure you want to continue?'));
  }

  downloadBookmarks(bookmarks: PageBookmark[]) {
    return this.httpClient.post(this.baseUrl + 'download/bookmarks', {bookmarks},
                      {observe: 'events', responseType: 'blob', reportProgress: true}
            ).pipe(debounceTime(300), download((blob, filename) => {
              this.save(blob, filename)
            }));
  }

  private preformSave(res: string, filename: string) {
    const blob = new Blob([res], {type: 'text/plain;charset=utf-8'});
    saveAs(blob, filename);
    this.toastr.success('File downloaded successfully: ' + filename);
  }


  /**
   * Attempts to parse out the filename from Content-Disposition header. 
   * If it fails, will default to defaultName and add the correct extension. If no extension is found in header, will use zip.
   * @param headers 
   * @param defaultName 
   * @returns 
   */
  private getFilenameFromHeader(headers: HttpHeaders, defaultName: string) {
    const tokens = (headers.get('content-disposition') || '').split(';');
    let filename = tokens[1].replace('filename=', '').replace(/"/ig, '').trim();  
    if (filename.startsWith('download_') || filename.startsWith('kavita_download_')) {
      const ext = filename.substring(filename.lastIndexOf('.'), filename.length);
      return defaultName + ext;
    }
    return filename;
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
