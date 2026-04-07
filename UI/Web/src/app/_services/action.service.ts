import {inject, Injectable} from '@angular/core';
import {NgbModalRef} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {catchError, finalize, map, take} from 'rxjs/operators';
import {ListSelectModalComponent} from '../shared/_components/list-select-modal/list-select-modal.component';
import {ScrobbleProvider} from './scrobbling.service';
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
import {ReadingList} from '../_models/reading-list/reading-list';
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
import {ReadingListService} from "./reading-list.service";
import {ChapterService} from "./chapter.service";
import {VolumeService} from "./volume.service";
import {MatchSeriesModalComponent} from "../_single-module/match-series-modal/match-series-modal.component";
import {
  BulkSetReadingProfileModalComponent
} from "../cards/_modals/bulk-set-reading-profile-modal/bulk-set-reading-profile-modal.component";
import {EditSeriesModalComponent} from "../cards/_modals/edit-series-modal/edit-series-modal.component";
import {EditVolumeModalComponent} from "../_single-module/edit-volume-modal/edit-volume-modal.component";
import {DownloadService} from '../shared/_services/download.service';
import {DownloadEntityType} from '../shared/_models/download-queue-item';
import {ReadingProfileService} from "./reading-profile.service";
import {Action} from "../_models/actionables/action";
import {ActionItem} from "../_models/actionables/action-item";
import {EMPTY, filter, from, Observable, of, switchMap, tap} from "rxjs";
import {ActionEffect, ActionResult} from "../_models/actionables/action-result";
import {EditChapterModalComponent} from "../_single-module/edit-chapter-modal/edit-chapter-modal.component";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {Router} from "@angular/router";
import {
  EditCollectionTagsModalComponent
} from "../cards/_modals/edit-collection-tags/edit-collection-tags-modal.component";
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
import {ModalResult} from "../_models/modal/modal-result";
import {addToModal, editModal} from "../_models/modal/modal-options";
import {ModalService, TypedModalRef} from "./modal.service";
import {FilterService} from "src/app/_services/filter.service";


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
  private readonly modalService = inject(ModalService);
  private readonly confirmService = inject(ConfirmService);
  private readonly memberService = inject(MemberService);
  private readonly deviceService = inject(DeviceService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly readingListService = inject(ReadingListService);
  private readonly collectionService = inject(CollectionTagService);
  private readonly downloadService = inject(DownloadService);
  private readonly readingProfilesService = inject(ReadingProfileService);
  private readonly router = inject(Router);
  private readonly annotationsService = inject(AnnotationService);
  private readonly sideNavService = inject(NavService);
  private readonly filterService = inject(FilterService);

  private readingListModalRef: TypedModalRef<BulkSetReadingProfileModalComponent> |  TypedModalRef<ListSelectModalComponent<ReadingList>> | null = null;
  private collectionModalRef: TypedModalRef<ListSelectModalComponent<UserCollection>> | null = null;



  // -------------------------------------------
  //      MAIN HANDLERS
  // -------------------------------------------

  handleLibraryAction(action: ActionItem<Library>, library: Library) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return of(this.fromAction(action, library, 'none'));
    }

    switch (action.action) {
      case Action.Scan:
        return this.libraryService.scan(library.id, false).pipe(
          tap(() => this.toastr.info(translate('toasts.scan-queued', {name: library.name}))),
          map(() => this.fromAction(action, library, 'none'))
        );

      case Action.RefreshMetadata:
        return from(this.confirmService.confirm(translate('toasts.confirm-regen-covers'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.libraryService.refreshMetadata(library.id, true, false)),
          tap(() => this.toastr.info(translate('toasts.refresh-covers-queued', {name: library.name}))),
          map(() => this.fromAction(action, library, 'none'))
        );

      case Action.GenerateColorScape:
        return this.libraryService.refreshMetadata(library.id, false, false).pipe(
          tap(() => this.toastr.info(translate('toasts.generate-colorscape-queued', {name: library.name}))),
          map(() => this.fromAction(action, library, 'none'))
        );

      case Action.Delete:
        return from(this.confirmService.alert(translate('toasts.confirm-library-delete'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.libraryService.delete(library.id)),
          tap(() => this.toastr.info(translate('toasts.library-deleted', {name: library.name}))),
          map(() => this.fromAction(action, library, 'remove'))
        );

      case Action.Edit: {
        const modalRef = this.modalService.open(LibrarySettingsModalComponent, editModal());
        modalRef.componentInstance.library = library;
        return this.handleEditModal(modalRef, action, library);
      }

      case Action.SetReadingProfile:
        this.setReadingProfileForLibrary(library);
        return of(this.fromAction(action, library, 'none'));

      case Action.ClearReadingProfile:
        return this.readingProfilesService.clearLibraryProfiles(library.id).pipe(
          tap(() => this.toastr.success(translate('actionable.cleared-profile'))),
          map(() => this.fromAction(action, library, 'none'))
        );

      default:
        return of(this.fromAction(action, library, 'none'));
    }
  }

  /**
   * Centralized handler for all series actions.
   * Returns Observable<ActionResult<Series>> so the caller can react to effects.
   */
  handleSeriesAction(action: ActionItem<Series>, series: Series) {
    switch (action.action) {
      case Action.MarkAsRead:
      case Action.MarkAsReadWithSession:
        const generateReadingSession = action.action === Action.MarkAsReadWithSession;
        return this.seriesService.markRead(series.id, generateReadingSession).pipe(
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
        const modalRef = this.modalService.open(EditSeriesModalComponent, editModal());
        modalRef.componentInstance.series = series;
        return this.handleEditModal(modalRef, action, series);
      }

      case Action.Match: {
        const ref = this.modalService.open(MatchSeriesModalComponent, editModal());
        ref.setInput('series', series);
        return from(ref.closed).pipe(
          filter((saved: boolean) => saved),
          map(() => this.fromAction(action, series, 'reload'))
        );
      }

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        const rlRef = this.modalService.open(ListSelectModalComponent, addToModal()) as TypedModalRef<ListSelectModalComponent<ReadingList>>;
        this.readingListModalRef = rlRef;

        rlRef.setInput('title', series.name);
        rlRef.setInput('showCreate', true);
        rlRef.setInput('createLabel', translate('add-to-list-modal.reading-list-label'));
        rlRef.setInput('inputItems', []);
        rlRef.setInput('loading', true);
        rlRef.setInput('createInitialValue', series.name);

        this.readingListService.getReadingLists(false, true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => rlRef.setInput('loading', false))
        ).subscribe(result => {
          rlRef.setInput('inputItems', result.result.map(l => ({ label: l.title, value: l })));
        });

        rlRef.setInput('interceptCreate', (name: string) =>
          this.readingListService.createList(name).pipe(
            switchMap(list => this.readingListService.updateBySeries(list.id, series.id)),
            tap(() => this.toastr.success(translate('toasts.series-added-to-reading-list')))
          )
        );

        rlRef.setInput('interceptConfirm', (item: ReadingList | ReadingList[]) => {
          const list = item as ReadingList;
          this.readingListService.updateBySeries(list.id, series.id).subscribe(() => {
            this.toastr.success(translate('toasts.series-added-to-reading-list'));
            rlRef.close();
          });
        });

        return new Observable<ActionResult<Series>>(subscriber => {
          rlRef.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, series, 'none'));
            subscriber.complete();
          });
          rlRef.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.AddToCollection: {
        if (this.collectionModalRef != null) return EMPTY;
        const colRef = this.modalService.open(ListSelectModalComponent, addToModal()) as TypedModalRef<ListSelectModalComponent<UserCollection>>;
        this.collectionModalRef = colRef;

        const singleSeriesIds = [series.id];
        colRef.setInput('title', translate('bulk-add-to-collection.title'));
        colRef.setInput('showCreate', true);
        colRef.setInput('createLabel', translate('bulk-add-to-collection.collection-label'));
        colRef.setInput('createInitialValue', translate('actionable.new-collection'));
        colRef.setInput('inputItems', []);
        colRef.setInput('loading', true);

        this.collectionService.allCollections(true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => colRef.setInput('loading', false))
        ).subscribe(tags => {
          const collections = tags.filter(t => t.source === ScrobbleProvider.Kavita);
          colRef.setInput('inputItems', collections.map(c => ({ label: c.title, value: c })));
        });

        colRef.setInput('interceptCreate', (name: string) =>
          this.collectionService.addByMultiple(0, singleSeriesIds, name).pipe(
            tap(() => this.toastr.success(translate('toasts.series-added-to-collection', { collectionName: name })))
          )
        );

        colRef.setInput('interceptConfirm', (item: UserCollection | UserCollection[]) => {
          const tag = item as UserCollection;
          this.collectionService.addByMultiple(tag.id, singleSeriesIds, '').subscribe(() => {
            this.toastr.success(translate('toasts.series-added-to-collection', { collectionName: tag.title }));
            colRef.close();
          });
        });

        return new Observable<ActionResult<Series>>(subscriber => {
          colRef.closed.subscribe(() => {
            this.collectionModalRef = null;
            subscriber.next(this.fromAction(action, series, 'none'));
            subscriber.complete();
          });
          colRef.dismissed.subscribe(() => {
            this.collectionModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.Download:
        this.downloadService.download(DownloadEntityType.Series, series, series.libraryId, series.id);
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
  handleVolumeAction(action: ActionItem<Volume>, volume: Volume, seriesId: number, libraryId: number, libraryType: LibraryType) {
    switch (action.action) {
      case Action.MarkAsRead:
      case Action.MarkAsReadWithSession:
        const generateReadingSession = action.action === Action.MarkAsReadWithSession;
        return this.readerService.markVolumeRead(seriesId, volume.id, generateReadingSession).pipe(
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
        const ref = this.modalService.open(EditVolumeModalComponent, editModal());
        ref.componentInstance.volume = volume;
        ref.componentInstance.libraryType = libraryType;
        ref.componentInstance.seriesId = seriesId;
        ref.componentInstance.libraryId = libraryId;
        return this.handleEditModal(ref, action, volume);
      }

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        const rlRef = this.modalService.open(ListSelectModalComponent, addToModal()) as TypedModalRef<ListSelectModalComponent<ReadingList>>;
        this.readingListModalRef = rlRef;

        rlRef.setInput('title', translate('add-to-list-modal.title'));
        rlRef.setInput('showCreate', true);
        rlRef.setInput('createLabel', translate('add-to-list-modal.reading-list-label'));
        rlRef.setInput('inputItems', []);
        rlRef.setInput('loading', true);

        this.readingListService.getReadingLists(false, true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => rlRef.setInput('loading', false))
        ).subscribe(result => {
          rlRef.setInput('inputItems', result.result.map(l => ({ label: l.title, value: l })));
        });

        rlRef.setInput('interceptCreate', (name: string) =>
          this.readingListService.createList(name).pipe(
            switchMap(list => this.readingListService.updateByVolume(list.id, seriesId, volume.id)),
            tap(() => this.toastr.success(translate('toasts.volumes-added-to-reading-list')))
          )
        );

        rlRef.setInput('interceptConfirm', (item: ReadingList | ReadingList[]) => {
          const list = item as ReadingList;
          this.readingListService.updateByVolume(list.id, seriesId, volume.id).subscribe(() => {
            this.toastr.success(translate('toasts.volumes-added-to-reading-list'));
            rlRef.close();
          });
        });

        return new Observable<ActionResult<Volume>>(subscriber => {
          rlRef.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, volume, 'none'));
            subscriber.complete();
          });
          rlRef.dismissed.subscribe(() => {
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
        this.downloadService.download(DownloadEntityType.Volume, volume, libraryId, seriesId);
        return of(this.fromAction(action, volume, 'none'));

      default:
        return of(this.fromAction(action, volume, 'none'));
    }
  }

  /**
   * Centralized handler for all chapter actions.
   * Returns Observable<ActionResult<Chapter>> so the caller can react to effects.
   */
  handleChapterAction(action: ActionItem<Chapter>, chapter: Chapter, seriesId: number, libraryId: number, libraryType: LibraryType) {
    switch (action.action) {

      case Action.MarkAsRead:
      case Action.MarkAsReadWithSession:
        const generateReadingSession = action.action === Action.MarkAsReadWithSession;
        return this.readerService.markChapterRead(seriesId, chapter.id, generateReadingSession).pipe(
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
        return this.readerService.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, 0).pipe(
          tap(() => this.toastr.success(translate('toasts.mark-unread'))),
          map(() => {
            const updated = {
              ...chapter,
              pagesRead: 0,
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
        this.downloadService.download(DownloadEntityType.Chapter, chapter, libraryId, seriesId);
        return of(this.fromAction(action, chapter, 'none'));

      case Action.Edit:
        const ref = this.modalService.open(EditChapterModalComponent, editModal());
        ref.componentInstance.chapter = chapter;
        ref.componentInstance.libraryType = libraryType;
        ref.componentInstance.seriesId = seriesId;
        ref.componentInstance.libraryId = libraryId;

        return this.handleEditModal(ref, action, chapter);

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        const rlRef = this.modalService.open(ListSelectModalComponent, addToModal()) as TypedModalRef<ListSelectModalComponent<ReadingList>>;
        this.readingListModalRef = rlRef;

        rlRef.setInput('title', translate('add-to-list-modal.title'));
        rlRef.setInput('showCreate', true);
        rlRef.setInput('createLabel', translate('add-to-list-modal.reading-list-label'));
        rlRef.setInput('inputItems', []);
        rlRef.setInput('loading', true);

        this.readingListService.getReadingLists(false, true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => rlRef.setInput('loading', false))
        ).subscribe(result => {
          rlRef.setInput('inputItems', result.result.map(l => ({ label: l.title, value: l })));
        });

        rlRef.setInput('interceptCreate', (name: string) =>
          this.readingListService.createList(name).pipe(
            switchMap(list => this.readingListService.updateByChapter(list.id, seriesId, chapter.id)),
            tap(() => this.toastr.success(translate('toasts.chapter-added-to-reading-list')))
          )
        );

        rlRef.setInput('interceptConfirm', (item: ReadingList | ReadingList[]) => {
          const list = item as ReadingList;
          this.readingListService.updateByChapter(list.id, seriesId, chapter.id).subscribe(() => {
            this.toastr.success(translate('toasts.chapter-added-to-reading-list'));
            rlRef.close();
          });
        });

        return new Observable<ActionResult<Chapter>>(subscriber => {
          rlRef.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, chapter, 'none'));
            subscriber.complete();
          });
          rlRef.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });
      }

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
  handleBookmarkAction(action: ActionItem<PageBookmark>, bookmark: PageBookmark, contextFunc: () => {seriesId: number, libraryId: number, seriesName: string}) {
    const ctx = contextFunc();
    switch (action.action) {
      case Action.Delete:
        return from(this.confirmService.confirm(translate('bookmarks.confirm-single-delete', {seriesName: ctx.seriesName}))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.readerService.clearBookmarks(ctx.seriesId)),
          tap(() => this.toastr.success(translate('bookmarks.delete-single-success'))),
          map(() => this.fromAction(action, bookmark, 'remove'))
        );

      case Action.Download:
        this.downloadService.download(DownloadEntityType.Bookmark, [bookmark], 0, 0);
        return of(this.fromAction(action, bookmark, 'none'));

      case Action.ViewSeries:
        this.router.navigate(['library', ctx.libraryId, 'series', ctx.seriesId]);
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

      case Action.Download:
        this.downloadService.download(DownloadEntityType.ReadingList, readingList, 0, 0);
        return of(this.fromAction(action, readingList, 'none'));

      case Action.Edit:
        const ref = this.modalService.open(EditReadingListModalComponent, editModal());
        ref.componentInstance.readingList = readingList;
        return this.handleEditModal(ref, action, readingList);
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

      case Action.ExportAsV1:
        return this.downloadService.exportReadingList(readingList.id, readingList.title).pipe(
          map(() => this.fromAction(action, readingList, 'none'))
        );
      case Action.ExportAsV2:
        return this.downloadService.exportReadingList(readingList.id, readingList.title, true).pipe(
          map(() => this.fromAction(action, readingList, 'none'))
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
        const ref = this.modalService.open(EditCollectionTagsModalComponent, editModal());
        ref.setInput('tag', collection);
        return this.handleEditModal(ref, action, collection);

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
      case Action.Download:
        this.downloadService.download(DownloadEntityType.Collection, collection, 0, 0);
        return of(this.fromAction(action, collection, 'none'));

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
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-client-device'))).pipe(
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
        const ref = this.modalService.open(EditPersonModalComponent, editModal());
        ref.componentInstance.person = person;

        return this.handleEditModal(ref, action, person);

      case Action.Merge:
        const ref2 = this.modalService.open(MergePersonModalComponent, editModal());
        ref2.componentInstance.person = person;

        return from(ref2.closed).pipe(
          filter((res: ModalResult<Person>) => res.success),
          map((res: ModalResult<Person>) =>
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
        const ref = this.modalService.open(EditSmartFilterModalComponent, editModal());
        ref.componentInstance.smartFilter = smartFilter;
        ref.componentInstance.allFilters = allFilters;
        return this.handleEditModal(ref, action, smartFilter);
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-smart-filter'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.filterService.deleteFilter(smartFilter.id)),
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

  /**
   * Centralized handler for all bulk library actions.
   * Returns Observable<ActionResult<Library>> so the caller can react to effects.
   */
  handleBulkLibraryAction(action: ActionItem<Library>, library: Library) {
    // manage-library handles all actions, the actionables don't perform as other implementations
    return of(this.fromAction(action, library, 'none'));
  }

  // -------------------------------------------
  //      BULK HANDLERS
  // -------------------------------------------

  handleBulkSeriesAction(action: ActionItem<any>, series: Series[]): Observable<ActionResult<Series[]>> {
    switch (action.action) {
      case Action.MarkAsRead:
      case Action.MarkAsReadWithSession:
        const generateReadingSession = action.action === Action.MarkAsReadWithSession;
        return this.readerService.markMultipleSeriesRead(series.map(s => s.id), generateReadingSession).pipe(
          tap(() => {
            series.forEach(s => s.pagesRead = s.pages);
            this.toastr.success(translate('toasts.mark-read'));
          }),
          map(() => this.fromAction(action, series, 'update'))
        );

      case Action.MarkAsUnread:
        return this.readerService.markMultipleSeriesUnread(series.map(s => s.id)).pipe(
          tap(() => {
            series.forEach(s => s.pagesRead = 0);
            this.toastr.success(translate('toasts.mark-unread'));
          }),
          map(() => this.fromAction(action, series, 'update'))
        );

      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-multiple-series', {count: series.length}))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.seriesService.deleteMultipleSeries(series.map(s => s.id))),
          tap(res => {
            if (res) {
              this.toastr.success(translate('toasts.series-deleted'));
            } else {
              this.toastr.error(translate('errors.generic'));
            }
          }),
          filter(res => res),
          map(() => this.fromAction(action, series, 'remove'))
        );

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        const rlRef = this.modalService.open(ListSelectModalComponent<ReadingList>, addToModal());
        this.readingListModalRef = rlRef;

        const bulkSeriesIds = series.map(s => s.id);
        rlRef.setInput('title', translate('actionable.multiple-selections'));
        rlRef.setInput('showCreate', true);
        rlRef.setInput('createLabel', translate('add-to-list-modal.reading-list-label'));
        rlRef.setInput('inputItems', []);
        rlRef.setInput('loading', true);

        this.readingListService.getReadingLists(false, true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => rlRef.setInput('loading', false))
        ).subscribe(result => {
          rlRef.setInput('inputItems', result.result.map(l => ({ label: l.title, value: l })));
        });

        rlRef.setInput('interceptCreate', (name: string) =>
          this.readingListService.createList(name).pipe(
            switchMap(list => this.readingListService.updateByMultipleSeries(list.id, bulkSeriesIds)),
            tap(() => this.toastr.success(translate('toasts.series-added-to-reading-list')))
          )
        );

        rlRef.setInput('interceptConfirm', (item: ReadingList | ReadingList[]) => {
          const list = item as ReadingList;
          this.readingListService.updateByMultipleSeries(list.id, bulkSeriesIds).subscribe(() => {
            this.toastr.success(translate('toasts.series-added-to-reading-list'));
            rlRef.close();
          });
        });

        return new Observable<ActionResult<Series[]>>(subscriber => {
          rlRef.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, series, 'none'));
            subscriber.complete();
          });
          rlRef.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.AddToCollection: {
        if (this.collectionModalRef != null) return EMPTY;
        const colRef = this.modalService.open(ListSelectModalComponent, addToModal()) as TypedModalRef<ListSelectModalComponent<UserCollection>>;
        this.collectionModalRef = colRef;

        const bulkColSeriesIds = series.map(s => s.id);
        colRef.setInput('title', translate('bulk-add-to-collection.title'));
        colRef.setInput('showCreate', true);
        colRef.setInput('createLabel', translate('bulk-add-to-collection.collection-label'));
        colRef.setInput('createInitialValue', translate('actionable.new-collection'));
        colRef.setInput('inputItems', []);
        colRef.setInput('loading', true);

        this.collectionService.allCollections(true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => colRef.setInput('loading', false))
        ).subscribe(tags => {
          const collections = tags.filter(t => t.source === ScrobbleProvider.Kavita);
          colRef.setInput('inputItems', collections.map(c => ({ label: c.title, value: c })));
        });

        colRef.setInput('interceptCreate', (name: string) =>
          this.collectionService.addByMultiple(0, bulkColSeriesIds, name).pipe(
            tap(() => this.toastr.success(translate('toasts.series-added-to-collection', { collectionName: name })))
          )
        );

        colRef.setInput('interceptConfirm', (item: UserCollection | UserCollection[]) => {
          const tag = item as UserCollection;
          this.collectionService.addByMultiple(tag.id, bulkColSeriesIds, '').subscribe(() => {
            this.toastr.success(translate('toasts.series-added-to-collection', { collectionName: tag.title }));
            colRef.close();
          });
        });

        return new Observable<ActionResult<Series[]>>(subscriber => {
          colRef.closed.subscribe(() => {
            this.collectionModalRef = null;
            subscriber.next(this.fromAction(action, series, 'none'));
            subscriber.complete();
          });
          colRef.dismissed.subscribe(() => {
            this.collectionModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.AddToWantToReadList:
        return this.memberService.addSeriesToWantToRead(series.map(s => s.id)).pipe(
          tap(() => this.toastr.success(translate('toasts.series-added-want-to-read'))),
          map(() => this.fromAction(action, series, 'none'))
        );

      case Action.RemoveFromWantToReadList:
        return this.memberService.removeSeriesToWantToRead(series.map(s => s.id)).pipe(
          tap(() => this.toastr.success(translate('toasts.series-removed-want-to-read'))),
          map(() => this.fromAction(action, series, 'reload'))
        );

      case Action.SetReadingProfile: {
        if (this.readingListModalRef != null) return EMPTY;
        this.readingListModalRef = this.modalService.open(BulkSetReadingProfileModalComponent, addToModal());
        this.readingListModalRef.setInput('seriesIds', series.map(s => s.id));

        const ref = this.readingListModalRef;
        return new Observable<ActionResult<Series[]>>(subscriber => {
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

      case Action.Download:
        for (const s of series) { this.downloadService.download(DownloadEntityType.Series, s, s.libraryId, s.id); }
        return of(this.fromAction(action, series, 'none'));

      default:
        return of(this.fromAction(action, series, 'none'));
    }
  }

  handleBulkVolumeChapterAction(action: ActionItem<any>, volumes: Volume[], chapters: Chapter[], seriesId: number, libraryId = 0): Observable<ActionResult<any[]>> {
    switch (action.action) {
      case Action.MarkAsRead:
      case Action.MarkAsReadWithSession:
        const generateReadingSession = action.action === Action.MarkAsReadWithSession;
        return this.readerService.markMultipleRead(seriesId, volumes.map(v => v.id), chapters.map(c => c.id), generateReadingSession).pipe(
          tap(() => {
            volumes.forEach(v => {
              v.pagesRead = v.pages;
              v.chapters?.forEach(c => c.pagesRead = c.pages);
            });
            chapters.forEach(c => c.pagesRead = c.pages);
            this.toastr.success(translate('toasts.mark-read'));
          }),
          map(() => this.fromAction(action, [...volumes, ...chapters], 'update'))
        );

      case Action.MarkAsUnread:
        return this.readerService.markMultipleUnread(seriesId, volumes.map(v => v.id), chapters.map(c => c.id)).pipe(
          tap(() => {
            volumes.forEach(v => {
              v.pagesRead = 0;
              v.chapters?.forEach(c => c.pagesRead = 0);
            });
            chapters.forEach(c => c.pagesRead = 0);
            this.toastr.success(translate('toasts.mark-unread'));
          }),
          map(() => this.fromAction(action, [...volumes, ...chapters], 'update'))
        );

      case Action.AddToReadingList: {
        if (this.readingListModalRef != null) return EMPTY;
        const rlRef = this.modalService.open(ListSelectModalComponent, addToModal()) as TypedModalRef<ListSelectModalComponent<ReadingList>>;
        this.readingListModalRef = rlRef;

        const volumeIdList = volumes.map(v => v.id);
        const chapterIdList = chapters.map(c => c.id);
        rlRef.setInput('title', translate('actionable.multiple-selections'));
        rlRef.setInput('showCreate', true);
        rlRef.setInput('createLabel', translate('add-to-list-modal.reading-list-label'));
        rlRef.setInput('inputItems', []);
        rlRef.setInput('loading', true);

        this.readingListService.getReadingLists(false, true).pipe(
          take(1),
          catchError(() => EMPTY),
          finalize(() => rlRef.setInput('loading', false))
        ).subscribe(result => {
          rlRef.setInput('inputItems', result.result.map(l => ({ label: l.title, value: l })));
        });

        rlRef.setInput('interceptCreate', (name: string) =>
          this.readingListService.createList(name).pipe(
            switchMap(list => this.readingListService.updateByMultiple(list.id, seriesId, volumeIdList, chapterIdList)),
            tap(() => this.toastr.success(translate('toasts.multiple-added-to-reading-list')))
          )
        );

        rlRef.setInput('interceptConfirm', (item: ReadingList | ReadingList[]) => {
          const list = item as ReadingList;
          this.readingListService.updateByMultiple(list.id, seriesId, volumeIdList, chapterIdList).subscribe(() => {
            this.toastr.success(translate('toasts.multiple-added-to-reading-list'));
            rlRef.close();
          });
        });

        return new Observable<ActionResult<any[]>>(subscriber => {
          rlRef.closed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.next(this.fromAction(action, [...volumes, ...chapters], 'none'));
            subscriber.complete();
          });
          rlRef.dismissed.subscribe(() => {
            this.readingListModalRef = null;
            subscriber.complete();
          });
        });
      }

      case Action.SendTo: {
        const device = action._extra!.data as Device;
        const chapterIds = [
          ...volumes.flatMap(v => v.chapters?.map(c => c.id) ?? []),
          ...chapters.map(c => c.id)
        ];
        return this.deviceService.sendToEmailDevice(chapterIds, device.id).pipe(
          tap(() => this.toastr.success(translate('toasts.file-send-to', {name: device.name}))),
          map(() => this.fromAction(action, [...volumes, ...chapters], 'none'))
        );
      }

      case Action.Delete: {
        const entities = [...volumes, ...chapters];
        const deleteOps: Observable<any>[] = [];

        if (volumes.length > 0) {
          deleteOps.push(
            from(this.confirmService.confirm(translate('toasts.confirm-delete-multiple-volumes', {count: volumes.length}))).pipe(
              filter(confirmed => confirmed),
              switchMap(() => this.volumeService.deleteMultipleVolumes(volumes.map(v => v.id)))
            )
          );
        }

        if (chapters.length > 0) {
          deleteOps.push(
            from(this.confirmService.confirm(translate('toasts.confirm-delete-multiple-chapters', {count: chapters.length}))).pipe(
              filter(confirmed => confirmed),
              switchMap(() => this.chapterService.deleteMultipleChapters(seriesId, chapters.map(c => c.id)))
            )
          );
        }

        if (deleteOps.length === 0) return EMPTY;

        return from(deleteOps).pipe(
          switchMap(op => op),
          map(() => this.fromAction(action, entities, 'remove'))
        );
      }

      case Action.Download:
        this.downloadService.downloadBulk(volumes, chapters, libraryId, seriesId);
        return of(this.fromAction(action, [...volumes, ...chapters], 'none'));

      default:
        return of(this.fromAction(action, [...volumes, ...chapters], 'none'));
    }
  }

  handleBulkBookmarkAction(action: ActionItem<any>, bookmarks: PageBookmark[], seriesIds: number[]): Observable<ActionResult<PageBookmark[]>> {
    switch (action.action) {
      case Action.Download:
        this.downloadService.download(DownloadEntityType.Bookmark, bookmarks, 0, 0);
        return of(this.fromAction(action, bookmarks, 'none'));

      case Action.Delete:
        return from(this.confirmService.confirm(translate('bookmarks.confirm-single-delete', {seriesName: ''}))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.readerService.clearMultipleBookmarks(seriesIds)),
          tap(() => this.toastr.success(translate('bookmarks.delete-single-success'))),
          map(() => this.fromAction(action, bookmarks, 'remove'))
        );

      default:
        return of(this.fromAction(action, bookmarks, 'none'));
    }
  }

  handleBulkCollectionAction(action: ActionItem<any>, collections: UserCollection[]): Observable<ActionResult<UserCollection[]>> {
    switch (action.action) {
      case Action.Promote:
        return this.collectionTagService.promoteMultipleCollections(collections.map(c => c.id), true).pipe(
          tap(() => this.toastr.success(translate('toasts.collections-promoted'))),
          map(() => this.fromAction(action, collections.map(c => ({...c, promoted: true})), 'update'))
        );

      case Action.UnPromote:
        return this.collectionTagService.promoteMultipleCollections(collections.map(c => c.id), false).pipe(
          tap(() => this.toastr.success(translate('toasts.collections-unpromoted'))),
          map(() => this.fromAction(action, collections.map(c => ({...c, promoted: false})), 'update'))
        );

      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-collections'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.collectionTagService.deleteMultipleCollections(collections.map(c => c.id))),
          tap(() => this.toastr.success(translate('toasts.collections-deleted'))),
          map(() => this.fromAction(action, collections, 'remove'))
        );

      case Action.Download:
        for (let c of collections) this.downloadService.download(DownloadEntityType.Collection, c, 0, 0);
        return of(this.fromAction(action, collections, 'none'));

      default:
        return of(this.fromAction(action, collections, 'none'));
    }
  }

  handleBulkReadingListAction(action: ActionItem<any>, readingLists: ReadingList[]): Observable<ActionResult<ReadingList[]>> {
    switch (action.action) {
      case Action.Promote:
        return this.readingListService.promoteMultipleReadingLists(readingLists.map(r => r.id), true).pipe(
          tap(() => this.toastr.success(translate('toasts.reading-list-promoted'))),
          map(() => this.fromAction(action, readingLists.map(r => ({...r, promoted: true})), 'update'))
        );

      case Action.UnPromote:
        return this.readingListService.promoteMultipleReadingLists(readingLists.map(r => r.id), false).pipe(
          tap(() => this.toastr.success(translate('toasts.reading-list-unpromoted'))),
          map(() => this.fromAction(action, readingLists.map(r => ({...r, promoted: false})), 'update'))
        );

      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-reading-list'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.readingListService.deleteMultipleReadingLists(readingLists.map(r => r.id))),
          tap(() => this.toastr.success(translate('toasts.reading-lists-deleted'))),
          map(() => this.fromAction(action, readingLists, 'remove'))
        );

      case Action.Download:
        for (const rl of readingLists) { this.downloadService.download(DownloadEntityType.ReadingList, rl, 0, 0); }
        return of(this.fromAction(action, readingLists, 'none'));

      default:
        return of(this.fromAction(action, readingLists, 'none'));
    }
  }

  handleBulkAnnotationAction(action: ActionItem<any>, annotations: Annotation[]): Observable<ActionResult<Annotation[]>> {
    switch (action.action) {
      case Action.Delete:
        return from(this.confirmService.confirm(translate('toasts.confirm-delete-annotations'))).pipe(
          filter(confirmed => confirmed),
          switchMap(() => this.annotationsService.bulkDelete(annotations.map(a => a.id))),
          tap(() => this.toastr.success(translate('toasts.annotations-deleted'))),
          map(() => this.fromAction(action, annotations, 'remove'))
        );

      case Action.Export:
        return this.annotationsService.exportAnnotations(annotations.map(a => a.id)).pipe(
          map(() => this.fromAction(action, annotations, 'none'))
        );

      case Action.Like:
        return this.annotationsService.likeAnnotations(annotations.map(a => a.id)).pipe(
          map(() => this.fromAction(action, annotations, 'update'))
        );

      case Action.UnLike:
        return this.annotationsService.unLikeAnnotations(annotations.map(a => a.id)).pipe(
          map(() => this.fromAction(action, annotations, 'update'))
        );

      default:
        return of(this.fromAction(action, annotations, 'none'));
    }
  }

  handleBulkSideNavStreamAction(action: ActionItem<any>, streams: SideNavStream[]): Observable<ActionResult<SideNavStream[]>> {
    switch (action.action) {
      case Action.MarkAsVisible:
        return this.sideNavService.bulkToggleSideNavStreamVisibility(streams.map(s => s.id), true).pipe(
          map(() => this.fromAction(action, streams.map(s => ({...s, visible: true})), 'update'))
        );

      case Action.MarkAsInvisible:
        return this.sideNavService.bulkToggleSideNavStreamVisibility(streams.map(s => s.id), false).pipe(
          map(() => this.fromAction(action, streams.map(s => ({...s, visible: false})), 'update'))
        );

      default:
        return of(this.fromAction(action, streams, 'none'));
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

    this.libraryService.scan(library.id, false).subscribe((res: any) => {
      this.toastr.info(translate('toasts.scan-queued', {name: library.name}));
      if (callback) {
        callback(library);
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

  editReadingList(readingList: ReadingList, callback?: ReadingListActionCallback) {
    const readingListModalRef = this.modalService.open(EditReadingListModalComponent, editModal());
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

  matchSeries(series: Series, callback?: BooleanActionCallback) {
   const ref = this.modalService.open(MatchSeriesModalComponent);
     ref.setInput('series', series);
     ref.closed.subscribe(saved => {
       if (callback) {
         callback(saved);
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

    this.readingListModalRef = this.modalService.open(BulkSetReadingProfileModalComponent, addToModal());
    this.readingListModalRef.setInput('seriesIds', series.map(s => s.id));

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

    this.readingListModalRef = this.modalService.open(BulkSetReadingProfileModalComponent, addToModal());
    this.readingListModalRef.setInput('libraryId', library.id);

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

  private handleEditModal<T>(ref: NgbModalRef, action: ActionItem<T>, fallbackEntity: T): Observable<ActionResult<T>> {
    return from(ref.closed).pipe(
      filter((res: ModalResult<T>) => res.success),
      map(res => {
        if (res.isDeleted) return this.fromAction(action, fallbackEntity, 'remove');
        return this.fromAction(action, res.data ?? fallbackEntity, 'update');
      })
    );
  }


  private fromAction<T>(action: ActionItem<T>, data: T, effect: ActionEffect): ActionResult<T> {
    return { action: action.action, entity: data, effect: effect };
  }
}
