import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Series } from 'src/app/_models/series';
import { environment } from 'src/environments/environment';
import { ConfirmService } from '../confirm.service';
import { saveAs } from 'file-saver';
import { Chapter } from 'src/app/_models/chapter';
import { Volume } from 'src/app/_models/volume';
import { ToastrService } from 'ngx-toastr';

@Injectable({
  providedIn: 'root'
})
export class DownloadService {

  private baseUrl = environment.apiUrl;
  /**
   * Size in bytes in which to inform the user for confirmation before download starts. Defaults to 100 MB.
   */
  public SIZE_WARNING = 104_857_600;

  constructor(private httpClient: HttpClient, private confirmService: ConfirmService, private toastr: ToastrService) { }


  private downloadSeriesSize(seriesId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/series-size?seriesId=' + seriesId);
  }

  private downloadVolumeSize(volumeId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/volume-size?volumeId=' + volumeId);
  }

  private downloadChapterSize(chapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'download/chapter-size?chapterId=' + chapterId);
  }

  private downloadSeriesAPI(seriesId: number) {
    return this.httpClient.get(this.baseUrl + 'download/series?seriesId=' + seriesId, {observe: 'response', responseType: 'blob' as 'text'});
  }

  private downloadVolumeAPI(volumeId: number) {
    return this.httpClient.get(this.baseUrl + 'download/volume?volumeId=' + volumeId, {observe: 'response', responseType: 'blob' as 'text'});
  }

  private downloadChapterAPI(chapterId: number) {
    return this.httpClient.get(this.baseUrl + 'download/chapter?chapterId=' + chapterId, {observe: 'response', responseType: 'blob' as 'text'});
  }

  downloadLogs() {
    this.httpClient.get(this.baseUrl + 'server/logs', {observe: 'response', responseType: 'blob' as 'text'}).subscribe(resp => {
      this.preformSave(resp.body || '', this.getFilenameFromHeader(resp.headers, 'logs'));
    });
  }

  downloadSeries(series: Series) {
    this.downloadSeriesSize(series.id).subscribe(async size => {
      if (size >= this.SIZE_WARNING && !await this.confirmService.confirm('The series is ' + this.humanFileSize(size) + '. Are you sure you want to continue?')) {
        return;
      }
      this.downloadSeriesAPI(series.id).subscribe(resp => {
        this.preformSave(resp.body || '', this.getFilenameFromHeader(resp.headers, series.name));
      });
    });
  }

  downloadChapter(chapter: Chapter, seriesName: string) {
    this.downloadChapterSize(chapter.id).subscribe(async size => {
      if (size >= this.SIZE_WARNING && !await this.confirmService.confirm('The chapter is ' + this.humanFileSize(size) + '. Are you sure you want to continue?')) {
        return;
      }
      this.downloadChapterAPI(chapter.id).subscribe((resp: HttpResponse<string>) => {
        this.preformSave(resp.body || '', this.getFilenameFromHeader(resp.headers, seriesName + ' - Chapter ' + chapter.number));
      });
    });
  }

  downloadVolume(volume: Volume, seriesName: string) {
    this.downloadVolumeSize(volume.id).subscribe(async size => {
      if (size >= this.SIZE_WARNING && !await this.confirmService.confirm('The chapter is ' + this.humanFileSize(size) + '. Are you sure you want to continue?')) {
        return;
      }
      this.downloadVolumeAPI(volume.id).subscribe(resp => {
        this.preformSave(resp.body || '', this.getFilenameFromHeader(resp.headers, seriesName + ' - Volume ' + volume.name));
      });
    });
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
