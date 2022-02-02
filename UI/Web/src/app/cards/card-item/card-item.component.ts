import { Component, EventEmitter, HostListener, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Observable, Subject } from 'rxjs';
import { finalize, take, takeUntil, takeWhile } from 'rxjs/operators';
import { Download } from 'src/app/shared/_models/download';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { MangaFormat } from 'src/app/_models/manga-format';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { RecentlyAddedItem } from 'src/app/_models/recently-added-item';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
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
  @Input() supressLibraryLink = false;
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
   * This will supress the cannot read archive warning when total pages is 0
   */
   @Input() supressArchiveWarning: boolean = false;
  /**
    * The number of updates/items within the card. If less than 2, will not be shown.
    */
   @Input() count: number = 0;
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

  isShiftDown: boolean = false;

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
    private toastr: ToastrService, public bulkSelectionService: BulkSelectionService) {}

  ngOnInit(): void {
    if (this.entity.hasOwnProperty('promoted') && this.entity.hasOwnProperty('title')) {
      this.supressArchiveWarning = true;
    }

    if (this.supressLibraryLink === false) {
      this.libraryService.getLibraryNames().pipe(takeUntil(this.onDestroy)).subscribe(names => {
        if (this.entity !== undefined && this.entity.hasOwnProperty('libraryId')) {
          this.libraryId = (this.entity as Series).libraryId;
          this.libraryName = names[this.libraryId];
        }
      });
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
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }


  prevTouchTime: number = 0;
  prevOffset: number = 0;
  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (!this.allowSelection) return;
    const verticalOffset = (window.pageYOffset 
      || document.documentElement.scrollTop 
      || document.body.scrollTop || 0);

    this.prevTouchTime = event.timeStamp;
    this.prevOffset = verticalOffset;
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    if (!this.allowSelection) return;
    const delta = event.timeStamp - this.prevTouchTime;
    const verticalOffset = (window.pageYOffset 
      || document.documentElement.scrollTop 
      || document.body.scrollTop || 0);

    if (verticalOffset != this.prevOffset) {
      this.prevTouchTime = 0;

      return;
    }

    if (delta >= 300 && delta <= 1000) {
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
