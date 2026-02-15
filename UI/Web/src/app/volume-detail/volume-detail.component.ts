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
  ViewChild
} from '@angular/core';
import {DOCUMENT, Location, NgClass, NgStyle} from "@angular/common";
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {ImageService} from "../_services/image.service";
import {SeriesService} from "../_services/series.service";
import {LibraryService} from "../_services/library.service";
import {ThemeService} from "../_services/theme.service";
import {DownloadEvent, DownloadService} from "../shared/_services/download.service";
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
import {map, Observable, tap} from "rxjs";
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
import {ChapterCardComponent} from "../cards/chapter-card/chapter-card.component";
import {EditVolumeModalComponent} from "../_single-module/edit-volume-modal/edit-volume-modal.component";
import {Genre} from "../_models/metadata/genre";
import {Tag} from "../_models/tag";
import {RelatedTabChangeEvent, RelatedTabComponent} from "../_single-module/related-tab/related-tab.component";
import {ReadingList} from "../_models/reading-list";
import {ReadingListService} from "../_services/reading-list.service";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {
  MetadataDetailRowComponent
} from "../series-detail/_components/metadata-detail-row/metadata-detail-row.component";
import {DownloadButtonComponent} from "../series-detail/_components/download-button/download-button.component";
import {EVENTS, MessageHubService} from "../_services/message-hub.service";
import {CoverUpdateEvent} from "../_models/events/cover-update-event";
import {ChapterRemovedEvent} from "../_models/events/chapter-removed-event";
import {ActionService} from "../_services/action.service";
import {VolumeRemovedEvent} from "../_models/events/volume-removed-event";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {EditChapterModalComponent} from "../_single-module/edit-chapter-modal/edit-chapter-modal.component";
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

enum TabID {
  Chapters = 'chapters-tab',
  Related = 'related-tab',
  Reviews = 'reviews-tab', // Only applicable for books
  Details = 'details-tab',
  Annotations = 'annotations-tab'
}

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
    ChapterCardComponent,
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
    ReadingProgressIconPipePipe
  ],
  templateUrl: './volume-detail.component.html',
  styleUrl: './volume-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VolumeDetailComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly imageService = inject(ImageService);
  private readonly volumeService = inject(VolumeService);
  private readonly seriesService = inject(SeriesService);
  private readonly libraryService = inject(LibraryService);
  private readonly themeService = inject(ThemeService);
  private readonly downloadService = inject(DownloadService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly readerService = inject(ReaderService);
  protected readonly accountService = inject(AccountService);
  private readonly modalService = inject(ModalService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  protected readonly utilityService = inject(UtilityService);
  private readonly readingListService = inject(ReadingListService);
  private readonly messageHub = inject(MessageHubService);
  private readonly location = inject(Location);
  private readonly chapterService = inject(ChapterService);
  private readonly annotationService = inject(AnnotationService);
  protected readonly breakpointService = inject(BreakpointService);

  protected readonly AgeRating = AgeRating;
  protected readonly TabID = TabID;
  protected readonly FilterField = FilterField;
  protected readonly encodeURIComponent = encodeURIComponent;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;


  seriesId = input(0, {transform: numberAttribute });
  libraryId = input(0, {transform: numberAttribute });
  volumeId = input(0, {transform: numberAttribute });

  volume = getWritableResolvedData(this.route, 'volume');
  series = getResolvedData(this.route, 'series');
  library = getResolvedData(this.route, 'library');
  libraryType = computed(() => this.library().type);

  coverImage = computed(() => this.imageService.getVolumeCoverImage(this.volume().id));

  isLoading: boolean = true;

  activeTabId = TabID.Chapters;
  readingLists: ReadingList[] = [];

  // Only populated if the volume has exactly one chapter
  userReviews: Array<UserReview> = [];
  plusReviews: Array<UserReview> = [];
  rating: number = 0;
  hasBeenRated: boolean = false;
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

  volumeActions: Array<ActionItem<Volume>> = [];
  chapterActions: Array<ActionItem<Chapter>> = [];

  /**
   * This is the download we get from download service.
   */
  download$: Observable<DownloadEvent | null> | null = null;

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

  maxAgeRating: AgeRating = AgeRating.Unknown;
  volumeCast: VolumeCast = {
    characterLocked: false,
    characters: [],
    coloristLocked: false,
    colorists: [],
    coverArtistLocked: false,
    coverArtists: [],
    editorLocked: false,
    editors: [],
    imprintLocked: false,
    imprints: [],
    inkerLocked: false,
    inkers: [],
    languageLocked: false,
    lettererLocked: false,
    letterers: [],
    locationLocked: false,
    locations: [],
    pencillerLocked: false,
    pencillers: [],
    publisherLocked: false,
    publishers: [],
    teamLocked: false,
    teams: [],
    translatorLocked: false,
    translators: [],
    writerLocked: false,
    writers: []
  };
  tags: Array<Tag> = [];
  genres: Array<Genre> = [];



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
    const volumeId = this.route.snapshot.paramMap.get('volumeId');
    if (seriesId === null || libraryId === null || volumeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

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


    this.volumeActions = this.actionFactoryService.getVolumeActions(this.seriesId(), this.libraryId(), this.libraryType(), this.shouldRenderVolumeAction.bind(this));
    this.chapterActions = this.actionFactoryService.getChapterActions(this.seriesId(), this.libraryId(), this.libraryType());


    if (this.volume().chapters.length === 1) {
      this.chapterService.chapterDetailPlus(this.seriesId(), this.volume().chapters[0].id).subscribe(detail => {
        this.userReviews = detail.reviews.filter(r => !r.isExternal);
        this.plusReviews = detail.reviews.filter(r => r.isExternal);
        this.rating = detail.rating;
        this.hasBeenRated = detail.hasBeenRated;
      });

      this.annotationService.getAllAnnotations(this.volume().chapters[0].id).subscribe(annotations => {
        this.annotations.set(annotations);
      });

    }

    this.themeService.setColorScape(this.volume()!.primaryColor, this.volume()!.secondaryColor);

    // Set up the download in progress
    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
      return this.downloadService.mapToEntityType(events, this.volume()!);
    }));

    this.route.fragment.pipe(tap(frag => {
      if (frag !== null && this.activeTabId !== (frag as TabID)) {
        this.activeTabId = frag as TabID;
        this.updateUrl(this.activeTabId);
        this.cdRef.markForCheck();
      }
    }), takeUntilDestroyed(this.destroyRef)).subscribe();


    this.loadReadingLists();

    // Calculate all the writes/artists for all chapters
    this.volumeCast.writers = this.volume().chapters
      .flatMap(c => c.writers)  // Flatten the array of writers from all chapters
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name) // Check for distinct names
      );

    this.volumeCast.coverArtists = this.volume().chapters
      .flatMap(c => c.coverArtists)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );

    this.volumeCast.characters = this.volume().chapters
      .flatMap(c => c.characters)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );
    this.volumeCast.colorists = this.volume().chapters
      .flatMap(c => c.colorists)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );
    this.volumeCast.editors = this.volume().chapters
      .flatMap(c => c.editors)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );
    this.volumeCast.imprints = this.volume().chapters
      .flatMap(c => c.imprints)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );
    this.volumeCast.inkers = this.volume().chapters
      .flatMap(c => c.inkers)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );
    this.volumeCast.letterers = this.volume().chapters
      .flatMap(c => c.letterers)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );
    this.volumeCast.locations = this.volume().chapters
      .flatMap(c => c.locations)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );

    this.volumeCast.teams = this.volume().chapters
      .flatMap(c => c.teams)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );

    this.volumeCast.translators = this.volume().chapters
      .flatMap(c => c.translators)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );

    this.volumeCast.publishers = this.volume().chapters
      .flatMap(c => c.publishers)
      .filter((person, index, self) =>
        index === self.findIndex(w => w.name === person.name)
      );

    this.genres = this.volume().chapters
      .flatMap(c => c.genres)
      .filter((tag, index, self) =>
        index === self.findIndex(w => w.title === tag.title)
      );

    this.tags = this.volume().chapters
      .flatMap(c => c.tags)
      .filter((tag, index, self) =>
        index === self.findIndex(w => w.title === tag.title)
      );

    this.maxAgeRating = Math.max(
      ...this.volume().chapters
        .flatMap(c => c.ageRating)
    );


    this.isLoading = false;
    this.cdRef.markForCheck();
  }

  private loadReadingLists(switchTabsIfNoList = false) {
    // TODO: Why can't we have a bulk flow for this?
    const volume = this.volume();
    if (!volume) return;

    if (volume.chapters.length === 1) {
      this.readingListService.getReadingListsForChapter(volume.chapters[0].id).subscribe(lists => {
        this.readingLists = lists;
        if (switchTabsIfNoList && lists.length === 0) {
          this.switchTabsToDetail();
        }
        this.cdRef.markForCheck();
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

    ref.closed.subscribe((res: ModalResult<Volume>) => {
      if (res.success && res.data) {
        this.volume.set({...res.data});
      }
    });
  }

  openEditChapterModal(chapter: Chapter) {
    const ref = this.modalService.open(EditChapterModalComponent);
    ref.componentInstance.chapter = chapter;
    ref.componentInstance.libraryType = this.libraryType();
    ref.componentInstance.libraryId = this.libraryId();
    ref.componentInstance.seriesId = this.seriesId();

    ref.closed.subscribe((res: ModalResult<Volume>) => {
      if (res.success && res.data) {
        this.volume.set({...res.data});
      }
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

  handleRelatedReload(event: RelatedTabChangeEvent) {
    if (event.entity === 'readingList') {
      this.loadReadingLists(true);
    }
  }

  shouldRenderVolumeAction(action: ActionItem<Volume>, entity: Volume, user: User) {
    switch (action.action) {
      case(Action.MarkAsRead):
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
    this.activeTabId = TabID.Details;
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

    const originalEntity = volume.chapters.find(s => s.id == updatedChapter.id);

    if (originalEntity) {
      Object.assign(originalEntity, updatedChapter);
      volume.chapters = [...volume.chapters];
      this.cdRef.markForCheck();
    }
  }

  protected readonly Breakpoint = Breakpoint;
}
