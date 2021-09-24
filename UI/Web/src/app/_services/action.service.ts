import { Injectable, OnDestroy } from '@angular/core';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { take } from 'rxjs/operators';
import { BookmarksModalComponent } from '../cards/_modals/bookmarks-modal/bookmarks-modal.component';
import { AddToListModalComponent, ADD_FLOW } from '../reading-list/_modals/add-to-list-modal/add-to-list-modal.component';
import { EditReadingListModalComponent } from '../reading-list/_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import { Chapter } from '../_models/chapter';
import { Library } from '../_models/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { LibraryService } from './library.service';
import { ReaderService } from './reader.service';
import { SeriesService } from './series.service';

export type LibraryActionCallback = (library: Partial<Library>) => void;
export type SeriesActionCallback = (series: Series) => void;
export type VolumeActionCallback = (volume: Volume) => void;
export type ChapterActionCallback = (chapter: Chapter) => void;
export type ReadingListActionCallback = (readingList: ReadingList) => void;
export type VoidActionCallback = () => void;

/**
 * Responsible for executing actions
 */
@Injectable({
  providedIn: 'root'
})
export class ActionService implements OnDestroy {

  private readonly onDestroy = new Subject<void>();
  private bookmarkModalRef: NgbModalRef | null = null;
  private readingListModalRef: NgbModalRef | null = null;

  constructor(private libraryService: LibraryService, private seriesService: SeriesService, 
    private readerService: ReaderService, private toastr: ToastrService, private modalService: NgbModal) { }

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
    this.readerService.markVolumeUnread(seriesId, volume.id).subscribe(() => {
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
    this.readerService.saveProgress(seriesId, chapter.volumeId, chapter.id, chapter.pages).pipe(take(1)).subscribe(results => {
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
    this.readerService.saveProgress(seriesId, chapter.volumeId, chapter.id, chapter.pages).pipe(take(1)).subscribe(results => {
      chapter.pagesRead = 0;
      this.toastr.success('Marked as unread');
      if (callback) {
        callback(chapter);
      }
    });
  }

  /**
   * Mark all chapters and the volumes as Read. All volumes and chapters must belong to a series
   * @param seriesId Series Id
   * @param volumes Volumes, should have id, chapters and pagesRead populated
   * @param chapters? Chapters, should have id
   * @param callback Optional callback to perform actions after API completes 
   */
   markMultipleAsRead(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: VoidActionCallback) {
    this.readerService.markMultipleRead(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).pipe(take(1)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = volume.pages;
        volume.chapters?.forEach(c => c.pagesRead = c.pages);
      });
      chapters?.forEach(c => c.pagesRead = c.pages);
      this.toastr.success('Marked as Read');

      if (callback) {
        callback();
      }
    });
  }

  /**
   * Mark all chapters and the volumes as Unread. All volumes must belong to a series
   * @param seriesId Series Id
   * @param volumes Volumes, should have id, chapters and pagesRead populated
   * @param callback Optional callback to perform actions after API completes 
   */
   markMultipleAsUnread(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: VoidActionCallback) {
    this.readerService.markMultipleUnread(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).pipe(take(1)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = volume.pages;
        volume.chapters?.forEach(c => c.pagesRead = c.pages);
      });
      chapters?.forEach(c => c.pagesRead = c.pages);
      this.toastr.success('Marked as Read');

      if (callback) {
        callback();
      }
    });
  }


  openBookmarkModal(series: Series, callback?: SeriesActionCallback) {
    if (this.bookmarkModalRef != null) { return; }
      this.bookmarkModalRef = this.modalService.open(BookmarksModalComponent, { scrollable: true, size: 'lg' });
      this.bookmarkModalRef.componentInstance.series = series;
      this.bookmarkModalRef.closed.pipe(take(1)).subscribe(() => {
        this.bookmarkModalRef = null;
        if (callback) {
          callback(series);
        }
      });
      this.bookmarkModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.bookmarkModalRef = null;
        if (callback) {
          callback(series);
        }
      });
  }

  addMultipleToReadingList(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: VoidActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md' });
      this.readingListModalRef.componentInstance.seriesId = seriesId;
      this.readingListModalRef.componentInstance.volumeIds = volumes.map(v => v.id);
      this.readingListModalRef.componentInstance.chapterIds = chapters?.map(c => c.id);
      this.readingListModalRef.componentInstance.title = 'Multiple Selections';
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Multiple;


      this.readingListModalRef.closed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback();
        }
      });
      this.readingListModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback();
        }
      });
  }

  addSeriesToReadingList(series: Series, callback?: SeriesActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md' });
      this.readingListModalRef.componentInstance.seriesId = series.id; 
      this.readingListModalRef.componentInstance.title = series.name;
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Series;


      this.readingListModalRef.closed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(series);
        }
      });
      this.readingListModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(series);
        }
      });
  }

  addVolumeToReadingList(volume: Volume, seriesId: number, callback?: VolumeActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md' });
      this.readingListModalRef.componentInstance.seriesId = seriesId; 
      this.readingListModalRef.componentInstance.volumeId = volume.id;
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Volume;


      this.readingListModalRef.closed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(volume);
        }
      });
      this.readingListModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(volume);
        }
      });
  }

  addChapterToReadingList(chapter: Chapter, seriesId: number, callback?: ChapterActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md' });
      this.readingListModalRef.componentInstance.seriesId = seriesId; 
      this.readingListModalRef.componentInstance.chapterId = chapter.id;
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Chapter;


      this.readingListModalRef.closed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(chapter);
        }
      });
      this.readingListModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(chapter);
        }
      });
  }

  editReadingList(readingList: ReadingList, callback?: ReadingListActionCallback) {
    const readingListModalRef = this.modalService.open(EditReadingListModalComponent, { scrollable: true, size: 'md' });
    readingListModalRef.componentInstance.readingList = readingList; 
    readingListModalRef.closed.pipe(take(1)).subscribe((list) => {
      if (callback && list !== undefined) {
        callback(readingList);
      }
    });
    readingListModalRef.dismissed.pipe(take(1)).subscribe((list) => {
      if (callback && list !== undefined) {
        callback(readingList);
      }
    });
  }

}
