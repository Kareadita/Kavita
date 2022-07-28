import { Injectable, OnDestroy } from '@angular/core';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { take } from 'rxjs/operators';
import { BulkAddToCollectionComponent } from '../cards/_modals/bulk-add-to-collection/bulk-add-to-collection.component';
import { AddToListModalComponent, ADD_FLOW } from '../reading-list/_modals/add-to-list-modal/add-to-list-modal.component';
import { EditReadingListModalComponent } from '../reading-list/_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import { ConfirmService } from '../shared/confirm.service';
import { Chapter } from '../_models/chapter';
import { Library } from '../_models/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { LibraryService } from './library.service';
import { MemberService } from './member.service';
import { ReaderService } from './reader.service';
import { SeriesService } from './series.service';

export type LibraryActionCallback = (library: Partial<Library>) => void;
export type SeriesActionCallback = (series: Series) => void;
export type VolumeActionCallback = (volume: Volume) => void;
export type ChapterActionCallback = (chapter: Chapter) => void;
export type ReadingListActionCallback = (readingList: ReadingList) => void;
export type VoidActionCallback = () => void;
export type BooleanActionCallback = (result: boolean) => void;

/**
 * Responsible for executing actions
 */
@Injectable({
  providedIn: 'root'
})
export class ActionService implements OnDestroy {

  private readonly onDestroy = new Subject<void>();
  private readingListModalRef: NgbModalRef | null = null;
  private collectionModalRef: NgbModalRef | null = null;

  constructor(private libraryService: LibraryService, private seriesService: SeriesService, 
    private readerService: ReaderService, private toastr: ToastrService, private modalService: NgbModal,
    private confirmService: ConfirmService, private memberService: MemberService) { }

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
      this.toastr.info('Scan queued for ' + library.name);
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
  async refreshMetadata(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }

    if (!await this.confirmService.confirm('Refresh covers will force all cover images to be recalculated. This is a heavy operation. Are you sure you don\'t want to perform a Scan instead?')) {
      if (callback) {
        callback(library);
      }
      return;
    }

    this.libraryService.refreshMetadata(library?.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info('Scan queued for ' + library.name);
      if (callback) {
        callback(library);
      }
    });
  }

  /**
   * Request an analysis of files for a given Library (currently just word count)
   * @param library Partial Library, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   * @returns 
   */
   async analyzeFiles(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }

    if (!await this.confirmService.alert('This is a long running process. Please give it the time to complete before invoking again.')) {
      if (callback) {
        callback(library);
      }
      return;
    }

    this.libraryService.analyze(library?.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info('Library file analysis queued for ' + library.name);
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
   * Start a file scan for a Series
   * @param series Series, must have libraryId and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  scanSeries(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.scan(series.libraryId, series.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info('Scan queued for ' + series.name);
      if (callback) {
        callback(series);
      }
    });
  }

  /**
   * Start a file scan for analyze files for a Series
   * @param series Series, must have libraryId and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  analyzeFilesForSeries(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.analyzeFiles(series.libraryId, series.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info('Scan queued for ' + series.name);
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
  async refreshMetdata(series: Series, callback?: SeriesActionCallback) {
    if (!await this.confirmService.confirm('Refresh covers will force all cover images and metadata to be recalculated. This is a heavy operation. Are you sure you don\'t want to perform a Scan instead?')) {
      if (callback) {
        callback(series);
      }
      return;
    }

    this.seriesService.refreshMetadata(series).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info('Refresh covers queued for ' + series.name);
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
    this.readerService.saveProgress(seriesId, chapter.volumeId, chapter.id, 0).pipe(take(1)).subscribe(results => {
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
        volume.pagesRead = 0;
        volume.chapters?.forEach(c => c.pagesRead = 0);
      });
      chapters?.forEach(c => c.pagesRead = 0);
      this.toastr.success('Marked as Read');

      if (callback) {
        callback();
      }
    });
  }

  /**
   * Mark all series as Read.
   * @param series Series, should have id, pagesRead populated
   * @param callback Optional callback to perform actions after API completes 
   */
   markMultipleSeriesAsRead(series: Array<Series>, callback?: VoidActionCallback) {
    this.readerService.markMultipleSeriesRead(series.map(v => v.id)).pipe(take(1)).subscribe(() => {
      series.forEach(s => {
        s.pagesRead = s.pages;
      });
      this.toastr.success('Marked as Read');

      if (callback) {
        callback();
      }
    });
  }

  /**
   * Mark all series as Unread. 
   * @param series Series, should have id, pagesRead populated
   * @param callback Optional callback to perform actions after API completes 
   */
   markMultipleSeriesAsUnread(series: Array<Series>, callback?: VoidActionCallback) {
    this.readerService.markMultipleSeriesUnread(series.map(v => v.id)).pipe(take(1)).subscribe(() => {
      series.forEach(s => {
        s.pagesRead = s.pages;
      });
      this.toastr.success('Marked as Unread');

      if (callback) {
        callback();
      }
    });
  }

  addMultipleToReadingList(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: BooleanActionCallback) {
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
          callback(true);
        }
      });
      this.readingListModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(false);
        }
      });
  }

  addMultipleSeriesToWantToReadList(seriesIds: Array<number>, callback?: VoidActionCallback) {
    this.memberService.addSeriesToWantToRead(seriesIds).subscribe(() => {
      this.toastr.success('Series added to Want to Read list');
      if (callback) {
        callback();
      }
    });
  }

  removeMultipleSeriesFromWantToReadList(seriesIds: Array<number>, callback?: VoidActionCallback) {
    this.memberService.removeSeriesToWantToRead(seriesIds).subscribe(() => {
      this.toastr.success('Series removed from Want to Read list');
      if (callback) {
        callback();
      }
    });
  }

  addMultipleSeriesToReadingList(series: Array<Series>, callback?: BooleanActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md' });
      this.readingListModalRef.componentInstance.seriesIds = series.map(v => v.id);
      this.readingListModalRef.componentInstance.title = 'Multiple Selections';
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Multiple_Series;


      this.readingListModalRef.closed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(true);
        }
      });
      this.readingListModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(false);
        }
      });
  }

  /**
   * Adds a set of series to a collection tag
   * @param series 
   * @param callback 
   * @returns 
   */
  addMultipleSeriesToCollectionTag(series: Array<Series>, callback?: VoidActionCallback) {
    if (this.collectionModalRef != null) { return; }
      this.collectionModalRef = this.modalService.open(BulkAddToCollectionComponent, { scrollable: true, size: 'md', windowClass: 'collection' });
      this.collectionModalRef.componentInstance.seriesIds = series.map(v => v.id);
      this.collectionModalRef.componentInstance.title = 'New Collection';

      this.collectionModalRef.closed.pipe(take(1)).subscribe(() => {
        this.collectionModalRef = null;
        if (callback) {
          callback();
        }
      });
      this.collectionModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.collectionModalRef = null;
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
    const readingListModalRef = this.modalService.open(EditReadingListModalComponent, { scrollable: true, size: 'lg' });
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

  /**
   * Mark all chapters and the volumes as Read. All volumes and chapters must belong to a series
   * @param seriesId Series Id
   * @param volumes Volumes, should have id, chapters and pagesRead populated
   * @param chapters? Chapters, should have id
   * @param callback Optional callback to perform actions after API completes 
   */
   deleteMultipleSeries(seriesIds: Array<Series>, callback?: VoidActionCallback) {
    this.seriesService.deleteMultipleSeries(seriesIds.map(s => s.id)).pipe(take(1)).subscribe(() => {
      this.toastr.success('Series deleted');

      if (callback) {
        callback();
      }
    });
  }

  async deleteSeries(series: Series, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm('Are you sure you want to delete this series? It will not modify files on disk.')) {
      if (callback) {
        callback(false);
      }
      return;
    }

    this.seriesService.delete(series.id).subscribe((res: boolean) => {
      if (callback) {
        this.toastr.success('Series deleted');
        callback(res);
      }
    });
  }
}
