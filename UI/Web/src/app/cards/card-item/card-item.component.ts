import { Component, EventEmitter, HostListener, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Observable, Subject } from 'rxjs';
import { filter, finalize, map, take, takeUntil, takeWhile } from 'rxjs/operators';
import { Download } from 'src/app/shared/_models/download';
import { DownloadService } from 'src/app/shared/_services/download.service';
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
  styleUrls: ['./card-item.component.scss']
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
  

  download$: Observable<Download> | null = null;
  downloadInProgress: boolean = false;

  /**
   * Handles touch events for selection on mobile devices
   */
  prevTouchTime: number = 0;
  /**
   * Handles touch events for selection on mobile devices to ensure you aren't touch scrolling
   */
  prevOffset: number = 0;

  private user: User | undefined;

  get tooltipTitle() {
    if (this.chapterTitle === '' || this.chapterTitle === null) return this.title;
    return this.chapterTitle;
  }
  

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  private readonly onDestroy = new Subject<void>();

  constructor(public imageService: ImageService, private libraryService: LibraryService, 
    public utilityService: UtilityService, private downloadService: DownloadService,
    private toastr: ToastrService, public bulkSelectionService: BulkSelectionService,
    private messageHub: MessageHubService, private accountService: AccountService, private scrollService: ScrollService) {}

  ngOnInit(): void {
    if (this.entity.hasOwnProperty('promoted') && this.entity.hasOwnProperty('title')) {
      this.suppressArchiveWarning = true;
    }

    if (this.suppressLibraryLink === false) {
      if (this.entity !== undefined && this.entity.hasOwnProperty('libraryId')) {
        this.libraryId = (this.entity as Series).libraryId;
      }

      if (this.libraryId !== undefined && this.libraryId > 0) {
        this.libraryService.getLibraryName(this.libraryId).pipe(takeUntil(this.onDestroy)).subscribe(name => {
          this.libraryName = name;
        });
      }
    }
    this.format = (this.entity as Series).format;

    if (this.utilityService.isChapter(this.entity)) {
      this.chapterTitle = this.utilityService.asChapter(this.entity).titleName;
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
      
      this.read = updateEvent.pagesRead;
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }


  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (!this.allowSelection) return;

    this.prevTouchTime = event.timeStamp;
    this.prevOffset = this.scrollService.scrollPosition;
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    if (!this.allowSelection) return;
    const delta = event.timeStamp - this.prevTouchTime;
    const verticalOffset = this.scrollService.scrollPosition;

    if (delta >= 300 && delta <= 1000 && (verticalOffset === this.prevOffset)) {
      this.handleSelection();
      event.stopPropagation();
      event.preventDefault();
    }
    this.prevTouchTime = 0;
  }


  handleClick(event?: any) {
    this.clicked.emit(this.title);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(action: ActionItem<any>) {
    if (action.action == Action.Download) {
      if (this.downloadInProgress === true) {
        this.toastr.info('Download is already in progress. Please wait.');
        return;
      }
      
      if (this.utilityService.isVolume(this.entity)) {
        const volume = this.utilityService.asVolume(this.entity);
        this.downloadService.downloadVolumeSize(volume.id).pipe(take(1)).subscribe(async (size) => {
          const wantToDownload = await this.downloadService.confirmSize(size, 'volume');
          if (!wantToDownload) { return; }
          this.downloadInProgress = true;
          this.download$ = this.downloadService.downloadVolume(volume).pipe(
            takeWhile(val => {
              return val.state != 'DONE';
            }),
            finalize(() => {
              this.download$ = null;
              this.downloadInProgress = false;
            }));
        });
      } else if (this.utilityService.isChapter(this.entity)) {
        const chapter = this.utilityService.asChapter(this.entity);
        this.downloadService.downloadChapterSize(chapter.id).pipe(take(1)).subscribe(async (size) => {
          const wantToDownload = await this.downloadService.confirmSize(size, 'chapter');
          if (!wantToDownload) { return; }
          this.downloadInProgress = true;
          this.download$ = this.downloadService.downloadChapter(chapter).pipe(
            takeWhile(val => {
              return val.state != 'DONE';
            }),
            finalize(() => {
              this.download$ = null;
              this.downloadInProgress = false;
            }));
        });
      } else if (this.utilityService.isSeries(this.entity)) {
        const series = this.utilityService.asSeries(this.entity);
        this.downloadService.downloadSeriesSize(series.id).pipe(take(1)).subscribe(async (size) => {
          const wantToDownload = await this.downloadService.confirmSize(size, 'series');
          if (!wantToDownload) { return; }
          this.downloadInProgress = true;
          this.download$ = this.downloadService.downloadSeries(series).pipe(
            takeWhile(val => {
              return val.state != 'DONE';
            }),
            finalize(() => {
              this.download$ = null;
              this.downloadInProgress = false;
            }));
        });
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
  }
}
