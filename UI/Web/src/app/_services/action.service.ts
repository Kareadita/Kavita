import {inject, Injectable} from '@angular/core';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { BulkAddToCollectionComponent } from '../cards/_modals/bulk-add-to-collection/bulk-add-to-collection.component';
import { AddToListModalComponent, ADD_FLOW } from '../reading-list/_modals/add-to-list-modal/add-to-list-modal.component';
import { EditReadingListModalComponent } from '../reading-list/_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import { ConfirmService } from '../shared/confirm.service';
import { LibrarySettingsModalComponent } from '../sidenav/_modals/library-settings-modal/library-settings-modal.component';
import { Chapter } from '../_models/chapter';
import { Device } from '../_models/device/device';
import { Library } from '../_models/library/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { DeviceService } from './device.service';
import { LibraryService } from './library.service';
import { MemberService } from './member.service';
import { ReaderService } from './reader.service';
import { SeriesService } from './series.service';
import {translate} from "@jsverse/transloco";
import {UserCollection} from "../_models/collection-tag";
import {CollectionTagService} from "./collection-tag.service";
import {FilterService} from "./filter.service";
import {ReadingListService} from "./reading-list.service";
import {ChapterService} from "./chapter.service";
import {VolumeService} from "./volume.service";
import {DefaultModalOptions} from "../_models/default-modal-options";
import {MatchSeriesModalComponent} from "../_single-module/match-series-modal/match-series-modal.component";


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
export class ActionService {

  private readonly chapterService = inject(ChapterService);
  private readonly volumeService = inject(VolumeService);
  private readonly libraryService = inject(LibraryService);
  private readonly seriesService = inject(SeriesService);
  private readonly readerService = inject(ReaderService);
  private readonly toastr = inject(ToastrService);
  private readonly modalService = inject(NgbModal);
  private readonly confirmService = inject(ConfirmService);
  private readonly memberService = inject(MemberService);
  private readonly deviceService = inject(DeviceService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly filterService = inject(FilterService);
  private readonly readingListService = inject(ReadingListService);


  private readingListModalRef: NgbModalRef | null = null;
  private collectionModalRef: NgbModalRef | null = null;


  /**
   * Request a file scan for a given Library
   * @param library Partial Library, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   * @returns
   */
  async scanLibrary(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }

    // Prompt user if we should do a force or not
    const force = false; // await this.promptIfForce();

    this.libraryService.scan(library.id, force).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info(translate('toasts.scan-queued', {name: library.name}));
      if (callback) {
        callback(library);
      }
    });
  }


  /**
   * Request a refresh of Metadata for a given Library
   * @param library Partial Library, must have id and name populated
   * @param callback Optional callback to perform actions after API completes
   * @param forceUpdate Optional Should we force
   * @param forceColorscape Optional Should we force colorscape gen
   * @returns
   */
  async refreshLibraryMetadata(library: Partial<Library>, callback?: LibraryActionCallback, forceUpdate: boolean = true, forceColorscape: boolean = false) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }

    // Prompt the user if we are doing a forced call
    if (forceUpdate) {
      if (!await this.confirmService.confirm(translate('toasts.confirm-regen-covers'))) {
        if (callback) {
          callback(library);
        }
        return;
      }
    }

    const message = forceUpdate ? 'toasts.refresh-covers-queued' : 'toasts.generate-colorscape-queued';

    this.libraryService.refreshMetadata(library?.id, forceUpdate, forceColorscape).subscribe((res: any) => {
      this.toastr.info(translate(message, {name: library.name}));

      if (callback) {
        callback(library);
      }
    });
  }

  editLibrary(library: Partial<Library>, callback?: LibraryActionCallback) {
    const modalRef = this.modalService.open(LibrarySettingsModalComponent, DefaultModalOptions);
      modalRef.componentInstance.library = library;
      modalRef.closed.subscribe((closeResult: {success: boolean, library: Library, coverImageUpdate: boolean}) => {
        if (callback) callback(library)
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

    if (!await this.confirmService.alert(translate('toasts.alert-long-running'))) {
      if (callback) {
        callback(library);
      }
      return;
    }

    this.libraryService.analyze(library?.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info(translate('toasts.library-file-analysis-queued', {name: library.name}));
      if (callback) {
        callback(library);
      }
    });
  }

  async deleteLibrary(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }

    if (!await this.confirmService.alert(translate('toasts.confirm-library-delete'))) {
      if (callback) {
        callback(library);
      }
      return;
    }

    this.libraryService.delete(library?.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info(translate('toasts.library-deleted', {name: library.name}));
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
      this.toastr.success(translate('toasts.entity-read', {name: series.name}));
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
      this.toastr.success(translate('toasts.entity-unread', {name: series.name}));
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
  async scanSeries(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.scan(series.libraryId, series.id).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info(translate('toasts.scan-queued', {name: series.name}));
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
      this.toastr.info(translate('toasts.scan-queued', {name: series.name}));
      if (callback) {
        callback(series);
      }
    });
  }

  /**
   * Start a metadata refresh for a Series
   * @param series Series, must have libraryId, id and name populated
   * @param callback Optional callback to perform actions after API completes
   * @param forceUpdate If cache should be checked or not
   * @param forceColorscape If cache should be checked or not
   */
  async refreshSeriesMetadata(series: Series, callback?: SeriesActionCallback, forceUpdate: boolean = true, forceColorscape: boolean = false) {

    // Prompt the user if we are doing a forced call
    if (forceUpdate) {
      if (!await this.confirmService.confirm(translate('toasts.confirm-regen-covers'))) {
        if (callback) {
          callback(series);
        }
        return;
      }
    }

    const message = forceUpdate ? 'toasts.refresh-covers-queued' : 'toasts.generate-colorscape-queued';

    this.seriesService.refreshMetadata(series, forceUpdate, forceColorscape).pipe(take(1)).subscribe((res: any) => {
      this.toastr.info(translate(message, {name: series.name}));
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
      this.toastr.success(translate('toasts.mark-read'));

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
      this.toastr.success(translate('toasts.mark-unread'));
      if (callback) {
        callback(volume);
      }
    });
  }

  /**
   * Mark a chapter as read
   * @param libraryId Library Id
   * @param seriesId Series Id
   * @param chapter Chapter, should have id, pages, volumeId populated
   * @param callback Optional callback to perform actions after API completes
   */
  markChapterAsRead(libraryId: number, seriesId: number, chapter: Chapter, callback?: ChapterActionCallback) {
    this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, chapter.pages).pipe(take(1)).subscribe(results => {
      chapter.pagesRead = chapter.pages;
      this.toastr.success(translate('toasts.mark-read'));
      if (callback) {
        callback(chapter);
      }
    });
  }

  /**
   * Mark a chapter as unread
   * @param libraryId Library Id
   * @param seriesId Series Id
   * @param chapter Chapter, should have id, pages, volumeId populated
   * @param callback Optional callback to perform actions after API completes
   */
  markChapterAsUnread(libraryId: number, seriesId: number, chapter: Chapter, callback?: ChapterActionCallback) {
    this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, 0).pipe(take(1)).subscribe(results => {
      chapter.pagesRead = 0;
      this.toastr.success(translate('toasts.mark-unread'));
      if (callback) {
        callback(chapter);
      }
    });
  }

  /**
   * Mark all chapters and the volumes as Read. All volumes and chapters must belong to a series
   * @param seriesId Series Id
   * @param volumes Volumes, should have id, chapters and pagesRead populated
   * @param chapters Optional Chapters, should have id
   * @param callback Optional callback to perform actions after API completes
   */
   markMultipleAsRead(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: VoidActionCallback) {
    this.readerService.markMultipleRead(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).pipe(take(1)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = volume.pages;
        volume.chapters?.forEach(c => c.pagesRead = c.pages);
      });
      chapters?.forEach(c => c.pagesRead = c.pages);
      this.toastr.success(translate('toasts.mark-read'));

      if (callback) {
        callback();
      }
    });
  }

  /**
   * Mark all chapters and the volumes as Unread. All volumes must belong to a series
   * @param seriesId Series Id
   * @param volumes Volumes, should have id, chapters and pagesRead populated
   * @param chapters Optional Chapters, should have id
   * @param callback Optional callback to perform actions after API completes
   */
   markMultipleAsUnread(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: VoidActionCallback) {
    this.readerService.markMultipleUnread(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).pipe(take(1)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = 0;
        volume.chapters?.forEach(c => c.pagesRead = 0);
      });
      chapters?.forEach(c => c.pagesRead = 0);
      this.toastr.success(translate('toasts.mark-unread'));

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
      this.toastr.success(translate('toasts.mark-read'));

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
      this.toastr.success(translate('toasts.mark-unread'));

      if (callback) {
        callback();
      }
    });
  }

  /**
   * Mark all collections as promoted/unpromoted.
   * @param collections UserCollection, should have id, pagesRead populated
   * @param promoted boolean, promoted state
   * @param callback Optional callback to perform actions after API completes
   */
  promoteMultipleCollections(collections: Array<UserCollection>, promoted: boolean, callback?: BooleanActionCallback) {
    this.collectionTagService.promoteMultipleCollections(collections.map(v => v.id), promoted).pipe(take(1)).subscribe(() => {
      if (promoted) {
        this.toastr.success(translate('toasts.collections-promoted'));
      } else {
        this.toastr.success(translate('toasts.collections-unpromoted'));
      }

      if (callback) {
        callback(true);
      }
    });
  }

  /**
   * Deletes multiple collections
   * @param collections UserCollection, should have id, pagesRead populated
   * @param callback Optional callback to perform actions after API completes
   */
  async deleteMultipleCollections(collections: Array<UserCollection>, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-collections'))) return;

    this.collectionTagService.deleteMultipleCollections(collections.map(v => v.id)).pipe(take(1)).subscribe(() => {
      this.toastr.success(translate('toasts.collections-deleted'));

      if (callback) {
        callback(true);
      }
    });
  }

  /**
   * Mark all reading lists as promoted/unpromoted.
   * @param readingLists UserCollection, should have id, pagesRead populated
   * @param promoted boolean, promoted state
   * @param callback Optional callback to perform actions after API completes
   */
  promoteMultipleReadingLists(readingLists: Array<ReadingList>, promoted: boolean, callback?: BooleanActionCallback) {
    this.readingListService.promoteMultipleReadingLists(readingLists.map(v => v.id), promoted).pipe(take(1)).subscribe(() => {
      if (promoted) {
        this.toastr.success(translate('toasts.reading-list-promoted'));
      } else {
        this.toastr.success(translate('toasts.reading-list-unpromoted'));
      }

      if (callback) {
        callback(true);
      }
    });
  }

  async deleteMultipleChapters(seriesId: number, chapterIds: Array<Chapter>, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-multiple-chapters'))) return;

    this.chapterService.deleteMultipleChapters(seriesId, chapterIds.map(c => c.id)).subscribe(() => {
      if (callback) {
        callback(true);
      }
    });
  }

  /**
   * Deletes multiple collections
   * @param readingLists ReadingList, should have id
   * @param callback Optional callback to perform actions after API completes
   */
  async deleteMultipleReadingLists(readingLists: Array<ReadingList>, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-reading-list'))) return;

    this.readingListService.deleteMultipleReadingLists(readingLists.map(v => v.id)).pipe(take(1)).subscribe(() => {
      this.toastr.success(translate('toasts.reading-lists-deleted'));

      if (callback) {
        callback(true);
      }
    });
  }

  addMultipleToReadingList(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: BooleanActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
      this.readingListModalRef.componentInstance.seriesId = seriesId;
      this.readingListModalRef.componentInstance.volumeIds = volumes.map(v => v.id);
      this.readingListModalRef.componentInstance.chapterIds = chapters?.map(c => c.id);
      this.readingListModalRef.componentInstance.title = translate('actionable.multiple-selections');
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
      this.toastr.success(translate('toasts.series-removed-want-to-read'));
      if (callback) {
        callback();
      }
    });
  }

  addMultipleSeriesToReadingList(series: Array<Series>, callback?: BooleanActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
      this.readingListModalRef.componentInstance.seriesIds = series.map(v => v.id);
      this.readingListModalRef.componentInstance.title = translate('actionable.multiple-selections');
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
  addMultipleSeriesToCollectionTag(series: Array<Series>, callback?: BooleanActionCallback) {
    if (this.collectionModalRef != null) { return; }
      this.collectionModalRef = this.modalService.open(BulkAddToCollectionComponent, { scrollable: true, size: 'md', windowClass: 'collection', fullscreen: 'md' });
      this.collectionModalRef.componentInstance.seriesIds = series.map(v => v.id);
      this.collectionModalRef.componentInstance.title = translate('actionable.new-collection');

      this.collectionModalRef.closed.pipe(take(1)).subscribe(() => {
        this.collectionModalRef = null;
        if (callback) {
          callback(true);
        }
      });
      this.collectionModalRef.dismissed.pipe(take(1)).subscribe(() => {
        this.collectionModalRef = null;
        if (callback) {
          callback(false);
        }
      });
  }

  addSeriesToReadingList(series: Series, callback?: SeriesActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
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
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
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
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
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
    const readingListModalRef = this.modalService.open(EditReadingListModalComponent, { scrollable: true, size: 'lg', fullscreen: 'md' });
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
   * Deletes all series
   * @param seriesIds - List of series
   * @param callback - Optional callback once complete
   */
   async deleteMultipleSeries(seriesIds: Array<Series>, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-multiple-series', {count: seriesIds.length}))) {
      if (callback) {
        callback(false);
      }
      return;
    }
    this.seriesService.deleteMultipleSeries(seriesIds.map(s => s.id)).pipe(take(1)).subscribe(res => {
      if (res) {
        this.toastr.success(translate('toasts.series-deleted'));
      } else {
        this.toastr.error(translate('errors.generic'));
      }

      if (callback) {
        callback(res);
      }
    });
  }

  async deleteSeries(series: Series, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-series'))) {
      if (callback) {
        callback(false);
      }
      return;
    }

    this.seriesService.delete(series.id).subscribe((res: boolean) => {
      if (callback) {
        if (res) {
          this.toastr.success(translate('toasts.series-deleted'));
        } else {
          this.toastr.error(translate('errors.generic'));
        }

        callback(res);
      }
    });
  }

  async deleteChapter(chapterId: number, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-chapter'))) {
      if (callback) {
        callback(false);
      }
      return;
    }

    this.chapterService.deleteChapter(chapterId).subscribe((res: boolean) => {
      if (callback) {
        if (res) {
          this.toastr.success(translate('toasts.chapter-deleted'));
        } else {
          this.toastr.error(translate('errors.generic'));
        }

        callback(res);
      }
    });
  }

  async deleteVolume(volumeId: number, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-volume'))) {
      if (callback) {
        callback(false);
      }
      return;
    }

    this.volumeService.deleteVolume(volumeId).subscribe((res: boolean) => {
      if (callback) {
        if (res) {
          this.toastr.success(translate('toasts.volume-deleted'));
        } else {
          this.toastr.error(translate('errors.generic'));
        }

        callback(res);
      }
    });
  }

  sendToDevice(chapterIds: Array<number>, device: Device, callback?: VoidActionCallback) {
    this.deviceService.sendTo(chapterIds, device.id).subscribe(() => {
      this.toastr.success(translate('toasts.file-send-to', {name: device.name}));
      if (callback) {
        callback();
      }
    });
  }

  sendSeriesToDevice(seriesId: number, device: Device, callback?: VoidActionCallback) {
    this.deviceService.sendSeriesTo(seriesId, device.id).subscribe(() => {
      this.toastr.success(translate('toasts.file-send-to', {name: device.name}));
      if (callback) {
        callback();
      }
    });
  }

  matchSeries(series: Series, callback?: BooleanActionCallback) {
   const ref = this.modalService.open(MatchSeriesModalComponent, {size: 'lg'});
   ref.componentInstance.series = series;
   ref.closed.subscribe(saved => {
     if (callback) {
       callback(saved);
     }
   });
  }

  async deleteFilter(filterId: number, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-smart-filter'))) {
      if (callback) {
        callback(false);
      }
      return;
    }

    this.filterService.deleteFilter(filterId).subscribe(_ => {
      this.toastr.success(translate('toasts.smart-filter-deleted'));

      if (callback) {
        callback(true);
      }
    });
  }

}
