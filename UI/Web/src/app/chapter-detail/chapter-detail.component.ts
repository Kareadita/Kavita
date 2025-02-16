import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {AsyncPipe, DOCUMENT, NgStyle, NgClass, DatePipe, Location} from "@angular/common";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {
  NgbDropdown,
  NgbDropdownItem,
  NgbDropdownMenu,
  NgbDropdownToggle, NgbModal,
  NgbNav, NgbNavChangeEvent,
  NgbNavContent, NgbNavItem,
  NgbNavLink, NgbNavOutlet,
  NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {ImageService} from "../_services/image.service";
import {ChapterService} from "../_services/chapter.service";
import {Chapter} from "../_models/chapter";
import {forkJoin, map, Observable, tap} from "rxjs";
import {SeriesService} from "../_services/series.service";
import {Series} from "../_models/series";
import {AgeRating} from "../_models/metadata/age-rating";
import {LibraryType} from "../_models/library/library";
import {LibraryService} from "../_services/library.service";
import {ThemeService} from "../_services/theme.service";
import {DownloadEvent, DownloadService} from "../shared/_services/download.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {BulkSelectionService} from "../cards/bulk-selection.service";
import {ToastrService} from "ngx-toastr";
import {ReaderService} from "../_services/reader.service";
import {AccountService} from "../_services/account.service";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {DetailsTabComponent} from "../_single-module/details-tab/details-tab.component";
import {EntityTitleComponent} from "../cards/entity-title/entity-title.component";
import {EditChapterModalComponent} from "../_single-module/edit-chapter-modal/edit-chapter-modal.component";
import {FilterField} from "../_models/metadata/v2/filter-field";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ReadingList} from "../_models/reading-list";
import {ReadingListService} from "../_services/reading-list.service";
import {RelatedTabComponent} from "../_single-module/related-tab/related-tab.component";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {
  MetadataDetailRowComponent
} from "../series-detail/_components/metadata-detail-row/metadata-detail-row.component";
import {DownloadButtonComponent} from "../series-detail/_components/download-button/download-button.component";
import {hasAnyCast} from "../_models/common/i-has-cast";
import {Breakpoint, UtilityService} from "../shared/_services/utility.service";
import {EVENTS, MessageHubService} from "../_services/message-hub.service";
import {CoverUpdateEvent} from "../_models/events/cover-update-event";
import {ChapterRemovedEvent} from "../_models/events/chapter-removed-event";
import {Action, ActionFactoryService, ActionItem} from "../_services/action-factory.service";
import {Device} from "../_models/device/device";
import {ActionService} from "../_services/action.service";
import {DefaultDatePipe} from "../_pipes/default-date.pipe";
import {CoverImageComponent} from "../_single-module/cover-image/cover-image.component";
import {DefaultModalOptions} from "../_models/default-modal-options";

enum TabID {
  Related = 'related-tab',
  Reviews = 'review-tab', // Only applicable for books
  Details = 'details-tab'
}

@Component({
  selector: 'app-chapter-detail',
  standalone: true,
    imports: [
        AsyncPipe,
        CardActionablesComponent,
        LoadingComponent,
        NgbDropdown,
        NgbDropdownItem,
        NgbDropdownMenu,
        NgbDropdownToggle,
        NgbNav,
        NgbNavContent,
        NgbNavLink,
        NgbTooltip,
        VirtualScrollerModule,
        NgStyle,
        NgClass,
        TranslocoDirective,
        ReadMoreComponent,
        NgbNavItem,
        NgbNavOutlet,
        DetailsTabComponent,
        RouterLink,
        EntityTitleComponent,
        RelatedTabComponent,
        BadgeExpanderComponent,
        MetadataDetailRowComponent,
        DownloadButtonComponent,
        DatePipe,
        DefaultDatePipe,
        CoverImageComponent
    ],
  templateUrl: './chapter-detail.component.html',
  styleUrl: './chapter-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterDetailComponent implements OnInit {

  private readonly document = inject(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly imageService = inject(ImageService);
  private readonly chapterService = inject(ChapterService);
  private readonly seriesService = inject(SeriesService);
  private readonly libraryService = inject(LibraryService);
  private readonly themeService = inject(ThemeService);
  private readonly downloadService = inject(DownloadService);
  private readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly toastr = inject(ToastrService);
  private readonly readerService = inject(ReaderService);
  protected readonly accountService = inject(AccountService);
  private readonly modalService = inject(NgbModal);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly readingListService = inject(ReadingListService);
  protected readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly location = inject(Location);

  protected readonly AgeRating = AgeRating;
  protected readonly TabID = TabID;
  protected readonly FilterField = FilterField;
  protected readonly Breakpoint = Breakpoint;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  isLoading: boolean = true;
  coverImage: string = '';
  chapterId: number = 0;
  seriesId: number = 0;
  libraryId: number = 0;
  chapter: Chapter | null = null;
  series: Series | null = null;
  libraryType: LibraryType | null = null;
  hasReadingProgress = false;
  weblinks: Array<string> = [];
  activeTabId = TabID.Details;
  /**
   * This is the download we get from download service.
   */
  download$: Observable<DownloadEvent | null> | null = null;
  downloadInProgress: boolean = false;
  readingLists: ReadingList[] = [];
  showDetailsTab: boolean = true;
  mobileSeriesImgBackground: string | undefined;
  chapterActions: Array<ActionItem<Chapter>> = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this));


  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar?.nativeElement.offsetHeight || 0;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }


  ngOnInit() {
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');
    if (seriesId === null || libraryId === null || chapterId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.mobileSeriesImgBackground = getComputedStyle(document.documentElement)
      .getPropertyValue('--mobile-series-img-background').trim();
    this.seriesId = parseInt(seriesId, 10);
    this.chapterId = parseInt(chapterId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.coverImage = this.imageService.getChapterCoverImage(this.chapterId);

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.event === EVENTS.CoverUpdate) {
        const coverUpdateEvent = event.payload as CoverUpdateEvent;
        if (coverUpdateEvent.entityType === 'chapter' && coverUpdateEvent.id === this.chapterId) {
          this.themeService.refreshColorScape('chapter', coverUpdateEvent.id).subscribe();
        }
      } else if (event.event === EVENTS.ChapterRemoved) {
        const removedEvent = event.payload as ChapterRemovedEvent;
        if (removedEvent.chapterId !== this.chapterId) return;

        // This series has been deleted from disk, redirect to series
        this.router.navigate(['library', this.libraryId, 'series', this.seriesId]);
      }
    });


    forkJoin({
      series: this.seriesService.getSeries(this.seriesId),
      chapter: this.chapterService.getChapterMetadata(this.chapterId),
      libraryType: this.libraryService.getLibraryType(this.libraryId)
    }).subscribe(results => {

      if (results.chapter === null) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.series = results.series;
      this.chapter = results.chapter;
      this.weblinks = this.chapter.webLinks.split(',');
      this.libraryType = results.libraryType;

      this.themeService.setColorScape(this.chapter.primaryColor, this.chapter.secondaryColor);

      // Set up the download in progress
      this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
        return this.downloadService.mapToEntityType(events, this.chapter!);
      }));

      this.readingListService.getReadingListsForChapter(this.chapterId).subscribe(lists => {
        this.readingLists = lists;
        this.cdRef.markForCheck();
      });

      this.route.fragment.pipe(tap(frag => {
        if (frag !== null && this.activeTabId !== (frag as TabID)) {
          this.activeTabId = frag as TabID;
          this.updateUrl(this.activeTabId);
          this.cdRef.markForCheck();
        }
      }), takeUntilDestroyed(this.destroyRef)).subscribe();

      this.showDetailsTab = hasAnyCast(this.chapter) || (this.chapter.genres || []).length > 0 ||
        (this.chapter.tags || []).length > 0 || this.chapter.webLinks.length > 0;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });

    this.cdRef.markForCheck();
  }

  loadData() {
    this.chapterService.getChapterMetadata(this.chapterId).subscribe(d => {
      if (d === null) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.chapter = d;
      this.cdRef.markForCheck();
    })
  }

  read(incognitoMode: boolean = false) {
    if (this.bulkSelectionService.hasSelections()) return;
    if (this.chapter === null) return;

    if (this.chapter.pages === 0) {
      this.toastr.error(translate('series-detail.no-pages'));
      return;
    }
    this.router.navigate(this.readerService.getNavigationArray(this.series?.libraryId!, this.seriesId, this.chapter.id, this.chapter.files[0].format),
      {queryParams: {incognitoMode}});
  }

  openEditModal() {
    const ref = this.modalService.open(EditChapterModalComponent, DefaultModalOptions);
    ref.componentInstance.chapter = this.chapter;
    ref.componentInstance.libraryType = this.libraryType;
    ref.componentInstance.libraryId = this.libraryId;
    ref.componentInstance.seriesId = this.series!.id;

    ref.closed.subscribe(res => {
      this.loadData();
    });
  }

  onNavChange(event: NgbNavChangeEvent) {
    this.bulkSelectionService.deselectAll();
    this.updateUrl(event.nextId);
    this.cdRef.markForCheck();
  }

  updateUrl(activeTab: TabID) {
    const tokens = this.location.path().split('#');
    const newUrl = `${tokens[0]}#${activeTab}`;
    this.location.replaceState(newUrl)
  }

  openPerson(field: FilterField, value: number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }

  downloadChapter() {
    if (this.downloadInProgress) return;
    this.downloadService.download('chapter', this.chapter!, (d) => {
      this.downloadInProgress = !!d;
      this.cdRef.markForCheck();
    });
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }

  switchTabsToDetail() {
    this.activeTabId = TabID.Details;
    this.cdRef.markForCheck();
  }

  performAction(action: ActionItem<Chapter>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.chapter!);
    }
  }

  handleChapterActionCallback(action: ActionItem<Chapter>, chapter: Chapter) {
    switch (action.action) {
      case(Action.MarkAsRead):
        this.actionService.markChapterAsRead(this.libraryId, this.seriesId, chapter, () => {
          this.loadData();
        });
        break;
      case(Action.MarkAsUnread):
        this.actionService.markChapterAsUnread(this.libraryId, this.seriesId, chapter, () => {
          this.loadData();
        });
        break;
      case(Action.Edit):
        this.openEditModal();
        break;
      case(Action.AddToReadingList):
        this.actionService.addChapterToReadingList(chapter, this.seriesId, () => {/* No Operation */ });
        break;
      case(Action.IncognitoRead):
        this.readerService.readChapter(this.libraryId, this.seriesId, chapter, true);
        break;
      case (Action.SendTo):
        const device = (action._extra!.data as Device);
        this.actionService.sendToDevice([chapter.id], device);
        break;
      case Action.Download:
        this.downloadChapter();
        break;
      case Action.Delete:
        this.router.navigate(['library', this.libraryId, 'series', this.seriesId]);
        break;
    }
  }

  protected readonly LibraryType = LibraryType;
    protected readonly encodeURIComponent = encodeURIComponent;
}
