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
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {ImageService} from "../_services/image.service";
import {ThemeService} from "../_services/theme.service";
import {BulkSelectionService} from "../cards/bulk-selection.service";
import {ReaderService} from "../_services/reader.service";
import {AccountService} from "../_services/account.service";
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
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {Chapter, LooseLeafOrDefaultNumber} from "../_models/chapter";
import {LibraryType} from "../_models/library/library";
import {filter, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterField} from '../_models/metadata/v2/filter-field';
import {AgeRating} from '../_models/metadata/age-rating';
import {Volume} from "../_models/volume";
import {VolumeService} from "../_services/volume.service";
import {LoadingComponent} from "../shared/loading/loading.component";
import {DetailsTabComponent} from "../_single-module/details-tab/details-tab.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {Person} from "../_models/metadata/person";
import {IHasCast} from "../_models/common/i-has-cast";
import {EntityTitleComponent} from "../cards/entity-title/entity-title.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {UtilityService} from "../shared/_services/utility.service";
import {EditVolumeModalComponent} from "../_single-module/edit-volume-modal/edit-volume-modal.component";
import {RelatedTabChangeEvent, RelatedTabComponent} from "../_single-module/related-tab/related-tab.component";
import {ReadingList} from "../_models/reading-list/reading-list";
import {ReadingListService} from "../_services/reading-list.service";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {
  MetadataDetailRowComponent
} from "../series-detail/_components/metadata-detail-row/metadata-detail-row.component";
import {DownloadButtonComponent} from "../series-detail/_components/download-button/download-button.component";
import {EVENTS, MessageHubService} from "../_services/message-hub.service";
import {CoverUpdateEvent} from "../_models/events/cover-update-event";
import {ChapterRemovedEvent} from "../_models/events/chapter-removed-event";
import {VolumeRemovedEvent} from "../_models/events/volume-removed-event";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {CoverImageComponent} from "../_single-module/cover-image/cover-image.component";
import {UserReview} from "../_models/user-review";
import {ReviewsComponent} from "../_single-module/reviews/reviews.component";
import {ExternalRatingComponent} from "../series-detail/_components/external-rating/external-rating.component";
import {ChapterService} from "../_services/chapter.service";
import {User} from "../_models/user/user";
import {AnnotationService} from "../_services/annotation.service";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {AnnotationsTabComponent} from "../_single-module/annotations-tab/annotations-tab.component";
import {UtcToLocalDatePipe} from "../_pipes/utc-to-locale-date.pipe";
import {ReadingProgressStatus} from "../_models/series-detail/reading-progress";
import {ReadingProgressStatusPipePipe} from "../_pipes/reading-progress-status-pipe.pipe";
import {ReadingProgressIconPipePipe} from "../_pipes/reading-progress-icon-pipe.pipe";
import {Breakpoint, BreakpointService} from "../_services/breakpoint.service";
import {ActionFactoryService} from "../_services/action-factory.service";
import {ActionItem} from "../_models/actionables/action-item";
import {Action} from "../_models/actionables/action";
import {ModalService} from "../_services/modal.service";
import {getResolvedData, getWritableResolvedData} from "../../libs/route-util";
import {ModalResult} from "../_models/modal/modal-result";
import {ChapterCardComponent} from "../cards/chapter-card/chapter-card.component";
import {Tabs} from "../_models/tabs";
import {TabTitlePipe} from "../_pipes/tab-title.pipe";
import {EntityTitleService} from "../_services/entity-title.service";

interface VolumeCast extends IHasCast {
  characterLocked: boolean;
  characters: Array<Person>;
  coloristLocked: boolean;
  colorists: Array<Person>;
  coverArtistLocked: boolean;
  coverArtists: Array<Person>;
  editorLocked: boolean;
  editors: Array<Person>;
  imprintLocked: boolean;
  imprints: Array<Person>;
  inkerLocked: boolean;
  inkers: Array<Person>;
  languageLocked: boolean;
  lettererLocked: boolean;
  letterers: Array<Person>;
  locationLocked: boolean;
  locations: Array<Person>;
  pencillerLocked: boolean;
  pencillers: Array<Person>;
  publisherLocked: boolean;
  publishers: Array<Person>;
  teamLocked: boolean;
  teams: Array<Person>;
  translatorLocked: boolean;
  translators: Array<Person>;
  writerLocked: boolean;
  writers: Array<Person>;
}

@Component({
  selector: 'app-volume-detail',
  imports: [
    LoadingComponent,
    NgbNavOutlet,
    DetailsTabComponent,
    NgbNavItem,
    NgbNavLink,
    NgbNavContent,
    NgbNav,
    ReadMoreComponent,
    NgbDropdownItem,
    NgbDropdownMenu,
    NgbDropdown,
    NgbDropdownToggle,
    EntityTitleComponent,
    RouterLink,
    NgbTooltip,
    NgStyle,
    NgClass,
    TranslocoDirective,
    VirtualScrollerModule,
    RelatedTabComponent,
    BadgeExpanderComponent,
    MetadataDetailRowComponent,
    DownloadButtonComponent,
    CardActionablesComponent,
    BulkOperationsComponent,
    CoverImageComponent,
    ReviewsComponent,
    ExternalRatingComponent,
    AnnotationsTabComponent,
    UtcToLocalDatePipe,
    ReadingProgressStatusPipePipe,
    ReadingProgressIconPipePipe,
    ChapterCardComponent,
    TabTitlePipe
  ],
  templateUrl: './volume-detail.component.html',
  styleUrl: './volume-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VolumeDetailComponent implements OnInit {
  protected readonly DownloadEntityType = DownloadEntityType;
  private readonly document = inject(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly imageService = inject(ImageService);
  private readonly volumeService = inject(VolumeService);
  private readonly themeService = inject(ThemeService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly readerService = inject(ReaderService);
  protected readonly accountService = inject(AccountService);
  private readonly modalService = inject(ModalService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  protected readonly utilityService = inject(UtilityService);
  private readonly readingListService = inject(ReadingListService);
  private readonly messageHub = inject(MessageHubService);
  private readonly location = inject(Location);
  private readonly chapterService = inject(ChapterService);
  private readonly annotationService = inject(AnnotationService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly entityTitleService = inject(EntityTitleService);

  readonly scrollingBlock = viewChild<ElementRef<HTMLDivElement>>('scrollingBlock');


  seriesId = input(0, {transform: numberAttribute });
  libraryId = input(0, {transform: numberAttribute });
  volumeId = input(0, {transform: numberAttribute });

  volume = getWritableResolvedData(this.route, 'volume');
  series = getResolvedData(this.route, 'series');
  library = getResolvedData(this.route, 'library');
  libraryType = computed(() => this.library().type);

  coverImage = computed(() => this.imageService.getVolumeCoverImage(this.volume().id));

  isLoading = signal(true);

  activeTabId = Tabs.Chapters;
  readingLists = signal<ReadingList[]>([]);

  // Only populated if the volume has exactly one chapter
  userReviews = signal<UserReview[]>([]);
  plusReviews = signal<UserReview[]>([]);
  rating = signal(0);
  hasBeenRated = signal(false);
  annotations = signal<Annotation[]>([]);
  totalReads = computed(() => {
    const chapters = this.volume()?.chapters || [];
    if (chapters.length === 0) return 0;

    return chapters.reduce((min, curr) => Math.min(min, curr.totalReads), Infinity);
  });
  files = computed(() => {
    const chapters = this.volume()?.chapters || [];
    return chapters.flatMap(c => c.files);
  });
  size = computed(() => {
    return this.volume().chapters.reduce((sum, c) =>
      sum + c.files.reduce((fileSum, f) => fileSum + f.bytes, 0), 0);
  });


  readingProgressStatus = computed(() => {
    if (this.volume().pagesRead > 0 && this.volume().pagesRead < this.volume().pages) {
      return ReadingProgressStatus.Progress;
    } else if (this.volume().pagesRead >= this.volume().pages) {
      return ReadingProgressStatus.FullyRead;
    }
    return ReadingProgressStatus.NoProgress;
  });

  mobileSeriesImgBackground: string | undefined;

  volumeActions = computed(() => this.actionFactoryService.getVolumeActions(this.seriesId(), this.libraryId(), this.libraryType(), this.shouldRenderVolumeAction.bind(this)));

  chapters = computed(() => this.volume()?.chapters || []);


  currentlyReadingChapter = computed(() => {
    const chaptersWithProgress = this.volume().chapters.filter(c => c.pagesRead < c.pages);
    if (chaptersWithProgress.length > 0 && this.volume().chapters.length > 1) {
      return chaptersWithProgress[0];
    } else {
      return null;
    }
  });

  continuePoint = computed(() => {
    const libraryType = this.libraryType();
    const currentlyReadingChapter = this.currentlyReadingChapter();
    const hasOneChapter = this.volume().chapters.length <= 1;

    if (currentlyReadingChapter === null || hasOneChapter) return '';

    if (currentlyReadingChapter.isSpecial) {
      return currentlyReadingChapter.title;
    }

    let chapterLocaleKey = 'common.chapter-num-shorthand';
    switch (libraryType) {
      case LibraryType.ComicVine:
      case LibraryType.Comic:
        chapterLocaleKey = 'common.issue-num-shorthand';
        break;
      case LibraryType.Book:
      case LibraryType.LightNovel:
        chapterLocaleKey = 'common.book-num-shorthand';
        break;
      case LibraryType.Manga:
      case LibraryType.Images:
        chapterLocaleKey = 'common.chapter-num-shorthand';
        break;
    }

    if (currentlyReadingChapter.minNumber === LooseLeafOrDefaultNumber) {
      return translate(chapterLocaleKey, {num: this.volume().chapters[0].minNumber});
    }

    return translate(chapterLocaleKey, {num: currentlyReadingChapter.minNumber});
  })

  volumeCast = computed<VolumeCast>(() => {
    const chapters = this.volume()?.chapters || [];
    return {
      characterLocked: false, characters: this.distinctPersons(chapters, c => c.characters),
      coloristLocked: false, colorists: this.distinctPersons(chapters, c => c.colorists),
      coverArtistLocked: false, coverArtists: this.distinctPersons(chapters, c => c.coverArtists),
      editorLocked: false, editors: this.distinctPersons(chapters, c => c.editors),
      imprintLocked: false, imprints: this.distinctPersons(chapters, c => c.imprints),
      inkerLocked: false, inkers: this.distinctPersons(chapters, c => c.inkers),
      languageLocked: false, lettererLocked: false,
      letterers: this.distinctPersons(chapters, c => c.letterers),
      locationLocked: false, locations: this.distinctPersons(chapters, c => c.locations),
      pencillerLocked: false, pencillers: this.distinctPersons(chapters, c => c.pencillers),
      publisherLocked: false, publishers: this.distinctPersons(chapters, c => c.publishers),
      teamLocked: false, teams: this.distinctPersons(chapters, c => c.teams),
      translatorLocked: false, translators: this.distinctPersons(chapters, c => c.translators),
      writerLocked: false, writers: this.distinctPersons(chapters, c => c.writers),
    };
  });

  genres = computed(() => (this.volume()?.chapters || [])
    .flatMap(c => c.genres)
    .filter((tag, i, self) => i === self.findIndex(w => w.title === tag.title)));

  tags = computed(() => (this.volume()?.chapters || [])
    .flatMap(c => c.tags)
    .filter((tag, i, self) => i === self.findIndex(w => w.title === tag.title)));

  maxAgeRating = computed(() => {
    const chapters = this.volume()?.chapters || [];
    if (chapters.length === 0) return AgeRating.Unknown;
    return Math.max(...chapters.map(c => c.ageRating));
  });

  chapterTabName = computed(() => this.entityTitleService.formatChapterName(this.libraryType(), true));
  reviewCount = computed(() => this.userReviews().length + this.plusReviews().length);



  get ScrollingBlockHeight() {
    if (this.scrollingBlock() === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const navbarHeight = navbar.offsetHeight;
    const totalHeight = navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }


  ngOnInit() {
    this.mobileSeriesImgBackground = getComputedStyle(document.documentElement)
      .getPropertyValue('--mobile-series-img-background').trim();

    this.bulkSelectionService.registerDataSource('chapter', () => this.volume()?.chapters ?? []);
    this.bulkSelectionService.registerPostAction(res => {
      if (res.effect === 'none') return;
      this.loadVolume();
    });
    this.bulkSelectionService.registerContext(() => ({seriesId: this.seriesId(), libraryId: this.libraryId(), libraryType: this.libraryType()}));


    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.event === EVENTS.CoverUpdate) {
        const coverUpdateEvent = event.payload as CoverUpdateEvent;
        if (coverUpdateEvent.entityType === 'volume' && coverUpdateEvent.id === this.volumeId()) {
          this.themeService.refreshColorScape('volume', coverUpdateEvent.id).subscribe();
        }
      } else if (event.event === EVENTS.ChapterRemoved) {
        const removedEvent = event.payload as ChapterRemovedEvent;
        if (removedEvent.seriesId !== this.seriesId()) return;

        // remove the chapter from the tab
        if (this.volume()) {
          const chapters = [...this.volume().chapters.filter(c => c.id !== removedEvent.chapterId)];
          this.volume.set({...this.volume(), chapters: chapters});
        }
      } else if (event.event === EVENTS.VolumeRemoved) {
        const removedEvent = event.payload as VolumeRemovedEvent;
        if (removedEvent.volumeId !== this.volumeId()) return;

        // remove the chapter from the tab
        this.navigateToSeries();
      }
    });

    if (this.volume().chapters.length === 1) {
      this.chapterService.chapterDetailPlus(this.seriesId(), this.volume().chapters[0].id).subscribe(detail => {
        this.userReviews.set(detail.reviews.filter(r => !r.isExternal));
        this.plusReviews.set(detail.reviews.filter(r => r.isExternal));
        this.rating.set(detail.rating);
        this.hasBeenRated.set(detail.hasBeenRated);
      });

      this.annotationService.getAllAnnotations(this.volume().chapters[0].id).subscribe(annotations => {
        this.annotations.set(annotations);
      });

    }

    this.themeService.setColorScape(this.volume()!.primaryColor, this.volume()!.secondaryColor);

    this.route.fragment.pipe(tap(frag => {
      if (frag !== null && this.activeTabId !== (frag as Tabs)) {
        this.activeTabId = frag as Tabs;
        this.updateUrl(this.activeTabId);
        this.cdRef.markForCheck();
      }
    }), takeUntilDestroyed(this.destroyRef)).subscribe();


    this.loadReadingLists();

    this.isLoading.set(false);
  }

  private distinctPersons(chapters: Chapter[], selector: (c: Chapter) => Person[]): Person[] {
    return chapters.flatMap(selector)
      .filter((person, i, self) => i === self.findIndex(w => w.name === person.name));
  }

  private loadReadingLists(switchTabsIfNoList = false) {
    const volume = this.volume();
    if (!volume) return;

    if (volume.chapters.length === 1) {
      this.readingListService.getReadingListsForChapter(volume.chapters[0].id).subscribe(lists => {
        this.readingLists.set(lists);
        if (switchTabsIfNoList && lists.length === 0) {
          this.switchTabsToDetail();
        }
      });
    }
  }

  loadVolume() {
    this.volumeService.getVolumeMetadata(this.volumeId()).subscribe(v => {
      this.volume.set({...v});
    });
  }

  readVolume(incognitoMode: boolean = false) {
    if (!this.volume) return;

    this.readerService.readVolume(this.libraryId(), this.seriesId(), this.volume(), incognitoMode);
  }

  openEditModal() {
    const ref = this.modalService.open(EditVolumeModalComponent);
    ref.componentInstance.volume = this.volume();
    ref.componentInstance.libraryType = this.libraryType();
    ref.componentInstance.libraryId = this.libraryId();
    ref.componentInstance.seriesId = this.seriesId();

    ref.closed.pipe(
      filter((res: ModalResult<Volume>) => res.success),
      tap(() => this.loadVolume())
    ).subscribe();
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

  handleRelatedReload(event: RelatedTabChangeEvent) {
    if (event.entity === 'readingList') {
      this.loadReadingLists(true);
    }
  }

  shouldRenderVolumeAction(action: ActionItem<Volume>, entity: Volume, _: User) {
    switch (action.action) {
      case(Action.MarkAsRead):
      case(Action.MarkAsReadWithSession):
        return entity.pagesRead < entity.pages;
      case(Action.MarkAsUnread):
        return entity.pagesRead !== 0;
      default:
        return true;
    }
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }


  switchTabsToDetail() {
    this.activeTabId = Tabs.Details;
    this.cdRef.markForCheck();
    setTimeout(() => {
      const tabElem = this.document.querySelector('#details-tab');
      if (tabElem) {
        (tabElem as HTMLLIElement).scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
      }
    }, 10);
  }

  navigateToSeries() {
    this.router.navigate(['library', this.libraryId(), 'series', this.seriesId()]);
  }


  updateChapter(updatedChapter: Chapter) {
    const volume = this.volume();
    if (!volume) return;
    const idx = volume.chapters.findIndex(c => c.id === updatedChapter.id);
    if (idx >= 0) {
      const chapters = [...volume.chapters];
      chapters[idx] = {...updatedChapter};
      this.volume.set({...volume, chapters});
    }
  }

  protected readonly Breakpoint = Breakpoint;
  protected readonly AgeRating = AgeRating;
  protected readonly Tabs = Tabs;
  protected readonly FilterField = FilterField;
  protected readonly encodeURIComponent = encodeURIComponent;
}
