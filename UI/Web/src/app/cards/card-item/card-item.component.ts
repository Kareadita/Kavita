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
  @Input() entity!: Series | Volume | Chapter | CollectionTag;
  /**
   * If the entity is selected or not. 
   */
  @Input() selected: boolean = false;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
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
   * This will supress the cannot read archive warning when total pages is 0
   */
  supressArchiveWarning: boolean = false;
  /**
   * Format of the entity (only applies to Series)
   */
  format: MangaFormat = MangaFormat.UNKNOWN;
  

  download$: Observable<Download> | null = null;
  downloadInProgress: boolean = false;

  isShiftDown: boolean = false;
  

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
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  // touchStartTimestamp: number = 0;
  // touchEndTimestamp: number = 0;
  // prevTouchTimestamp: number = 0;
  touchTimer?: number | undefined = undefined;

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (this.touchTimer) {
      clearTimeout(this.touchTimer);
      setTimeout(this.checkLongPress, 300);
    }
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    clearTimeout(this.touchTimer);
  }

  checkLongPress() {
    this.handleSelection();
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
