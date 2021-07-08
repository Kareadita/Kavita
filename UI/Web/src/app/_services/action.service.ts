import { Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { ConfirmService } from '../shared/confirm.service';
import { Library } from '../_models/library';
import { Series } from '../_models/series';
import { LibraryService } from './library.service';
import { SeriesService } from './series.service';

export type LibraryActionCallback = (library: Partial<Library>) => void;
export type SeriesActionCallback = (series: Series) => void;

/**
 * Responsible for executing actions
 */
@Injectable({
  providedIn: 'root'
})
export class ActionService {

  constructor(private libraryService: LibraryService, private seriesService: SeriesService, private confirmService: ConfirmService, private toastr: ToastrService) { }

  /**
   * Request a file scan for a given Library
   * @param library Partial Library, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   * @returns 
   */
  scanLibrary(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }
    this.libraryService.scan(library?.id).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + library.name);
      if (callback) {
        callback(library);
      }
    });
  }

  /**
   * Request a refresh of Metadata for a given Library
   * @param library Partial Library, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   * @returns 
   */
  refreshMetadata(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }

    this.libraryService.refreshMetadata(library?.id).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + library.name);
      if (callback) {
        callback(library);
      }
    });
  }

  /**
   * Mark a series as read; updates the series pagesRead
   * @param series Series, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  markSeriesAsRead(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.markRead(series.id).subscribe(res => {
      series.pagesRead = series.pages;
      this.toastr.success(series.name + ' is now read');
      if (callback) {
        callback(series);
      }
    });
  }

  /**
   * Mark a series as unread; updates the series pagesRead
   * @param series Series, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  markSeriesAsUnread(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.markUnread(series.id).subscribe(res => {
      series.pagesRead = 0;
      this.toastr.success(series.name + ' is now unread');
      if (callback) {
        callback(series);
      }
    });
  }

  /**
   * Start a file scan for a Series (currently just does the library not the series directly)
   * @param series Series, must have libraryId and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  scanSeries(series: Series, callback?: SeriesActionCallback) {
    this.libraryService.scan(series.libraryId).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + series.name);
      if (callback) {
        callback(series);
      }
    });
  }

  /**
   * Start a metadata refresh for a Series
   * @param series Series, must have libraryId, id and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  refreshMetdata(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.refreshMetadata(series).subscribe((res: any) => {
      this.toastr.success('Refresh started for ' + series.name);
      if (callback) {
        callback(series);
      }
    });
  }

  
}
