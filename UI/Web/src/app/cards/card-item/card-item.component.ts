import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ContentChild, DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  OnInit,
  Output, TemplateRef
} from '@angular/core';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { DownloadEvent, DownloadService } from 'src/app/shared/_services/download.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { UserCollection } from 'src/app/_models/collection-tag';
import { UserProgressUpdateEvent } from 'src/app/_models/events/user-progress-update-event';
import { MangaFormat } from 'src/app/_models/manga-format';
import { PageBookmark } from 'src/app/_models/readers/page-bookmark';
import { RecentlyAddedItem } from 'src/app/_models/recently-added-item';
import { Series } from 'src/app/_models/series';
import { User } from 'src/app/_models/user';
import { Volume } from 'src/app/_models/volume';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { BulkSelectionService } from '../bulk-selection.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {FormsModule} from "@angular/forms";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {MangaFormatIconPipe} from "../../_pipes/manga-format-icon.pipe";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {DecimalPipe, NgTemplateOutlet} from "@angular/common";
import {RouterLink, RouterLinkActive} from "@angular/router";
import {TranslocoModule} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {NextExpectedChapter} from "../../_models/series-detail/next-expected-chapter";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {PromotedIconComponent} from "../../shared/_components/promoted-icon/promoted-icon.component";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {BrowsePerson} from "../../_models/person/browse-person";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";

export type CardEntity = Series | Volume | Chapter | UserCollection | PageBookmark | RecentlyAddedItem | NextExpectedChapter | BrowsePerson;

@Component({
  selector: 'app-card-item',
  standalone: true,
  imports: [
    ImageComponent,
    NgbProgressbar,
    DownloadIndicatorComponent,
    FormsModule,
    NgbTooltip,
    MangaFormatPipe,
    MangaFormatIconPipe,
    CardActionablesComponent,
    SentenceCasePipe,
    RouterLink,
    TranslocoModule,
    SafeHtmlPipe,
    RouterLinkActive,
    PromotedIconComponent,
    SeriesFormatComponent,
    DecimalPipe,
    NgTemplateOutlet,
    CompactNumberPipe
  ],
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardItemComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly libraryService = inject(LibraryService);
  private readonly downloadService = inject(DownloadService);
  private readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly accountService = inject(AccountService);
  private readonly scrollService = inject(ScrollService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly actionFactoryService = inject(ActionFactoryService);

  protected readonly MangaFormat = MangaFormat;

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
   * Suppress library link
   */
  @Input() suppressLibraryLink = false;
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  @Input({required: true}) entity!: CardEntity;
  /**
   * If the entity is selected or not.
   */
  @Input() selected: boolean = false;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
  /**
   * This will suppress the "cannot read archive warning" when total pages is 0
   */
  @Input() suppressArchiveWarning: boolean = false;
  /**
   * The number of updates/items within the card. If less than 2, will not be shown.
   */
  @Input() count: number = 0;
  /**
   * Show a read button. Emits on (readClicked)
   */
  @Input() showReadButton: boolean = false;
  /**
   * If overlay is enabled, should the text be centered or not
   */
  @Input() centerOverlay = false;
  /**
   * Will generate a button to instantly read
   */
  @Input() hasReadButton = false;
  /**
   * A method that if defined will return the url
   */
  @Input() linkUrl?: string;
  /**
   * Show the format of the series
   */
  @Input() showFormat: boolean = true;
  /**
   * Event emitted when item is clicked
   */
  @Output() clicked = new EventEmitter<string>();
  /**
   * When the card is selected.
   */
  @Output() selection = new EventEmitter<boolean>();
  @Output() readClicked = new EventEmitter<CardEntity>();
  @ContentChild('subtitle') subtitleTemplate!: TemplateRef<any>;
  /**
   * Library name item belongs to
   */
  libraryName: string | undefined = undefined;
  libraryId: number | undefined = undefined;
  /**
   * Format of the entity (only applies to Series)
   */
  format: MangaFormat = MangaFormat.UNKNOWN;
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

  ngOnInit(): void {

    if (this.entity.hasOwnProperty('promoted') && this.entity.hasOwnProperty('title')) {
      this.suppressArchiveWarning = true;
      this.cdRef.markForCheck();
    }

    if (!this.suppressLibraryLink) {
      if (this.entity !== undefined && this.entity.hasOwnProperty('libraryId')) {
        this.libraryId = (this.entity as Series).libraryId;
        this.cdRef.markForCheck();
      }

      if (this.libraryId !== undefined && this.libraryId > 0) {
        this.libraryService.getLibraryName(this.libraryId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(name => {
          this.libraryName = name;
          this.cdRef.markForCheck();
        });
      }
    }
    this.format = (this.entity as Series).format;

    if (this.utilityService.isChapter(this.entity)) {
      const chapter = this.utilityService.asChapter(this.entity);
      const chapterTitle = chapter.titleName;
      if (chapterTitle === '' || chapterTitle === null || chapterTitle === undefined) {
        const volumeTitle = chapter.volumeTitle
        if (volumeTitle === '' || volumeTitle === null || volumeTitle === undefined) {
          this.tooltipTitle = (this.title).trim();
        } else {
          this.tooltipTitle = (volumeTitle + ' ' + this.title).trim();
        }
      } else {
        this.tooltipTitle = chapterTitle;
      }
    } else if (this.utilityService.isVolume(this.entity)) {
      const vol = this.utilityService.asVolume(this.entity);
      if (vol.chapters !== undefined && vol.chapters.length > 0) {
        this.tooltipTitle = vol.chapters[0].titleName;
      }
      if (this.tooltipTitle === '') {
        this.tooltipTitle = vol.name;
      }
    } else if (this.utilityService.isSeries(this.entity)) {
      this.tooltipTitle = this.title || (this.utilityService.asSeries(this.entity).name);
    } else if (this.entity.hasOwnProperty('expectedDate')) {
      this.suppressArchiveWarning = true;
      this.imageUrl = '';
      const nextDate = (this.entity as NextExpectedChapter);

      const tokens = nextDate.title.split(':');
      // this.overlayInformation = `
      //         <i class="fa-regular fa-clock mb-2" style="font-size: 26px" aria-hidden="true"></i>
      //         <div>${tokens[0]}</div><div>${tokens[1]}</div>`;
      // // todo: figure out where this caller is
      this.centerOverlay = true;

      if (nextDate.expectedDate) {
        const utcPipe = new UtcToLocalTimePipe();
        this.title = '~ ' + utcPipe.transform(nextDate.expectedDate, 'shortDate');
      }

      this.cdRef.markForCheck();
    } else {
      this.tooltipTitle = this.title;
    }


    this.filterSendTo();
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      this.user = user;
    });

    this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
    map(evt => evt.payload as UserProgressUpdateEvent), takeUntilDestroyed(this.destroyRef)).subscribe(updateEvent => {
      if (this.user === undefined || this.user.username !== updateEvent.username) return;
      if (this.utilityService.isChapter(this.entity) && updateEvent.chapterId !== this.entity.id) return;
      if (this.utilityService.isVolume(this.entity) && updateEvent.volumeId !== this.entity.id) return;
      if (this.utilityService.isSeries(this.entity) && updateEvent.seriesId !== this.entity.id) return;

      // For volume or Series, we can't just take the event
      if (this.utilityService.isChapter(this.entity)) {
        const c = this.utilityService.asChapter(this.entity);
        c.pagesRead = updateEvent.pagesRead;
        this.read = updateEvent.pagesRead;
      }
      if (this.utilityService.isVolume(this.entity) || this.utilityService.isSeries(this.entity)) {
        if (this.utilityService.isVolume(this.entity)) {
          const v = this.utilityService.asVolume(this.entity);
          let sum = 0;
          const chapters = v.chapters.filter(c => c.volumeId === updateEvent.volumeId);
          chapters.forEach(chapter => {
            chapter.pagesRead = updateEvent.pagesRead;
            sum += chapter.pagesRead;
          });
          v.pagesRead = sum;
          this.read = sum;
        } else {
          return;
        }
      }

      this.cdRef.detectChanges();
    });

    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
      return this.downloadService.mapToEntityType(events, this.entity);
    }));
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
      action.callback(action, this.entity);
    }
  }


  isPromoted() {
    const tag = this.entity as UserCollection;
    return tag.hasOwnProperty('promoted') && tag.promoted;
  }


  handleSelection(event?: any) {
    if (event) {
      event.stopPropagation();
    }
    this.selection.emit(this.selected);
    this.cdRef.detectChanges();
  }

  filterSendTo() {
    if (!this.actions || this.actions.length === 0) return;

    if (this.utilityService.isChapter(this.entity)) {
      this.actions = this.actionFactoryService.filterSendToAction(this.actions, this.entity as Chapter);
    } else if (this.utilityService.isVolume(this.entity)) {
      const vol = this.utilityService.asVolume(this.entity);
      this.actions = this.actionFactoryService.filterSendToAction(this.actions, vol.chapters[0]);
    } else if (this.utilityService.isSeries(this.entity)) {
      const series = (this.entity as Series);
      // if (series.format === MangaFormat.EPUB || series.format === MangaFormat.PDF) {
      //   this.actions = this.actions.filter(a => a.title !== 'Send To');
      // }
    }

    // this.actions = this.actions.filter(a => {
    //   if (!a.isAllowed) return true;
    //   return a.isAllowed(a, this.entity);
    // });
  }

  clickRead(event: any) {
    event.stopPropagation();
    if (this.bulkSelectionService.hasSelections()) return;

    this.readClicked.emit(this.entity);
  }
}
