import { Injectable, OnDestroy } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { forkJoin, Subject } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';
import { Chapter } from '../_models/chapter';
import { Library } from '../_models/library';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { LibraryService } from './library.service';
import { ReaderService } from './reader.service';
import { SeriesService } from './series.service';

export type LibraryActionCallback = (library: Partial<Library>) => void;
export type SeriesActionCallback = (series: Series) => void;
export type VolumeActionCallback = (volume: Volume) => void;
export type ChapterActionCallback = (chapter: Chapter) => void;

/**
 * Responsible for executing actions
 */
@Injectable({
  providedIn: 'root'
})
export class ActionService implements OnDestroy {

  private readonly onDestroy = new Subject<void>();

  constructor(private libraryService: LibraryService, private seriesService: SeriesService, 
    private readerService: ReaderService, private toastr: ToastrService) { }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

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
    this.libraryService.scan(library?.id).pipe(take(1)).subscribe((res: any) => {
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

    this.libraryService.refreshMetadata(library?.id).pipe(take(1)).subscribe((res: any) => {
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
    this.seriesService.markRead(series.id).pipe(take(1)).subscribe(res => {
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
    this.seriesService.markUnread(series.id).pipe(take(1)).subscribe(res => {
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
    this.seriesService.scan(series.libraryId, series.id).pipe(take(1)).subscribe((res: any) => {
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
    this.seriesService.refreshMetadata(series).pipe(take(1)).subscribe((res: any) => {
      this.toastr.success('Refresh started for ' + series.name);
      if (callback) {
        callback(series);
      }
    });
  }

  /**
   * Mark all chapters and the volume as Read
   * @param seriesId Series Id
   * @param volume Volume, should have id, chapters and pagesRead populated
   * @param callback Optional callback to perform actions after API completes
   */
  markVolumeAsRead(seriesId: number, volume: Volume, callback?: VolumeActionCallback) {
    this.readerService.markVolumeRead(seriesId, volume.id).pipe(take(1)).subscribe(() => {
      volume.pagesRead = volume.pages;
      volume.chapters?.forEach(c => c.pagesRead = c.pages);
      this.toastr.success('Marked as Read');

      if (callback) {
        callback(volume);
      }
    });
  }

  /**
   * Mark all chapters and the volume as unread
   * @param seriesId Series Id
   * @param volume Volume, should have id, chapters and pagesRead populated
   * @param callback Optional callback to perform actions after API completes
   */
  markVolumeAsUnread(seriesId: number, volume: Volume, callback?: VolumeActionCallback) {
    forkJoin(volume.chapters?.map(chapter => this.readerService.bookmark(seriesId, volume.id, chapter.id, 0))).pipe(takeUntil(this.onDestroy)).subscribe(results => {
      volume.pagesRead = 0;
      volume.chapters?.forEach(c => c.pagesRead = 0);
      this.toastr.success('Marked as Unread');
      if (callback) {
        callback(volume);
      }
    });
  }

  /**
   * Mark a chapter as read
   * @param seriesId Series Id
   * @param chapter Chapter, should have id, pages, volumeId populated
   * @param callback Optional callback to perform actions after API completes
   */
  markChapterAsRead(seriesId: number, chapter: Chapter, callback?: ChapterActionCallback) {
    this.readerService.bookmark(seriesId, chapter.volumeId, chapter.id, chapter.pages).pipe(take(1)).subscribe(results => {
      chapter.pagesRead = chapter.pages;
      this.toastr.success('Marked as Read');
      if (callback) {
        callback(chapter);
      }
    });
  }

  /**
   * Mark a chapter as unread
   * @param seriesId Series Id
   * @param chapter Chapter, should have id, pages, volumeId populated
   * @param callback Optional callback to perform actions after API completes
   */
  markChapterAsUnread(seriesId: number, chapter: Chapter, callback?: ChapterActionCallback) {
    this.readerService.bookmark(seriesId, chapter.volumeId, chapter.id, chapter.pages).pipe(take(1)).subscribe(results => {
      chapter.pagesRead = 0;
      this.toastr.success('Marked as unread');
      if (callback) {
        callback(chapter);
      }
    });
  }

  
}
