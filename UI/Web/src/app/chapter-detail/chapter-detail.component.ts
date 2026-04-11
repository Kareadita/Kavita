import {DownloadEntityType} from '../shared/_models/download-queue-item';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  numberAttribute,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {DOCUMENT, Location, NgClass, NgStyle} from "@angular/common";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {
  NgbDropdown,
  NgbDropdownItem,
  NgbDropdownMenu,
  NgbDropdownToggle,
  NgbNav,
  NgbNavChangeEvent,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {ImageService} from "../_services/image.service";
import {ChapterService} from "../_services/chapter.service";
import {tap} from "rxjs";
import {AgeRating} from "../_models/metadata/age-rating";
import {LibraryType} from "../_models/library/library";
import {ThemeService} from "../_services/theme.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {BulkSelectionService} from "../cards/bulk-selection.service";
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
import {ReadingList} from "../_models/reading-list/reading-list";
import {ReadingListService} from "../_services/reading-list.service";
import {RelatedTabChangeEvent, RelatedTabComponent} from "../_single-module/related-tab/related-tab.component";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {
  MetadataDetailRowComponent
} from "../series-detail/_components/metadata-detail-row/metadata-detail-row.component";
import {DownloadButtonComponent} from "../series-detail/_components/download-button/download-button.component";
import {hasAnyCast} from "../_models/common/i-has-cast";
import {EVENTS, MessageHubService} from "../_services/message-hub.service";
import {CoverUpdateEvent} from "../_models/events/cover-update-event";
import {ChapterRemovedEvent} from "../_models/events/chapter-removed-event";
import {DefaultDatePipe} from "../_pipes/default-date.pipe";
import {CoverImageComponent} from "../_single-module/cover-image/cover-image.component";
import {UserReview} from "../_models/user-review";
import {ReviewsComponent} from "../_single-module/reviews/reviews.component";
import {ExternalRatingComponent} from "../series-detail/_components/external-rating/external-rating.component";
import {Rating} from "../_models/rating";
import {AnnotationService} from "../_services/annotation.service";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {AnnotationsTabComponent} from "../_single-module/annotations-tab/annotations-tab.component";
import {UtcToLocalTimePipe} from "../_pipes/utc-to-local-time.pipe";
import {UtcToLocalDatePipe} from "../_pipes/utc-to-locale-date.pipe";
import {ReadingProgressStatus} from "../_models/series-detail/reading-progress";
import {ReadingProgressStatusPipePipe} from "../_pipes/reading-progress-status-pipe.pipe";
import {ReadingProgressIconPipePipe} from "../_pipes/reading-progress-icon-pipe.pipe";
import {BreakpointService} from "../_services/breakpoint.service";
import {ActionFactoryService} from "../_services/action-factory.service";
import {ModalService} from "../_services/modal.service";
import {getResolvedData, getWritableResolvedData} from "../../libs/route-util";
import {Tabs} from "../_models/tabs";
import {TabTitlePipe} from "../_pipes/tab-title.pipe";
import {NULL_DATE} from "../_pipes/date-year-range.pipe";

@Component({
  selector: 'app-chapter-detail',
  imports: [
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
    DefaultDatePipe,
    CoverImageComponent,
    ReviewsComponent,
    ExternalRatingComponent,
    AnnotationsTabComponent,
    UtcToLocalTimePipe,
    UtcToLocalDatePipe,
    ReadingProgressStatusPipePipe,
    ReadingProgressIconPipePipe,
    TabTitlePipe
  ],
  templateUrl: './chapter-detail.component.html',
  styleUrl: './chapter-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterDetailComponent implements OnInit {

  protected readonly DownloadEntityType = DownloadEntityType;
  private readonly document = inject(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly imageService = inject(ImageService);
  private readonly chapterService = inject(ChapterService);
  private readonly themeService = inject(ThemeService);
  private readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly readerService = inject(ReaderService);
  protected readonly accountService = inject(AccountService);
  private readonly modalService = inject(ModalService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly readingListService = inject(ReadingListService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly messageHub = inject(MessageHubService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly location = inject(Location);
  private readonly annotationService = inject(AnnotationService);

  readonly scrollingBlock = viewChild<ElementRef<HTMLDivElement>>('scrollingBlock');
  readonly companionBar = viewChild<ElementRef<HTMLDivElement>>('companionBar');

  seriesId = input(0, {transform: numberAttribute });
  libraryId = input(0, {transform: numberAttribute });
  chapterId = input(0, {transform: numberAttribute });

  isLoading = signal<boolean>(true);
  chapter = getWritableResolvedData(this.route, 'chapter');
  series = getResolvedData(this.route, 'series');
  library = getResolvedData(this.route, 'library');
  libraryType = computed(() => this.library().type);


  size = computed(() => {
    return (this.chapter()?.files || []).reduce((sum, f) => sum + f.bytes, 0);
  });
  annotations = signal<Annotation[]>([]);
  readingProgressStatus = computed(() => {
    const chapter = this.chapter();

    if (chapter.pagesRead > 0 && chapter.pagesRead < chapter.pages) {
      return ReadingProgressStatus.Progress;
    } else if (chapter.pagesRead >= chapter.pages) {
      return ReadingProgressStatus.FullyRead;
    }
    return ReadingProgressStatus.NoProgress;
  });

  coverImage = computed(() => this.imageService.getChapterCoverImage(this.chapterId()));
  weblinks = computed(() => {
    const chapter = this.chapter();
    return chapter.webLinks.length > 0 ? chapter.webLinks.split(',') : [];
  });
  userReviews = signal<UserReview[]>([]);
  plusReviews = signal<UserReview[]>([]);
  ratings = signal<Rating[]>([]);
  rating = signal<number>(0);
  hasBeenRated = signal<boolean>(false);
  readingLists = signal<ReadingList[]>([]);
  showDetailsTab = computed(() => {
    const chp = this.chapter();

    return hasAnyCast(chp) || (chp?.genres || []).length > 0 ||
      (chp?.tags || []).length > 0 || (chp?.webLinks || []).length > 0 || this.accountService.hasAdminRole();
  })
  mobileSeriesImgBackground = this.themeService.getCssVariable('--mobile-series-img-background');

  activeTabId = Tabs.Details;

  chapterActions = computed(() => this.actionFactoryService.getChapterActions(this.seriesId(), this.libraryId(), this.libraryType()));
  totalReviewCount = computed(() => this.userReviews().length + this.plusReviews().length);

  get ScrollingBlockHeight() {
    if (this.scrollingBlock() === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar()?.nativeElement.offsetHeight || 0;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }


  ngOnInit() {

    this.annotationService.getAllAnnotations(this.chapterId()).subscribe(annotations => {
      this.annotations.set(annotations);
    });

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.event === EVENTS.CoverUpdate) {
        const coverUpdateEvent = event.payload as CoverUpdateEvent;
        if (coverUpdateEvent.entityType === 'chapter' && coverUpdateEvent.id === this.chapterId()) {
          this.themeService.refreshColorScape('chapter', coverUpdateEvent.id).subscribe();
        }
      } else if (event.event === EVENTS.ChapterRemoved) {
        const removedEvent = event.payload as ChapterRemovedEvent;
        if (removedEvent.chapterId !== this.chapterId()) return;

        // This series has been deleted from disk, redirect to series
        this.router.navigate(['library', this.libraryId(), 'series', this.seriesId()]);
      }
    });


    this.chapterService.chapterDetailPlus(this.seriesId(), this.chapterId()).subscribe(chapterDetail => {

    this.userReviews.set(chapterDetail.reviews.filter(r => !r.isExternal));
    this.plusReviews.set(chapterDetail.reviews.filter(r => r.isExternal));
    this.rating.set(chapterDetail.rating);
    this.hasBeenRated.set(chapterDetail.hasBeenRated);
    this.ratings.set(chapterDetail.ratings);


    this.themeService.setColorScape(this.chapter().primaryColor, this.chapter().secondaryColor);

    this.loadReadingListsForChapter(this.chapterId());

    this.route.fragment.pipe(tap(frag => {
      if (frag !== null && this.activeTabId !== (frag as Tabs)) {
        this.activeTabId = frag as Tabs;
        this.updateUrl(this.activeTabId);
        this.cdRef.markForCheck();
      }
    }), takeUntilDestroyed(this.destroyRef)).subscribe();


    if (!this.showDetailsTab() && this.activeTabId === Tabs.Details) {
      this.activeTabId = Tabs.Reviews;
    }

    this.isLoading.set(false);
    this.cdRef.markForCheck();
  });

  this.cdRef.markForCheck();
  }

  loadReadingListsForChapter(chapterId: number) {
    this.readingListService.getReadingListsForChapter(chapterId).subscribe(lists => {
      this.readingLists.set(lists);
    });
  }

  loadData() {
    this.chapterService.getChapterMetadata(this.chapterId()).subscribe(d => {
      if (d === null) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.chapter.set(d);
    })
  }

  read(incognitoMode: boolean = false) {
    if (this.bulkSelectionService.hasSelections()) return;
    if (this.chapter()! === null) return;

    this.readerService.readChapter(this.libraryId(), this.seriesId(), this.chapter(), incognitoMode);
  }

  openEditModal() {
    const ref = this.modalService.open(EditChapterModalComponent);
    ref.componentInstance.chapter = this.chapter();
    ref.componentInstance.libraryType = this.libraryType();
    ref.componentInstance.libraryId = this.libraryId();
    ref.componentInstance.seriesId = this.seriesId();

    ref.closed.subscribe(res => {
      this.loadData();
    });
  }

  onNavChange(event: NgbNavChangeEvent) {
    this.bulkSelectionService.deselectAll();
    this.updateUrl(event.nextId);
    this.cdRef.markForCheck();
  }

  updateUrl(activeTab: Tabs) {
    const tokens = this.location.path().split('#');
    const newUrl = `${tokens[0]}#${activeTab}`;
    this.location.replaceState(newUrl)
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }

  switchTabsToDetail() {
    this.activeTabId = Tabs.Details;
    this.cdRef.markForCheck();
  }

  handleRelatedReload(event: RelatedTabChangeEvent) {
    if (event.entity === 'readingList') {
      this.loadReadingListsForChapter(this.chapterId());
    }
  }

  protected readonly AgeRating = AgeRating;
  protected readonly Tabs = Tabs;
  protected readonly FilterField = FilterField;
  protected readonly LibraryType = LibraryType;
  protected readonly encodeURIComponent = encodeURIComponent;
  protected readonly NULL_DATE = NULL_DATE;
}
