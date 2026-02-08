import {inject, Injectable} from '@angular/core';
import {NgbModal, NgbModalRef} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {map, take} from 'rxjs/operators';
import {BulkAddToCollectionComponent} from '../cards/_modals/bulk-add-to-collection/bulk-add-to-collection.component';
import {ADD_FLOW, AddToListModalComponent} from '../reading-list/_modals/add-to-list-modal/add-to-list-modal.component';
import {
  EditReadingListModalComponent
} from '../reading-list/_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import {ConfirmService} from '../shared/confirm.service';
import {
  LibrarySettingsModalComponent
} from '../sidenav/_modals/library-settings-modal/library-settings-modal.component';
import {Chapter} from '../_models/chapter';
import {Device} from '../_models/device/device';
import {Library, LibraryType} from '../_models/library/library';
import {ReadingList} from '../_models/reading-list';
import {Series} from '../_models/series';
import {Volume} from '../_models/volume';
import {DeviceService} from './device.service';
import {LibraryService} from './library.service';
import {MemberService} from './member.service';
import {ReaderService} from './reader.service';
import {SeriesService} from './series.service';
import {translate} from "@jsverse/transloco";
import {UserCollection} from "../_models/collection-tag";
import {CollectionTagService} from "./collection-tag.service";
import {FilterService} from "./filter.service";
import {ReadingListService} from "./reading-list.service";
import {ChapterService} from "./chapter.service";
import {VolumeService} from "./volume.service";
import {DefaultModalOptions} from "../_models/default-modal-options";
import {MatchSeriesModalComponent} from "../_single-module/match-series-modal/match-series-modal.component";
import {
  BulkSetReadingProfileModalComponent
} from "../cards/_modals/bulk-set-reading-profile-modal/bulk-set-reading-profile-modal.component";
import {EditSeriesModalComponent} from "../cards/_modals/edit-series-modal/edit-series-modal.component";
import {
  EditVolumeModalCloseResult,
  EditVolumeModalComponent
} from "../_single-module/edit-volume-modal/edit-volume-modal.component";
import {DownloadService} from "../shared/_services/download.service";
import {ReadingProfileService} from "./reading-profile.service";
import {Action} from "../_models/actionables/action";
import {ActionItem} from "../_models/actionables/action-item";
import {EMPTY, filter, from, Observable, of, switchMap, tap} from "rxjs";
import {ActionEffect, ActionResult} from "../_models/actionables/action-result";
import {EditChapterModalComponent} from "../_single-module/edit-chapter-modal/edit-chapter-modal.component";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {Router} from "@angular/router";
import {EditCollectionTagsComponent} from "../cards/_modals/edit-collection-tags/edit-collection-tags.component";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {AnnotationService} from "./annotation.service";
import {ClientDevice} from "../_models/client-device";
import {Person} from "../_models/metadata/person";
import {EditPersonModalComponent} from "../person-detail/_modal/edit-person-modal/edit-person-modal.component";
import {MergePersonModalComponent} from "../person-detail/_modal/merge-person-modal/merge-person-modal.component";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {
  EditSmartFilterModalComponent
} from "../sidenav/_components/edit-smart-filter-modal/edit-smart-filter-modal.component";
import {SideNavStream} from "../_models/sidenav/sidenav-stream";
import {NavService} from "./nav.service";


export type LibraryActionCallback = (library: Partial<Library>) => void;
export type SeriesActionCallback = (series: Series) => void;
export type VolumeActionCallback = (volume: Volume) => void;
export type ChapterActionCallback = (chapter: Chapter) => void;
export type ReadingListActionCallback = (readingList: ReadingList) => void;
export type VoidActionCallback = () => void;
export type BooleanActionCallback = (result: boolean) => void;

export type ExtraActionCallback<T> = (action: Action, data: T) => void;

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
  private readonly collectionService = inject(CollectionTagService);
  private readonly downloadService = inject(DownloadService);
  private readonly readingProfilesService = inject(ReadingProfileService);
  private readonly router = inject(Router);
  private readonly annotationsService = inject(AnnotationService);
  private readonly sideNavService = inject(NavService);

  private readingListModalRef: NgbModalRef | null = null;
  private collectionModalRef: NgbModalRef | null = null;



  // -------------------------------------------
  //      MAIN HANDLERS
  // -------------------------------------------


  /**
   * Centralized handler for all series actions.
   * Returns Observable<ActionResult<Series>> so the caller can react to effects.
   */
  handleSeriesAction(action: ActionItem<Series>, series: Series): Observable<ActionResult<Series>> {
    switch (action.action) {
      case Action.MarkAsRead:
        return this.seriesService.markRead(series.id).pipe(
          tap(() => this.toastr.success(translate('toasts.entity-read', {name: series.name}))),
          map(() => this.fromAction(action, { ...series, pagesRead: series.pages }, 'update'))
        );

      case Action.MarkAsUnread:
        return this.seriesService.markUnread(series.id).pipe(
          tap(() => this.toastr.success(translate('toasts.entity-unread', {name: series.name}))),
          map(() => this.fromAction(action, { ...series, pagesRead: 0 }, 'update'))
        );

      case Action.Scan:
        return this.seriesService.scan(series.libraryId, series.id).pipe(
          tap(() => this.toastr.info(translate('toasts.scan-queued', {name: series.name}))),
          map(() => this.fromAction(action, series, 'none'))
        );

      case Action.RefreshMetadata:
        return from(this.confirmService.confirm(translate('toasts.confirm-regen-covers'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.seriesService.refreshMetadata(series, true, false)),
          tap(() => this.toastr.info(translate('toasts.refresh-covers-queued', {name: series.name}))),
          map(() => this.fromAction(action, series, 'none'))
        );

      case Action.GenerateColorScape:
        return this.seriesService.refreshMetadata(series, false, false).pipe(
          tap(() => this.toastr.info(translate('toasts.generate-colorscape-queued', {name: series.name}))),
          map(() => this.fromAction(action, series, 'none'))
        );

      case Action.AnalyzeFiles:
        return this.seriesService.analyzeFiles(series.libraryId, series.id).pipe(
          tap(() => this.toastr.info(translate('toasts.scan-queued', {name: series.name}))),
          map(() => this.fromAction(action, series, 'none'))
        );

      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-series'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.seriesService.delete(series.id)),
          tap(() => this.toastr.success(translate('toasts.series-deleted'))),
          map(() => this.fromAction(action, series, 'remove'))
        );

      case Action.Edit: {
        const modalRef = this.modalService.open(EditSeriesModalComponent, DefaultModalOptions);
        modalRef.componentInstance.series = series;
        return from(modalRef.closed).pipe(
          filter((closeResult: { success: boolean; series: Series; coverImageUpdate: boolean }) => closeResult.success),
          switchMap(() => this.seriesService.getSeries(series.id)),
          map(updated => this.fromAction(action, updated, 'update'))
        );
      }

      case Action.Match: {
        const ref = this.modalService.open(MatchSeriesModalComponent, DefaultModalOptions);
        ref.componentInstance.series = series;
        return from(ref.closed).pipe(
          filter((saved: boolean) => saved),
          map(() => this.fromAction(action, series, 'reload'))
        );
      }

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
        this.readingListModalRef.componentInstance.seriesId = series.id;
        this.readingListModalRef.componentInstance.title = series.name;
        this.readingListModalRef.componentInstance.type = ADD_FLOW.Series;

        const ref = this.readingListModalRef;
        return new Observable<ActionResult<Series>>(subscriber => {
          ref.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, series, 'none'));
            subscriber.complete();
          });
          ref.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.AddToCollection: {
        if (this.collectionModalRef != null) return EMPTY;
        this.collectionModalRef = this.modalService.open(BulkAddToCollectionComponent, { scrollable: true, size: 'md', windowClass: 'collection', fullscreen: 'md' });
        this.collectionModalRef.componentInstance.seriesIds = [series.id];
        this.collectionModalRef.componentInstance.title = translate('actionable.new-collection');

        const ref = this.collectionModalRef;
        return new Observable<ActionResult<Series>>(subscriber => {
          ref.closed.subscribe(() => {
            this.collectionModalRef = null;
            subscriber.next(this.fromAction(action, series, 'none'));
            subscriber.complete();
          });
          ref.dismissed.subscribe(() => {
            this.collectionModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.Download:
        this.downloadService.download('series', series);
        return of(this.fromAction(action, series, 'none'));

      case Action.AddToWantToReadList:
        return this.memberService.addSeriesToWantToRead([series.id]).pipe(
          tap(() => this.toastr.success(translate('toasts.series-added-want-to-read'))),
          map(() => this.fromAction(action, series, 'none'))
        );

      case Action.RemoveFromWantToReadList:
        return this.memberService.removeSeriesToWantToRead([series.id]).pipe(
          tap(() => this.toastr.success(translate('toasts.series-removed-want-to-read'))),
          map(() => this.fromAction(action, series, 'reload'))
        );

      case Action.RemoveFromOnDeck:
        return this.seriesService.removeFromOnDeck(series.id).pipe(
          map(() => this.fromAction(action, series, 'reload'))
        );

      case Action.SendTo: {
        const device = action._extra!.data as Device;
        return this.deviceService.sendSeriesToEmailDevice(series.id, device.id).pipe(
          tap(() => this.toastr.success(translate('toasts.file-send-to', {name: device.name}))),
          map(() => this.fromAction(action, series, 'none'))
        );
      }

      case Action.SetReadingProfile:
        this.setReadingProfileForMultiple([series]);
        return of(this.fromAction(action, series, 'none'));

      case Action.ClearReadingProfile:
        return this.readingProfilesService.clearSeriesProfiles(series.id).pipe(
          tap(() => this.toastr.success(translate('actionable.cleared-profile'))),
          map(() => this.fromAction(action, series, 'none'))
        );

      default:
        return of(this.fromAction(action, series, 'none'));
    }
  }

  /**
   * Centralized handler for all volume actions.
   * Returns Observable<ActionResult<Volume>> so the caller can react to effects.
   */
  handleVolumeAction(action: ActionItem<Volume>, volume: Volume, seriesId: number, libraryId: number, libraryType: LibraryType): Observable<ActionResult<Volume>> {
    switch (action.action) {
      case Action.MarkAsRead:
        return this.readerService.markVolumeRead(seriesId, volume.id).pipe(
          tap(() => this.toastr.success(translate('toasts.mark-read'))),
          map(() => {
            const updated = {
              ...volume,
              pagesRead: volume.pages,
              chapters: volume.chapters?.map(c => ({...c, pagesRead: c.pages}))
            };
            return this.fromAction(action, updated, 'update');
          })
        );

      case Action.MarkAsUnread:
        return this.readerService.markVolumeUnread(seriesId, volume.id).pipe(
          tap(() => this.toastr.success(translate('toasts.mark-unread'))),
          map(() => {
            const updated = {
              ...volume,
              pagesRead: 0,
              chapters: volume.chapters?.map(c => ({...c, pagesRead: 0}))
            };
            return this.fromAction(action, updated, 'update');
          })
        );

      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-volume'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.volumeService.deleteVolume(volume.id)),
          filter(success => success),
          tap(() => this.toastr.success(translate('toasts.volume-deleted'))),
          map(() => this.fromAction(action, volume, 'remove'))
        );

      case Action.Edit: {
        const ref = this.modalService.open(EditVolumeModalComponent, DefaultModalOptions);
        ref.componentInstance.volume = volume;
        ref.componentInstance.libraryType = libraryType;
        ref.componentInstance.seriesId = seriesId;
        ref.componentInstance.libraryId = libraryId;

        return from(ref.closed).pipe(
          filter((res: EditVolumeModalCloseResult) => res.success),
          map((res: EditVolumeModalCloseResult) =>
            this.fromAction(action, volume, res.isDeleted ? 'remove' : 'update')
          )
        );
      }

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        this.readingListModalRef = this.modalService.open(AddToListModalComponent, {scrollable: true, size: 'md', fullscreen: 'md'});
        this.readingListModalRef.componentInstance.seriesId = seriesId;
        this.readingListModalRef.componentInstance.volumeId = volume.id;
        this.readingListModalRef.componentInstance.type = ADD_FLOW.Volume;

        const ref = this.readingListModalRef;
        return new Observable<ActionResult<Volume>>(subscriber => {
          ref.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, volume, 'none'));
            subscriber.complete();
          });
          ref.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.IncognitoRead:
        if (volume.chapters != undefined && volume.chapters.length >= 1) {
          const sorted = [...volume.chapters].sort((a, b) => a.minNumber - b.minNumber);
          this.readerService.readChapter(libraryId, seriesId, sorted[0], true);
        }
        return of(this.fromAction(action, volume, 'none'));

      case Action.SendTo: {
        const device = action._extra!.data as Device;
        return this.deviceService.sendToEmailDevice(volume.chapters.map(c => c.id), device.id).pipe(
          tap(() => this.toastr.success(translate('toasts.file-send-to', {name: device.name}))),
          map(() => this.fromAction(action, volume, 'none'))
        );
      }

      case Action.Download:
        this.downloadService.download('volume', volume);
        return of(this.fromAction(action, volume, 'none'));

      default:
        return of(this.fromAction(action, volume, 'none'));
    }
  }

  /**
   * Centralized handler for all chapter actions.
   * Returns Observable<ActionResult<Chapter>> so the caller can react to effects.
   */
  handleChapterAction(action: ActionItem<Chapter>, chapter: Chapter, seriesId: number, libraryId: number, libraryType: LibraryType): Observable<ActionResult<Chapter>> {
    switch (action.action) {

      case Action.MarkAsRead:
        return this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, chapter.pages).pipe(
          tap(() => this.toastr.success(translate('toasts.mark-read'))),
          map(() => {
            const updated = {
              ...chapter,
              pagesRead: chapter.pages,
            };
            return this.fromAction(action, updated, 'update');
          })
        );

      case Action.MarkAsUnread:
        return this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, 9).pipe(
          tap(() => this.toastr.success(translate('toasts.mark-unread'))),
          map(() => {
            const updated = {
              ...chapter,
              pagesRead: 9,
            };
            return this.fromAction(action, updated, 'update');
          })
        );

      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-chapter'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.chapterService.deleteChapter(chapter.id)),
          filter(success => success),
          tap(() => this.toastr.success(translate('toasts.chapter-deleted'))),
          map(() => this.fromAction(action, chapter, 'remove'))
        );

      case Action.Download:
        this.downloadService.download('chapter', chapter);
        return of(this.fromAction(action, chapter, 'none'));

      case Action.Edit:
        const ref = this.modalService.open(EditChapterModalComponent, DefaultModalOptions);
        ref.componentInstance.chapter = chapter;
        ref.componentInstance.libraryType = libraryType;
        ref.componentInstance.seriesId = seriesId;
        ref.componentInstance.libraryId = libraryId;

        return from(ref.closed).pipe(
          filter((res: EditVolumeModalCloseResult) => res.success),
          map((res: EditVolumeModalCloseResult) =>
            this.fromAction(action, chapter, res.isDeleted ? 'remove' : 'update')
          )
        );

      case Action.AddToReadingList:
        if (this.readingListModalRef != null) return EMPTY;
        this.readingListModalRef = this.modalService.open(AddToListModalComponent, {scrollable: true, size: 'md', fullscreen: 'md'});
        this.readingListModalRef.componentInstance.seriesId = seriesId;
        this.readingListModalRef.componentInstance.volumeId = chapter.volumeId;
        this.readingListModalRef.componentInstance.chapterId = chapter.id;
        this.readingListModalRef.componentInstance.type = ADD_FLOW.Chapter;

        const chapterRLRef = this.readingListModalRef;
        return new Observable<ActionResult<Chapter>>(subscriber => {
          chapterRLRef.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, chapter, 'none'));
            subscriber.complete();
          });
          chapterRLRef.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });

      case Action.IncognitoRead:
        this.readerService.readChapter(libraryId, seriesId, chapter, true);
        return of(this.fromAction(action, chapter, 'none'));

      case Action.SendTo:
        const device = action._extra!.data as Device;
        return this.deviceService.sendToEmailDevice([chapter.id], device.id).pipe(
          tap(() => this.toastr.success(translate('toasts.file-send-to', {name: device.name}))),
          map(() => this.fromAction(action, chapter, 'none'))
        );

      default:
        return of(this.fromAction(action, chapter, 'none'));
    }
  }

  /**
   * Centralized handler for all bookmark actions.
   * Returns Observable<ActionResult<PageBookmark>> so the caller can react to effects.
   */
  handleBookmarkAction(action: ActionItem<PageBookmark>, bookmark: PageBookmark, seriesId: number, libraryId: number, seriesName: string) {
    switch (action.action) {

      case Action.Delete:
        return from(this.confirmService.confirm(translate('bookmarks.confirm-single-delete', {seriesName}))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.readerService.clearBookmarks(seriesId)),
          tap(() => this.toastr.success(translate('bookmarks.delete-single-success'))),
          map(() => this.fromAction(action, bookmark, 'remove'))
        );

      case Action.DownloadBookmark:
        this.downloadService.download('bookmark', [bookmark]);
        return of(this.fromAction(action, bookmark, 'none'));

      case Action.ViewSeries:
        this.router.navigate(['library', libraryId, 'series', seriesId]);
        return of(this.fromAction(action, bookmark, 'none'));

      default:
        return of(this.fromAction(action, bookmark, 'none'));
    }
  }

  /**
   * Centralized handler for all reading list actions.
   * Returns Observable<ActionResult<ReadingList>> so the caller can react to effects.
   */
  handleReadingListAction(action: ActionItem<ReadingList>, readingList: ReadingList) {
    switch (action.action) {
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-reading-list'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.readingListService.delete(readingList.id)),
          tap(() => this.toastr.success(translate('toasts.reading-list-deleted'))),
          map(() => this.fromAction(action, readingList, 'remove'))
        );

      case Action.Edit:
        const ref = this.modalService.open(EditReadingListModalComponent, DefaultModalOptions);
        ref.componentInstance.readingList = readingList;
        return from(ref.closed).pipe(
          map((res: ReadingList) =>
            this.fromAction(action, res, 'reload')
          )
        );
      case Action.Promote:
        return this.readingListService.promoteMultipleReadingLists([readingList.id], true).pipe(
          tap(() => this.toastr.success(translate('toasts.reading-list-promoted'))),
          map(() => this.fromAction(action, {...readingList, promoted: true}, 'update'))
        );

      case Action.UnPromote:
        return this.readingListService.promoteMultipleReadingLists([readingList.id], false).pipe(
          tap(() => this.toastr.success(translate('toasts.reading-list-unpromoted'))),
          map(() => this.fromAction(action, {...readingList, promoted: false}, 'update'))
        );
      default:
        return of(this.fromAction(action, readingList, 'none'));
    }
  }

  /**
   * Centralized handler for all collection actions.
   * Returns Observable<ActionResult<UserCollection>> so the caller can react to effects.
   */
  handleCollectionAction(action: ActionItem<UserCollection>, collection: UserCollection) {
    switch (action.action) {
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-collection'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.collectionService.deleteTag(collection.id)),
          tap(() => this.toastr.success(translate('toasts.collection-tag-deleted'))),
          map(() => this.fromAction(action, collection, 'remove'))
        );

      case Action.Edit:
        const ref = this.modalService.open(EditCollectionTagsComponent, DefaultModalOptions);
        ref.componentInstance.tag = collection;
        return from(ref.closed).pipe(
          map((res: {success: boolean, coverImageUpdated: boolean}) =>
            this.fromAction(action, collection, 'update')
          )
        );

      case Action.Promote:
        return this.collectionService.promoteMultipleCollections([collection.id], true).pipe(
          tap(() => this.toastr.success(translate('toasts.collections-promoted'))),
          map(() => this.fromAction(action, {...collection, promoted: true}, 'update'))
        );

      case Action.UnPromote:
        return this.collectionService.promoteMultipleCollections([collection.id], false).pipe(
          tap(() => this.toastr.success(translate('toasts.collections-unpromoted'))),
          map(() => this.fromAction(action, {...collection, promoted: false}, 'update'))
        );

      default:
        return of(this.fromAction(action, collection, 'none'));
    }
  }

  /**
   * Centralized handler for all annotation actions.
   * Returns Observable<ActionResult<Annotation>> so the caller can react to effects.
   */
  handleAnnotationAction(action: ActionItem<Annotation>, annotation: Annotation) {
    switch (action.action) {
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-annotations'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.annotationsService.bulkDelete([annotation.id])),
          tap(() => this.toastr.success(translate('toasts.annotations-deleted'))),
          map(() => this.fromAction(action, annotation, 'remove'))
        );

      case Action.Export:
        return this.annotationsService.exportAnnotations([annotation.id]).pipe(
          map(() => this.fromAction(action, annotation, 'none'))
        );

      case Action.Like:
        return this.annotationsService.likeAnnotations([annotation.id]).pipe(
          map(() => this.fromAction(action, annotation, 'update'))
        );

      case Action.UnLike:
        return this.annotationsService.unLikeAnnotations([annotation.id]).pipe(
          map(() => this.fromAction(action, annotation, 'update'))
        );

      default:
        return of(this.fromAction(action, annotation, 'none'));
    }
  }

  /**
   * Centralized handler for all client device actions.
   * Returns Observable<ActionResult<ClientDevice>> so the caller can react to effects.
   */
  handleClientDeviceAction(action: ActionItem<ClientDevice>, clientDevice: ClientDevice) {
    switch (action.action) {
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-annotations'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.deviceService.deleteClientDevice(clientDevice.id)),
          map((success) => this.fromAction(action, clientDevice,  success ? 'remove' : 'none'))
        );

      case Action.Edit:
          // Special case: This actually just triggers an edit toggle. Since there is no edit modal, we send update to handle
          return of(this.fromAction(action, clientDevice, 'update'));

      default:
        return of(this.fromAction(action, clientDevice, 'none'));
    }
  }

  /**
   * Centralized handler for all person actions.
   * Returns Observable<ActionResult<Person>> so the caller can react to effects.
   */
  handlePersonAction(action: ActionItem<Person>, person: Person) {
    switch (action.action) {
      case Action.Edit:
        const ref = this.modalService.open(EditPersonModalComponent, DefaultModalOptions);
        ref.componentInstance.person = person;

        return from(ref.closed).pipe(
          filter((res: {success: false, coverImageUpdate: false, person: Person}) => res.success),
          map((res: {success: false, coverImageUpdate: false}) =>
            this.fromAction(action, person, res.success ? 'update' : 'none')
          )
        );

      case Action.Merge:
        const ref2 = this.modalService.open(MergePersonModalComponent, DefaultModalOptions);
        ref2.componentInstance.person = person;

        return from(ref2.closed).pipe(
          filter((res: {success: false, coverImageUpdate: false, person: Person}) => res.success),
          map((res: {success: false, coverImageUpdate: false}) =>
            this.fromAction(action, person, res.success ? 'reload' : 'none')
          )
        );
      default:
        return of(this.fromAction(action, person, 'none'));
    }
  }

  /**
   * Centralized handler for all smart filter actions.
   * Returns Observable<ActionResult<SmartFilter>> so the caller can react to effects.
   */
  handleSmartFilterAction(action: ActionItem<SmartFilter>, smartFilter: SmartFilter, allFilters: SmartFilter[]) {
    switch (action.action) {
      case Action.Edit:
        const ref = this.modalService.open(EditSmartFilterModalComponent, DefaultModalOptions);
        ref.componentInstance.smartFilter = smartFilter;
        ref.componentInstance.allFilters = allFilters;
        return from(ref.closed).pipe(
          filter(success => success),
          map((res: boolean) =>
            this.fromAction(action, smartFilter, 'update')
          )
        );
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-smart-filter'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.collectionService.deleteTag(smartFilter.id)),
          tap(() => this.toastr.success(translate('toasts.smart-filter-deleted'))),
          map(() => this.fromAction(action, smartFilter, 'remove'))
        );
      default:
        return of(this.fromAction(action, smartFilter, 'none'));
    }
  }

  /**
   * Centralized handler for all side nav stream actions.
   * Returns Observable<ActionResult<SideNavStream>> so the caller can react to effects.
   */
  handleSideNavStreamAction(action: ActionItem<SideNavStream>, sideNavStream: SideNavStream) {
    switch (action.action) {
      case Action.MarkAsVisible:
        return this.sideNavService.bulkToggleSideNavStreamVisibility([sideNavStream.id], true).pipe(
          map(() => this.fromAction(action, {...sideNavStream, visible: true}, 'update'))
        );

      case Action.MarkAsInvisible:
        return this.sideNavService.bulkToggleSideNavStreamVisibility([sideNavStream.id], false).pipe(
          map(() => this.fromAction(action, {...sideNavStream, visible: false}, 'update'))
        );

      default:
        return of(this.fromAction(action, sideNavStream, 'none'));
    }
  }

  /**
   * Centralized handler for all side nav home stream actions.
   * Returns Observable<ActionResult<{}>> so the caller can react to effects.
   */
  handleSideNavHomeStream(action: ActionItem<{}>, entity: {}) {
    switch (action.action) {
      case Action.Edit:
        return of(this.fromAction(action, entity, 'none'));

      default:
        return of(this.fromAction(action, entity, 'none'));
    }
  }

  // -------------------------------------------
  //      INDIVIDUAL HANDLERS
  // -------------------------------------------


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

    this.libraryService.scan(library.id, force).subscribe((res: any) => {
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

    this.libraryService.delete(library?.id).subscribe(() => {
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
    this.seriesService.markRead(series.id).subscribe(() => {
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
    this.seriesService.markUnread(series.id).subscribe(() => {
      series.pagesRead = 0;
      this.toastr.success(translate('toasts.entity-unread', {name: series.name}));
      callback?.(series);
    });
  }

  /**
   * Start a file scan for a Series
   * @param series Series, must have libraryId and name populated
   * @param callback Optional callback to perform actions after API completes
   */
  async scanSeries(series: Series, callback?: SeriesActionCallback) {
    this.seriesService.scan(series.libraryId, series.id).subscribe(() => {
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
    this.seriesService.analyzeFiles(series.libraryId, series.id).subscribe(() => {
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
        callback?.(series)
        return;
      }
    }

    const message = forceUpdate ? 'toasts.refresh-covers-queued' : 'toasts.generate-colorscape-queued';

    this.seriesService.refreshMetadata(series, forceUpdate, forceColorscape).subscribe(() => {
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
    this.readerService.markVolumeRead(seriesId, volume.id).subscribe(() => {
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
    this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, chapter.pages).subscribe(() => {
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
    this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, 0).subscribe(() => {
      chapter.pagesRead = 0;
      this.toastr.success(translate('toasts.mark-unread'));
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
  markChapterAsUnread2(libraryId: number, seriesId: number, chapter: Chapter, callback?: ExtraActionCallback<Chapter>) {
    this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, 0).subscribe(() => {
      chapter.pagesRead = 0;
      this.toastr.success(translate('toasts.mark-unread'));
      callback?.(Action.MarkAsUnread, chapter);
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
    this.readerService.markMultipleRead(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = volume.pages;
        volume.chapters?.forEach(c => c.pagesRead = c.pages);
      });
      chapters?.forEach(c => c.pagesRead = c.pages);
      this.toastr.success(translate('toasts.mark-read'));

      callback?.()
    });
  }

  /**
   * Mark all chapters and the volumes as Read. All volumes and chapters must belong to a series
   * @param seriesId Series Id
   * @param volumes Volumes, should have id, chapters and pagesRead populated
   * @param chapters Optional Chapters, should have id
   * @param callback Optional callback to perform actions after API completes
   */
  markMultipleAsRead2(seriesId: number, volumes: Array<Volume>, chapters?: Array<Chapter>, callback?: ExtraActionCallback<void>) {
    this.readerService.markMultipleRead(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = volume.pages;
        volume.chapters?.forEach(c => c.pagesRead = c.pages);
      });
      chapters?.forEach(c => c.pagesRead = c.pages);
      this.toastr.success(translate('toasts.mark-read'));

      callback?.(Action.MarkAsRead);
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
    this.readerService.markMultipleUnread(seriesId, volumes.map(v => v.id), chapters?.map(c => c.id)).subscribe(() => {
      volumes.forEach(volume => {
        volume.pagesRead = 0;
        volume.chapters?.forEach(c => c.pagesRead = 0);
      });
      chapters?.forEach(c => c.pagesRead = 0);
      this.toastr.success(translate('toasts.mark-unread'));

      callback?.()
    });
  }

  /**
   * Mark all series as Read.
   * @param series Series, should have id, pagesRead populated
   * @param callback Optional callback to perform actions after API completes
   */
   markMultipleSeriesAsRead(series: Array<Series>, callback?: VoidActionCallback) {
    this.readerService.markMultipleSeriesRead(series.map(v => v.id)).subscribe(() => {
      series.forEach(s => {
        s.pagesRead = s.pages;
      });
      this.toastr.success(translate('toasts.mark-read'));

      callback?.()
    });
  }

  /**
   * Mark all series as Unread.
   * @param series Series, should have id, pagesRead populated
   * @param callback Optional callback to perform actions after API completes
   */
   markMultipleSeriesAsUnread(series: Array<Series>, callback?: VoidActionCallback) {
    this.readerService.markMultipleSeriesUnread(series.map(v => v.id)).subscribe(() => {
      series.forEach(s => {
        s.pagesRead = s.pages;
      });
      this.toastr.success(translate('toasts.mark-unread'));

      callback?.()
    });
  }

  /**
   * Mark all collections as promoted/unpromoted.
   * @param collections UserCollection, should have id, pagesRead populated
   * @param promoted boolean, promoted state
   * @param callback Optional callback to perform actions after API completes
   */
  promoteMultipleCollections(collections: Array<UserCollection>, promoted: boolean, callback?: BooleanActionCallback) {
    this.collectionTagService.promoteMultipleCollections(collections.map(v => v.id), promoted).subscribe(() => {
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

    this.collectionTagService.deleteMultipleCollections(collections.map(v => v.id)).subscribe(() => {
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
    this.readingListService.promoteMultipleReadingLists(readingLists.map(v => v.id), promoted).subscribe(() => {
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

  async deleteMultipleVolumes(volumes: Array<Volume>, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-multiple-volumes', {count: volumes.length}))) return;

    this.volumeService.deleteMultipleVolumes(volumes.map(v => v.id)).subscribe((success) => {
      if (callback) {
        callback(success);
      }
    })
  }

  async deleteMultipleChapters(seriesId: number, chapterIds: Array<Chapter>, callback?: BooleanActionCallback) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-multiple-chapters', {count: chapterIds.length}))) return;

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

    this.readingListService.deleteMultipleReadingLists(readingLists.map(v => v.id)).subscribe(() => {
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


      this.readingListModalRef.closed.subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(true);
        }
      });
      this.readingListModalRef.dismissed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(false)
      });
  }

  addMultipleSeriesToWantToReadList(seriesIds: Array<number>, callback?: VoidActionCallback) {
    this.memberService.addSeriesToWantToRead(seriesIds).subscribe(() => {
      this.toastr.success(translate('toasts.series-added-want-to-read'));
      callback?.()
    });
  }

  removeMultipleSeriesFromWantToReadList(seriesIds: Array<number>, callback?: VoidActionCallback) {
    this.memberService.removeSeriesToWantToRead(seriesIds).subscribe(() => {
      this.toastr.success(translate('toasts.series-removed-want-to-read'));
      callback?.()
    });
  }

  addMultipleSeriesToReadingList(series: Array<Series>, callback?: BooleanActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
      this.readingListModalRef.componentInstance.seriesIds = series.map(v => v.id);
      this.readingListModalRef.componentInstance.title = translate('actionable.multiple-selections');
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Multiple_Series;


      this.readingListModalRef.closed.subscribe(() => {
        this.readingListModalRef = null;
        if (callback) {
          callback(true);
        }
      });
      this.readingListModalRef.dismissed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(false)
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

      this.collectionModalRef.closed.subscribe(() => {
        this.collectionModalRef = null;
        if (callback) {
          callback(true);
        }
      });
      this.collectionModalRef.dismissed.subscribe(() => {
        this.collectionModalRef = null;
        callback?.(false)
      });
  }

  addSeriesToReadingList(series: Series, callback?: SeriesActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
      this.readingListModalRef.componentInstance.seriesId = series.id;
      this.readingListModalRef.componentInstance.title = series.name;
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Series;


      this.readingListModalRef.closed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(series)
      });
      this.readingListModalRef.dismissed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(series)
      });
  }

addVolumeToReadingList(volume: Volume, seriesId: number, callback?: VolumeActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
      this.readingListModalRef.componentInstance.seriesId = seriesId;
      this.readingListModalRef.componentInstance.volumeId = volume.id;
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Volume;


      this.readingListModalRef.closed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(volume)
      });
      this.readingListModalRef.dismissed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(volume)
      });
  }

  addChapterToReadingList(chapter: Chapter, seriesId: number, callback?: ChapterActionCallback) {
    if (this.readingListModalRef != null) { return; }
      this.readingListModalRef = this.modalService.open(AddToListModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
      this.readingListModalRef.componentInstance.seriesId = seriesId;
      this.readingListModalRef.componentInstance.chapterId = chapter.id;
      this.readingListModalRef.componentInstance.type = ADD_FLOW.Chapter;


      this.readingListModalRef.closed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(chapter)
      });
      this.readingListModalRef.dismissed.subscribe(() => {
        this.readingListModalRef = null;
        callback?.(chapter)
      });
  }

  editReadingList(readingList: ReadingList, callback?: ReadingListActionCallback) {
    const readingListModalRef = this.modalService.open(EditReadingListModalComponent, DefaultModalOptions);
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
    this.seriesService.deleteMultipleSeries(seriesIds.map(s => s.id)).subscribe(res => {
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
    this.deviceService.sendToEmailDevice(chapterIds, device.id).subscribe(() => {
      this.toastr.success(translate('toasts.file-send-to', {name: device.name}));
      callback?.()
    });
  }

  sendSeriesToDevice(seriesId: number, device: Device, callback?: VoidActionCallback) {
    this.deviceService.sendSeriesToEmailDevice(seriesId, device.id).subscribe(() => {
      this.toastr.success(translate('toasts.file-send-to', {name: device.name}));
      callback?.()
    });
  }

  matchSeries(series: Series, callback?: BooleanActionCallback) {
   const ref = this.modalService.open(MatchSeriesModalComponent, DefaultModalOptions);
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

  /**
   * Sets the reading profile for multiple series
   * @param series
   * @param callback
   */
  setReadingProfileForMultiple(series: Array<Series>, callback?: BooleanActionCallback) {
    if (this.readingListModalRef != null) { return; }

    this.readingListModalRef = this.modalService.open(BulkSetReadingProfileModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
    this.readingListModalRef.componentInstance.seriesIds = series.map(s => s.id)

    this.readingListModalRef.closed.subscribe(() => {
      this.readingListModalRef = null;
      if (callback) {
        callback(true);
      }
    });
    this.readingListModalRef.dismissed.subscribe(() => {
      this.readingListModalRef = null;
      if (callback) {
        callback(false);
      }
    });
  }

  /**
   * Sets the reading profile for multiple series
   * @param library
   * @param callback
   */
  setReadingProfileForLibrary(library: Library, callback?: BooleanActionCallback) {
    if (this.readingListModalRef != null) { return; }

    this.readingListModalRef = this.modalService.open(BulkSetReadingProfileModalComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
    this.readingListModalRef.componentInstance.libraryId = library.id;

    this.readingListModalRef.closed.subscribe(() => {
      this.readingListModalRef = null;
      if (callback) {
        callback(true);
      }
    });
    this.readingListModalRef.dismissed.subscribe(() => {
      this.readingListModalRef = null;
      if (callback) {
        callback(false);
      }
    });
  }


  private fromAction<T>(action: ActionItem<T>, data: T, effect: ActionEffect): ActionResult<T> {
    return { action: action.action, entity: data, effect: effect };
  }
}
