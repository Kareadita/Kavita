import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, HostListener, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Observable, Subject } from 'rxjs';
import { filter, finalize, map, take, takeUntil, takeWhile } from 'rxjs/operators';
import { Download } from 'src/app/shared/_models/download';
import { DownloadEntityType, DownloadEvent, DownloadService } from 'src/app/shared/_services/download.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { UserProgressUpdateEvent } from 'src/app/_models/events/user-progress-update-event';
import { MangaFormat } from 'src/app/_models/manga-format';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { RecentlyAddedItem } from 'src/app/_models/recently-added-item';
import { Series } from 'src/app/_models/series';
import { User } from 'src/app/_models/user';
import { Volume } from 'src/app/_models/volume';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { BulkSelectionService } from '../bulk-selection.service';

@Component({
  selector: 'app-card-item',
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardItemComponent implements OnInit, OnDestroy {

  /**
   * Card item url. Will internally handle error and missing covers
   */
  @Input() imageUrl = '';
  /**
   * Name of the card
   */
  @Input() title = '';
  /**
   * Shows below the title. Defaults to not visible
   */
  @Input() subtitle = '';
  /**
   * Any actions to perform on the card
   */
  @Input() actions: ActionItem<any>[] = [];
  /**
   * Pages Read
   */
  @Input() read = 0;
  /**
   * Total Pages
   */
  @Input() total = 0;
  /**
   * Supress library link
   */
  @Input() suppressLibraryLink = false;
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  @Input() entity!: Series | Volume | Chapter | CollectionTag | PageBookmark | RecentlyAddedItem;
  /**
   * If the entity is selected or not.
   */
  @Input() selected: boolean = false;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
  /**
   * This will suppress the cannot read archive warning when total pages is 0
   */
  @Input() suppressArchiveWarning: boolean = false;
  /**
   * The number of updates/items within the card. If less than 2, will not be shown.
   */
  @Input() count: number = 0;
  /**
   * Additional information to show on the overlay area. Will always render.
   */
  @Input() overlayInformation: string = '';
  /**
   * Event emitted when item is clicked
   */
  @Output() clicked = new EventEmitter<string>();
  /**
   * When the card is selected.
   */
  @Output() selection = new EventEmitter<boolean>();
  /**
   * Library name item belongs to
   */
  libraryName: string | undefined = undefined;
  libraryId: number | undefined = undefined;
  /**
   * Format of the entity (only applies to Series)
   */
  format: MangaFormat = MangaFormat.UNKNOWN;
  chapterTitle: string = '';
  tooltipTitle: string = this.title;

  /**
   * This is the download we get from download service.
   */
  download$: Observable<DownloadEvent | null> | null = null;

  /**
   * Handles touch events for selection on mobile devices
   */
  prevTouchTime: number = 0;
  /**
   * Handles touch events for selection on mobile devices to ensure you aren't touch scrolling
   */
  prevOffset: number = 0;
  selectionInProgress: boolean = false;

  private user: User | undefined;

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  private readonly onDestroy = new Subject<void>();

  constructor(public imageService: ImageService, private libraryService: LibraryService,
    public utilityService: UtilityService, private downloadService: DownloadService,
    private toastr: ToastrService, public bulkSelectionService: BulkSelectionService,
    private messageHub: MessageHubService, private accountService: AccountService, 
    private scrollService: ScrollService, private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    if (this.entity.hasOwnProperty('promoted') && this.entity.hasOwnProperty('title')) {
      this.suppressArchiveWarning = true;
      this.cdRef.markForCheck();
    }

    if (this.suppressLibraryLink === false) {
      if (this.entity !== undefined && this.entity.hasOwnProperty('libraryId')) {
        this.libraryId = (this.entity as Series).libraryId;
        this.cdRef.markForCheck();
      }

      if (this.libraryId !== undefined && this.libraryId > 0) {
        this.libraryService.getLibraryName(this.libraryId).pipe(takeUntil(this.onDestroy)).subscribe(name => {
          this.libraryName = name;
          this.cdRef.markForCheck();
        });
      }
    }
    this.format = (this.entity as Series).format;

    if (this.utilityService.isChapter(this.entity)) {
      this.chapterTitle = this.utilityService.asChapter(this.entity).titleName;
      if (this.chapterTitle === '' || this.chapterTitle === null) {
        this.tooltipTitle = (this.utilityService.asChapter(this.entity).volumeTitle + ' ' + this.title).trim();
      } else {
        this.tooltipTitle = this.chapterTitle;
      }
    } else if (this.utilityService.isVolume(this.entity)) {
      const vol = this.utilityService.asVolume(this.entity);
      if (vol.chapters !== undefined && vol.chapters.length > 0) {
        this.chapterTitle = vol.chapters[0].titleName;
      }
    }

    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy)).subscribe(user => {
      this.user = user;
    });

    this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
    map(evt => evt.payload as UserProgressUpdateEvent), takeUntil(this.onDestroy)).subscribe(updateEvent => {
      if (this.user === undefined || this.user.username !== updateEvent.username) return;
      if (this.utilityService.isChapter(this.entity) && updateEvent.chapterId !== this.entity.id) return;
      if (this.utilityService.isVolume(this.entity) && updateEvent.volumeId !== this.entity.id) return;
      if (this.utilityService.isSeries(this.entity) && updateEvent.seriesId !== this.entity.id) return;

      // For volume or Series, we can't just take the event 
      if (this.utilityService.isVolume(this.entity) || this.utilityService.isSeries(this.entity)) {
        if (this.utilityService.isVolume(this.entity)) {
          const v = this.utilityService.asVolume(this.entity);
          const chapter = v.chapters.find(c => c.id === updateEvent.chapterId);
          if (chapter) {
            chapter.pagesRead = updateEvent.pagesRead;
          }
        } else {
          // re-request progress for the series
          const s = this.utilityService.asSeries(this.entity);
          let pagesRead = 0;
          if (s.hasOwnProperty('volumes')) {
            s.volumes.forEach(v => {
              v.chapters.forEach(c => {
                if (c.id === updateEvent.chapterId) {
                  c.pagesRead = updateEvent.pagesRead;
                }
                pagesRead += c.pagesRead;
              });
            });
            s.pagesRead = pagesRead;
          }
        }
      }


      this.read = updateEvent.pagesRead;
      this.cdRef.detectChanges();
    });

    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntil(this.onDestroy), map((events) => {
      if(this.utilityService.isSeries(this.entity)) return events.find(e => e.entityType === 'series' && e.subTitle === this.downloadService.downloadSubtitle('series', (this.entity as Series))) || null;
      if(this.utilityService.isVolume(this.entity)) return events.find(e => e.entityType === 'volume' && e.subTitle === this.downloadService.downloadSubtitle('volume', (this.entity as Volume))) || null;
      if(this.utilityService.isChapter(this.entity)) return events.find(e => e.entityType === 'chapter' && e.subTitle === this.downloadService.downloadSubtitle('chapter', (this.entity as Chapter))) || null;
      // Is PageBookmark[]
      if(this.entity.hasOwnProperty('length')) return events.find(e => e.entityType === 'bookmark' && e.subTitle === this.downloadService.downloadSubtitle('bookmark', [(this.entity as PageBookmark)])) || null;
      return null;
    }));

  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  @HostListener('touchmove', ['$event'])
  onTouchMove(event: TouchEvent) {
    if (!this.allowSelection) return;

    this.selectionInProgress = false;
    this.cdRef.markForCheck();
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (!this.allowSelection) return;

    this.prevTouchTime = event.timeStamp;
    this.prevOffset = this.scrollService.scrollPosition;
    this.selectionInProgress = true;
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    if (!this.allowSelection) return;
    const delta = event.timeStamp - this.prevTouchTime;
    const verticalOffset = this.scrollService.scrollPosition;

    if (delta >= 300 && delta <= 1000 && (verticalOffset === this.prevOffset) && this.selectionInProgress) {
      this.handleSelection();
      event.stopPropagation();
      event.preventDefault();
    }
    this.prevTouchTime = 0;
    this.selectionInProgress = false;
  }


  handleClick(event?: any) {
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection();
      return;
    }
    this.clicked.emit(this.title);
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(action: ActionItem<any>) {
    if (action.action == Action.Download) {

      // if (this.download$ !== null) {
      //   this.toastr.info('Download is already in progress. Please wait.');
      //   return;
      // }

      if (this.utilityService.isVolume(this.entity)) {
        const volume = this.utilityService.asVolume(this.entity);
        this.downloadService.download('volume', volume);
      } else if (this.utilityService.isChapter(this.entity)) {
        const chapter = this.utilityService.asChapter(this.entity);
        this.downloadService.download('chapter', chapter);
      } else if (this.utilityService.isSeries(this.entity)) {
        const series = this.utilityService.asSeries(this.entity);
        this.downloadService.download('series', series);
      }
      return; // Don't propagate the download from a card
    }

    if (typeof action.callback === 'function') {
      action.callback(action.action, this.entity);
    }
  }


  isPromoted() {
    const tag = this.entity as CollectionTag;
    return tag.hasOwnProperty('promoted') && tag.promoted;
  }


  handleSelection(event?: any) {
    if (event) {
      event.stopPropagation();
    }
    this.selection.emit(this.selected);
    this.cdRef.detectChanges();
  }
}
