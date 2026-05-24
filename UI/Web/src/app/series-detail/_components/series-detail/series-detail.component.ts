import {DOCUMENT, Location, NgClass, NgStyle, NgTemplateOutlet} from '@angular/common';
import {DownloadEntityType} from '../../../shared/_models/download-queue-item';
import {
  AfterViewInit,
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
import {ReactiveFormsModule} from '@angular/forms';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
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
  NgbPopover,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {DrawerService} from '../../../_services/drawer.service';
import {KavitaplusDrawerComponent} from '../kavitaplus-drawer/kavitaplus-drawer.component';
import {KavitaplusTooltipComponent} from '../kavitaplus-tooltip/kavitaplus-tooltip.component';
import {ToastrService} from 'ngx-toastr';
import {catchError, debounceTime, EMPTY, of, ReplaySubject, tap} from 'rxjs';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {EditSeriesModalComponent} from 'src/app/cards/_modals/edit-series-modal/edit-series-modal.component';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {Chapter, LooseLeafOrDefaultNumber, SpecialVolumeNumber} from 'src/app/_models/chapter';
import {ScanSeriesEvent} from 'src/app/_models/events/scan-series-event';
import {SeriesRemovedEvent} from 'src/app/_models/events/series-removed-event';
import {LibraryType} from 'src/app/_models/library/library';
import {ReadingList} from 'src/app/_models/reading-list/reading-list';
import {Series} from 'src/app/_models/series';
import {RelatedSeries} from 'src/app/_models/series-detail/related-series';
import {RelationKind} from 'src/app/_models/series-detail/relation-kind';
import {SeriesMetadata} from 'src/app/_models/metadata/series-metadata';
import {Volume} from 'src/app/_models/volume';
import {AccountService} from 'src/app/_services/account.service';
import {ActionFactoryService} from 'src/app/_services/action-factory.service';
import {ActionService} from 'src/app/_services/action.service';
import {ImageService} from 'src/app/_services/image.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {NavService} from 'src/app/_services/nav.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {SeriesService} from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {UserReview} from "../../../_models/user-review";
import {ExternalSeriesCardComponent} from '../../../cards/external-series-card/external-series-card.component';
import {SeriesCardComponent} from '../../../cards/series-card/series-card.component';
import {VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {PublicationStatus} from "../../../_models/metadata/publication-status";
import {NextExpectedChapter} from "../../../_models/series-detail/next-expected-chapter";
import {NextExpectedCardComponent} from "../../../cards/next-expected-card/next-expected-card.component";
import {MetadataService} from "../../../_services/metadata.service";
import {Rating} from "../../../_models/rating";
import {ThemeService} from "../../../_services/theme.service";
import {BasicMetadataInfo, DetailsTabComponent} from "../../../_single-module/details-tab/details-tab.component";
import {ChapterRemovedEvent} from "../../../_models/events/chapter-removed-event";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {AgeRating} from "../../../_models/metadata/age-rating";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {ExternalRatingComponent} from "../external-rating/external-rating.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {ScrobblingService} from "../../../_services/scrobbling.service";
import {HourEstimateRange} from "../../../_models/series-detail/hour-estimate-range";
import {PublicationStatusPipe} from "../../../_pipes/publication-status.pipe";
import {MetadataDetailRowComponent} from "../metadata-detail-row/metadata-detail-row.component";
import {DownloadButtonComponent} from "../download-button/download-button.component";
import {hasAnyCast} from "../../../_models/common/i-has-cast";
import {CoverUpdateEvent} from "../../../_models/events/cover-update-event";
import {
  RelatedSeriesPair,
  RelatedTabChangeEvent,
  RelatedTabComponent
} from "../../../_single-module/related-tab/related-tab.component";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {UserCollection} from "../../../_models/collection-tag";
import {CoverImageComponent} from "../../../_single-module/cover-image/cover-image.component";
import {LicenseService} from "../../../_services/license.service";
import {PageBookmark} from "../../../_models/readers/page-bookmark";
import {VolumeRemovedEvent} from "../../../_models/events/volume-removed-event";
import {ReviewsComponent} from "../../../_single-module/reviews/reviews.component";
import {AnnotationsTabComponent} from "../../../_single-module/annotations-tab/annotations-tab.component";
import {Annotation} from "../../../book-reader/_models/annotations/annotation";
import {AnnotationService} from "../../../_services/annotation.service";
import {ReadingProgressStatus} from "../../../_models/series-detail/reading-progress";
import {ReadingProgressStatusPipePipe} from "../../../_pipes/reading-progress-status-pipe.pipe";
import {ReadingProgressIconPipePipe} from "../../../_pipes/reading-progress-icon-pipe.pipe";
import {Breakpoint, BreakpointService} from "../../../_services/breakpoint.service";
import {Action} from "../../../_models/actionables/action";
import {ActionResult} from "../../../_models/actionables/action-result";
import {CardEntityFactory, ChapterCardEntity, VolumeCardEntity} from "../../../_models/card/card-entity";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {EntityCardComponent} from "../../../cards/entity-card/entity-card.component";
import {ModalResult} from "../../../_models/modal/modal-result";
import {patchEntitySignal, patchSignalArray} from "../../../../libs/patch";
import {ModalService} from "../../../_services/modal.service";
import {getResolvedData, getWritableResolvedData} from "../../../../libs/route-util";
import {ExternalSeries} from "../../../_models/series-detail/external-series";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";
import {EntityTitleService} from "../../../_services/entity-title.service";
import {ReadingHistoryItem} from "src/app/_models/stats/reading-history-item";
import {StatisticsService} from "src/app/_services/statistics.service";
import {Pagination} from "src/app/_models/pagination";
import {ReadingHistoryViewerComponent} from "src/app/shared/reading-history-viewer/reading-history-viewer.component";
import {SeriesUpdateEvent} from "../../../_models/events/series-update-event";

interface StoryLineItem {
  chapter?: ChapterCardEntity;
  volume?: VolumeCardEntity;
  isChapter: boolean;
}

const READING_HISTORY_PAGE_SIZE = 10;

@Component({
  selector: 'app-series-detail',
  templateUrl: './series-detail.component.html',
  styleUrls: ['./series-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CardActionablesComponent, ReactiveFormsModule, NgStyle,
    NgbTooltip, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu,
    NgbDropdownItem, BulkOperationsComponent,
    NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, VirtualScrollerModule, SeriesCardComponent, ExternalSeriesCardComponent, NgbNavOutlet, NgbPopover, KavitaplusTooltipComponent,
    TranslocoDirective, NgTemplateOutlet, NextExpectedCardComponent,
    NgClass, DetailsTabComponent, DefaultValuePipe, ExternalRatingComponent, ReadMoreComponent, RouterLink, BadgeExpanderComponent,
    PublicationStatusPipe, MetadataDetailRowComponent, DownloadButtonComponent, RelatedTabComponent, CoverImageComponent, ReviewsComponent,
    AnnotationsTabComponent, ReadingProgressStatusPipePipe, ReadingProgressIconPipePipe, EntityCardComponent, TabTitlePipe, ReadingHistoryViewerComponent]
})
class SeriesDetailComponent implements OnInit, AfterViewInit {

  protected readonly DownloadEntityType = DownloadEntityType;
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly seriesService = inject(SeriesService);
  private readonly metadataService = inject(MetadataService);
  private readonly router = inject(Router);
  private readonly modalService = inject(ModalService);
  private readonly toastr = inject(ToastrService);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly messageHub = inject(MessageHubService);
  private readonly readingListService = inject(ReadingListService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrollService = inject(ScrollService);
  private readonly cardConfigFactory = inject(CardConfigFactory);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly imageService = inject(ImageService);
  protected readonly navService = inject(NavService);
  protected readonly readerService = inject(ReaderService);
  protected readonly themeService = inject(ThemeService);
  protected readonly annotationService = inject(AnnotationService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly scrobbleService = inject(ScrobblingService);
  private readonly location = inject(Location);
  private readonly document = inject(DOCUMENT);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly entityTitleService = inject(EntityTitleService);
  private readonly statisticsService = inject(StatisticsService);
  private readonly drawerService = inject(DrawerService);

  readonly scrollingBlock = viewChild<ElementRef<HTMLDivElement>>('scrollingBlock');


  seriesId = input(0, {transform: numberAttribute });
  libraryId = input(0, {transform: numberAttribute });
  /** This will be {id,type,name} only for non-admin users */
  library = getResolvedData(this.route, 'library');
  series = getWritableResolvedData(this.route, 'series');

  volumes = signal<Volume[]>([]);
  volumeEntities = computed(() => this.volumes().map(v => CardEntityFactory.volume(v, this.seriesId(), this.libraryId())));
  volumeConfig = computed(() => {
    const seriesId = this.seriesId();
    const libraryId = this.libraryId();
    const libraryType = this.libraryType();
    return this.cardConfigFactory.forVolume({seriesId, libraryId, libraryType})
  });
  chapters = signal<Chapter[]>([]);
  chapterEntities = computed(() => this.chapters().map(v => CardEntityFactory.chapter(v, this.seriesId(), this.libraryId())));
  chapterConfig = computed(() => {
    const seriesId = this.seriesId();
    const libraryId = this.libraryId();
    const libraryType = this.libraryType();
    return this.cardConfigFactory.forChapter({seriesId, libraryId, libraryType})
  });
  specials = signal<Chapter[]>([]);
  specialEntities = computed(() => this.specials().map(v => CardEntityFactory.chapter(v, this.seriesId(), this.libraryId())));
  specialConfig = computed(() => {
    const seriesId = this.seriesId();
    const libraryId = this.libraryId();
    const libraryType = this.libraryType();
    return this.cardConfigFactory.forChapter({seriesId, libraryId, libraryType, overrides: {selectionType: 'special'}})
  });
  storylineChapters = signal<Chapter[]>([]);
  private storylineChapterEntities = computed(() => this.storylineChapters().map(v => CardEntityFactory.chapter(v, this.seriesId(), this.libraryId())));
  storylineItems = computed(() => {
    const items: StoryLineItem[] = [];
    const volumes = this.volumeEntities();
    const storylineChapterEntities = this.storylineChapterEntities();

    const v = volumes.map(v => {
      return {volume: v, chapter: undefined, isChapter: false} as StoryLineItem;
    });
    items.push(...v);
    const c = storylineChapterEntities.map(c => {
      return {volume: undefined, chapter: c, isChapter: true} as StoryLineItem;
    });
    items.push(...c);

    return items;
  });

  protected readonly readingHistory = signal<ReadingHistoryItem[]>([]);
  protected readonly hasReadingHistory = computed(() => this.readingHistory().length > 0);
  protected readonly readingHistoryPagination = signal<Pagination | null>(null);
  protected readonly isLoadingReadingHistory = signal(false);
  protected readonly readingHistoryCurrentPage = signal(1);

  readonly isAdmin = this.accountService.hasAdminRole;

  activeTabId = Tabs.Storyline;
  mobileSeriesImgBackground = this.themeService.getCssVariable('--mobile-series-img-background');

  isLoading = signal<boolean>(true);
  isLoadingExtra = signal<boolean>(false);
  showLoader = computed(() => this.isLoading() || this.isLoadingExtra());

  libraryAllowsScrobbling  = signal<boolean>(false);
  isScrobbling = signal<boolean>(true);
  showScrobbleControls = computed(() => this.licenseService.hasActiveLicense() && this.libraryAllowsScrobbling());

  currentlyReadingChapter = signal<Chapter | null>(null);
  continueReadingTitle = computed(() => {
    const currentlyReadingChp = this.currentlyReadingChapter();
    if (currentlyReadingChp === null || !this.hasReadingProgress()) return '';

    if (!currentlyReadingChp.isSpecial) {
      const vol = this.volumes().filter(v => v.id === currentlyReadingChp.volumeId);

      let chapterLocaleKey = 'common.chapter-num-shorthand';
      let volumeLocaleKey = 'common.volume-num-shorthand';
      switch (this.libraryType()) {
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

      // This is a lone chapter
      if (vol.length === 0) {
        if (currentlyReadingChp.minNumber === LooseLeafOrDefaultNumber) {
          return currentlyReadingChp.titleName;
        }
        return translate(chapterLocaleKey, {num: currentlyReadingChp.minNumber});
      }

      if (currentlyReadingChp.minNumber === LooseLeafOrDefaultNumber) {
        return translate(volumeLocaleKey, {num: vol[0].minNumber});
      }

      return translate(volumeLocaleKey, {num: vol[0].minNumber})
        + ' ' + translate(chapterLocaleKey, {num: currentlyReadingChp.minNumber});
    }

    return currentlyReadingChp.title;
  });
  hasReadingProgress = signal<boolean>(false);
  readingProgressStatus = computed(() => {
    const hasProgress = this.hasReadingProgress();
    const series = this.series();

    if (!series || !hasProgress) return ReadingProgressStatus.NoProgress;

    if (series.pagesRead >= series.pages) {
      return ReadingProgressStatus.FullyRead;
    }

    return ReadingProgressStatus.Progress;
  });
  hasSpecials = computed(() => this.specials().length > 0);



  reviews = signal<UserReview[]>([]);
  plusReviews = signal<UserReview[]>([]);
  reviewCount = computed(() => this.reviews().length + this.plusReviews().length);
  bookmarks = signal<PageBookmark[]>([]);
  ratings = signal<Rating[]>([]);
  libraryType = computed(() => this.library().type);

  seriesMetadata = signal<SeriesMetadata | null>(null);
  readingLists = signal<ReadingList[]>([]);
  collections = signal<UserCollection[]>([]);
  isWantToRead = signal<boolean>(false);
  unreadCount = signal(0);
  totalCount = signal(0);
  seriesActions = computed(() => {
    const hasLicense = this.licenseService.hasActiveLicense();
    let actions = this.actionFactoryService.getSeriesActions()
      .filter(action => action.action !== Action.Edit);
    if (!hasLicense) {
      actions = actions.filter(action => action.action !== Action.Match);
    }
    return actions;
  });
  totalSize = computed(() => {
    const seen = new Set<number>();
    let total = 0;

    const addChapter = (c: Chapter) => {
      if (seen.has(c.id)) return;
      seen.add(c.id);
      for (const f of c.files) {
        total += f.bytes;
      }
    };

    for (const c of this.chapters()) addChapter(c);
    for (const v of this.volumes()) {
      for (const c of v.chapters) addChapter(c);
    }
    for (const c of this.specials()) addChapter(c);

    return total;
  });

  readingTimeLeft = signal<HourEstimateRange | null>(null);


  seriesCoverImage = computed(() => this.imageService.getSeriesCoverImage(this.seriesId()));
  chapterTabName = computed(() => this.entityTitleService.formatChapterName(this.libraryType(), true));
  nextExpectedChapter = signal<NextExpectedChapter | null>(null);
  loadPageSource = new ReplaySubject<boolean>(1);
  loadPage$ = this.loadPageSource.asObservable();

  readonly useBookLogic = computed(() => {
    const libType = this.libraryType();
    return libType === LibraryType.Book || libType === LibraryType.LightNovel;
  });

  readonly shouldShowStorylineTab = computed(() => {
    const libType = this.libraryType();
    const chapters = this.chapters();

    if (libType === LibraryType.ComicVine) return false;

    // Edge case for bad pdf parse
    if ((libType === LibraryType.Book || libType === LibraryType.LightNovel) && (this.volumes().length === 0 && chapters.length === 0 && this.storylineChapters().length > 0)) return true;

    return (libType !== LibraryType.Book && libType !== LibraryType.LightNovel && libType !== LibraryType.Comic)
      && (this.volumes().length > 0 || chapters.length > 0);
  });

  readonly shouldShowVolumeTab = computed(() => {
    const libType = this.libraryType();
    const chapters = this.chapters();

    if (libType === LibraryType.ComicVine) {
      if (this.volumes().length > 1) return true;

      return this.specials().length === 0 && chapters.length === 0;

    }

    return this.volumes().length > 0;
  });

  showDetailsTab = computed(() => {
    const seriesMetadataValue = this.seriesMetadata();
    return hasAnyCast(seriesMetadataValue) || (seriesMetadataValue?.genres || []).length > 0
      || (seriesMetadataValue?.tags || []).length > 0 || (seriesMetadataValue?.webLinks || []).length > 0;
  });

  weblinks = computed(() => {
    const seriesMetadataValue = this.seriesMetadata();
    const webLinks = seriesMetadataValue?.webLinks || '';
    if (!webLinks) return [];

    return webLinks.split(',');
  });

  seriesBasicMetadata = computed<BasicMetadataInfo>(() => {
    const s = this.series();
    const meta = this.seriesMetadata();
    return {
      readingTime: s,
      pages: s.pages,
      words: s.wordCount,
      addedAt: s.created,
      updatedAt: s.lastChapterAdded,
      kavitaId: s.id,
      language: meta?.language || null,
      publicationStatus: meta?.publicationStatus ?? null,
    };
  });

  trackStoryLineIdentity = (index: number, item: StoryLineItem) => item.isChapter ? `${item.chapter!.data.id}_ch_storyline` : `${item.volume!.data.id}_vol_storyline`;

  /**
   * Related Series. Sorted by backend
   */
  relations = signal<RelatedSeriesPair[]>([]);
  /**
   * Recommended Series
   */
  combinedRecs = signal<Array<Series | ExternalSeries>>([]);
  hasRecommendations = computed(() => this.combinedRecs().length > 0);

  showChapterTab = computed(() => this.chapters().length > 0);
  annotations = signal<Annotation[]>([]);

  totalRelatedCount = computed(() => this.relations().length + this.readingLists().length + this.collections().length + (this.bookmarks().length > 0 ? 1 : 0));
  /** Are there any related series */
  hasRelations = computed(() => this.relations().length > 0);

  get ScrollingBlockHeight() {
    if (this.scrollingBlock() === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const navbarHeight = navbar.offsetHeight;
    const totalHeight = navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }



  constructor() {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;


    this.bulkSelectionService.registerResolver(() => {
      // Tab-dependent chapter array
      let chapterArray = this.activeTabId === Tabs.Chapters ? this.chapters() : this.storylineChapters();
      const offset = this.activeTabId === Tabs.Storyline ? this.volumes().length : 0;

      const volIndices = this.bulkSelectionService.getSelectedCardsForSource('volume');
      const chIndices = this.bulkSelectionService.getSelectedCardsForSource('chapter');
      const spIndices = this.bulkSelectionService.getSelectedCardsForSource('special');

      return {
        volumes: this.volumes().filter((_, i) => volIndices.includes(i + '')),
        chapters: [
          ...chapterArray.filter((_, i) => chIndices.includes((i + offset) + '')),
          ...this.specials().filter((_, i) => spIndices.includes(i + '')),
        ],
      };
    });
    this.bulkSelectionService.registerContext(() => ({ seriesId: this.seriesId(), libraryId: this.libraryId(), libraryType: this.libraryType() }));
    this.bulkSelectionService.registerPostAction((res: ActionResult<Volume[] | Chapter[]>) => {
      if (res.effect === 'none') return;
      if (res.effect === 'reload') {
        this.loadPageSource.next(false);
        return;
      }

      if (res.effect === 'update') {
        const entityMap = new Map<number, any>(res.entity.map(e => [e.id, e]));

        patchSignalArray(this.volumes, entityMap);
        patchSignalArray(this.chapters, entityMap);
        patchSignalArray(this.specials, entityMap);
        patchSignalArray(this.storylineChapters, entityMap);
      }
      this.setContinuePoint();
    });
  }

  ngAfterViewInit(): void {
    this.scrollService.setScrollContainer(this.scrollingBlock());
  }

  ngOnInit(): void {
    this.loadPage$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(300), tap(val => this.loadSeries(this.seriesId(), val))).subscribe();

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemovedEvent = event.payload as SeriesRemovedEvent;
        if (seriesRemovedEvent.seriesId === this.seriesId()) {
          this.toastr.info(translate('errors.series-doesnt-exist'));
          this.router.navigateByUrl('/home');
        }
      } else if (event.event === EVENTS.ScanSeries) {
        const seriesScanEvent = event.payload as ScanSeriesEvent;
        if (seriesScanEvent.seriesId === this.seriesId()) {
          this.loadPageSource.next(false);
        }
      } else if (event.event === EVENTS.CoverUpdate) {
        const coverUpdateEvent = event.payload as CoverUpdateEvent;
        if (coverUpdateEvent.id === this.seriesId() && coverUpdateEvent.entityType === 'series') {
          this.themeService.refreshColorScape('series', this.seriesId()).subscribe();
        }
      } else if (event.event === EVENTS.ChapterRemoved) {
        const removedEvent = event.payload as ChapterRemovedEvent;
        if (removedEvent.seriesId !== this.seriesId()) return;
        this.loadPageSource.next(false);
      } else if (event.event === EVENTS.VolumeRemoved) {
        const volumeRemoveEvent = event.payload as VolumeRemovedEvent;
        if (volumeRemoveEvent.seriesId === this.seriesId()) {
          this.loadPageSource.next(false);
        }
      } else if (event.event === EVENTS.SeriesUpdated) {
        if ((event.payload as SeriesUpdateEvent).id === this.seriesId()) {
          this.loadPageSource.next(false);
        }
      }
    });


    this.scrobbleService.hasHold(this.seriesId()).subscribe(res => {
      this.isScrobbling.set(!res);
    });

    this.scrobbleService.libraryAllowsScrobbling(this.seriesId()).subscribe(res => {
      this.libraryAllowsScrobbling.set(res);
    });


    this.route.fragment.pipe(tap(frag => {
      if (frag !== null && this.activeTabId !== (frag as Tabs)) {
        this.activeTabId = frag as Tabs;
        this.updateUrl(this.activeTabId);
        this.cdRef.markForCheck();
      }
    }), takeUntilDestroyed(this.destroyRef)).subscribe();

    this.loadPageSource.next(true);
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

  onSeriesActionResult(event: any) {
    if (!('effect' in event)) return; // Ignore legacy ActionItem events
    const result = event as ActionResult<Series>;
    switch (result.effect) {
      case 'update':
        this.loadPageSource.next(false);
        break;
      case 'remove':
        this.router.navigate(['library', this.libraryId()]);
        break;
      case 'reload':
        this.loadPageSource.next(true);
        break;
      case 'none':
        break;
    }
  }


  loadSeries(seriesId: number, loadExternal: boolean = false) {
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.series.set(series);
    });

    this.loadReadingHistory(this.readingHistoryCurrentPage(), READING_HISTORY_PAGE_SIZE);

    this.seriesService.getMetadata(seriesId).subscribe(metadata => {
      this.seriesMetadata.set({...metadata});

      if (![PublicationStatus.Ended, PublicationStatus.OnGoing].includes(this.seriesMetadata()!.publicationStatus)) return;

      this.seriesService.getNextExpectedChapterDate(seriesId).subscribe(date => {
        if (date == null || date.expectedDate === null) {
          if (this.nextExpectedChapter !== null) {
            // Clear out the data so the card removes
            this.nextExpectedChapter.set(null);
          }
          return;
        }

        this.nextExpectedChapter.set(date);
      });
    });

    this.seriesService.isWantToRead(seriesId).subscribe(isWantToRead => {
      this.isWantToRead.set(isWantToRead);
    });

    this.loadReadingLists(seriesId);
    this.loadCollections(seriesId);
    this.loadBookmarks(seriesId);
    this.loadRelatedSeries(seriesId);

    this.annotationService.getAnnotationsForSeries(seriesId).subscribe(annotationsForSeries => {
      this.annotations.set(annotationsForSeries);
    });

    this.readerService.getTimeLeft(seriesId).subscribe((timeLeft) => {
      this.readingTimeLeft.set(timeLeft);
    });

    this.setContinuePoint();

    this.themeService.setColorScape(this.series().primaryColor, this.series().secondaryColor);

    if (loadExternal) {
      this.loadPlusMetadata(this.seriesId(), this.library().type);
    }

    this.seriesService.getSeriesDetail(this.seriesId()).pipe(catchError(_ => {
      this.router.navigateByUrl('/home');
      return of(null);
    })).subscribe(detail => {
      if (detail == null) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.unreadCount.set(detail.unreadCount);
      this.totalCount.set(detail.totalCount);

      this.specials.set(detail.specials);
      this.chapters.set(detail.chapters);
      this.volumes.set(detail.volumes);
      this.storylineChapters.set(detail.storylineChapters);

      if (!this.router.url.includes('#')) {
        this.updateSelectedTab();
      } else if (this.activeTabId != Tabs.Storyline) {
        // Validate that the tab we are selected is still there (in case this comes from a messageHub)
        switch (this.activeTabId) {
          case Tabs.Related:
            if (!this.hasRelations()) this.updateSelectedTab();
            break;
          case Tabs.Specials:
            if (!this.hasSpecials()) this.updateSelectedTab();
            break;
          case Tabs.Volumes:
            if (this.volumes().length === 0) this.updateSelectedTab();
            break;
          case Tabs.Chapters:
            if (this.chapters().length === 0) this.updateSelectedTab();
            break;
          case Tabs.Recommendations:
            if (!this.hasRecommendations()) this.updateSelectedTab();
            break;
          case Tabs.Reviews:
            if (this.reviews().length === 0) this.updateSelectedTab();
            break;
          case Tabs.Details:
            break;
        }
      }

      this.isLoading.set(false);
    });

  }

  private loadRelatedSeries(seriesId: number) {
    this.seriesService.getRelatedForSeries(seriesId).subscribe((relations: RelatedSeries) => {
      this.relations.set([
        ...relations.prequels.map(item => this.createRelatedSeries(item, RelationKind.Prequel)),
        ...relations.sequels.map(item => this.createRelatedSeries(item, RelationKind.Sequel)),
        ...relations.sideStories.map(item => this.createRelatedSeries(item, RelationKind.SideStory)),
        ...relations.spinOffs.map(item => this.createRelatedSeries(item, RelationKind.SpinOff)),
        ...relations.adaptations.map(item => this.createRelatedSeries(item, RelationKind.Adaptation)),
        ...relations.contains.map(item => this.createRelatedSeries(item, RelationKind.Contains)),
        ...relations.characters.map(item => this.createRelatedSeries(item, RelationKind.Character)),
        ...relations.others.map(item => this.createRelatedSeries(item, RelationKind.Other)),
        ...relations.alternativeSettings.map(item => this.createRelatedSeries(item, RelationKind.AlternativeSetting)),
        ...relations.alternativeVersions.map(item => this.createRelatedSeries(item, RelationKind.AlternativeVersion)),
        ...relations.doujinshis.map(item => this.createRelatedSeries(item, RelationKind.Doujinshi)),
        ...relations.parent.map(item => this.createRelatedSeries(item, RelationKind.Parent)),
        ...relations.editions.map(item => this.createRelatedSeries(item, RelationKind.Edition)),
        ...relations.annuals.map(item => this.createRelatedSeries(item, RelationKind.Annual)),
      ]);
    });
  }

  private loadReadingLists(seriesId: number) {
    this.readingListService.getReadingListsForSeries(seriesId).subscribe(lists => {
      this.readingLists.set(lists);
    });
  }

  private loadCollections(seriesId: number) {
    this.collectionTagService.allCollectionsForSeries(seriesId, false).subscribe(tags => {
      this.collections.set(tags);
    });
  }

  private loadBookmarks(seriesId: number) {
    this.readerService.getBookmarksForSeries(seriesId).subscribe(bookmarks => {
      if (bookmarks.length === 0) {
        this.bookmarks.set([]);
        return;
      }

      const seen = new Map<number, PageBookmark>();
      for (const bookmark of bookmarks) {
        if (!seen.has(bookmark.seriesId)) {
          seen.set(bookmark.seriesId, bookmark);
        }
      }

      this.bookmarks.set(Array.from(seen.values()));
    });
  }

  createRelatedSeries(series: Series, relation: RelationKind) {
    return {series, relation} as RelatedSeriesPair;
  }

  loadReadingHistory(page: number, pageSize: number) {
    this.isLoadingReadingHistory.set(true);

    this.statisticsService.getReadingHistoryForSeries(this.seriesId(), page, pageSize).pipe(
      tap(result => {
        this.readingHistory.set(result.result);
        this.readingHistoryPagination.set(result.pagination);
        this.readingHistoryCurrentPage.set(page);
        this.isLoadingReadingHistory.set(false);
      }),
      catchError(() => {
        this.isLoadingReadingHistory.set(false);
        return EMPTY;
      }),
    ).subscribe();
  }


  /**
   * This will update the selected tab
   *
   * This assumes loadPage() has already primed all the calculations and state variables. Do not call directly.
   */
  updateSelectedTab() {
    const libType = this.libraryType();
    // Book libraries only have Volumes or Specials enabled
    if (libType === LibraryType.Book || libType === LibraryType.LightNovel) {
      if (this.volumes().length === 0) {
        if (this.specials().length === 0 && this.storylineChapters().length > 0) {
          // NOTE: This is an edge case caused by bad parsing of pdf files. Once the new pdf parser is in place, this should be removed
          this.activeTabId = Tabs.Storyline;
        } else {
          this.activeTabId = Tabs.Specials;
        }
      } else {
        this.activeTabId = Tabs.Volumes;
      }
      this.updateUrl(this.activeTabId);
      this.cdRef.markForCheck();
      return;
    }

    if (this.volumes().length === 0 && this.chapters().length === 0 && this.specials().length > 0) {
      this.activeTabId = Tabs.Specials;
    } else {
      if (libType == LibraryType.Comic || libType == LibraryType.ComicVine) {
        if (this.chapters().length === 0) {

          if (this.volumes().length > 0) {
            this.activeTabId = Tabs.Volumes;
          } else if (this.specials().length > 0) {
            this.activeTabId = Tabs.Specials;
          }
        } else {
          this.activeTabId = Tabs.Chapters;
        }
      } else {
        this.activeTabId = Tabs.Storyline;
      }
    }

    // BUG: Related or other tab can be in history but no longer there, need to default

    this.updateUrl(this.activeTabId);
    this.cdRef.markForCheck();
  }


  loadPlusMetadata(seriesId: number, libraryType: LibraryType) {
    this.isLoadingExtra.set(true);

    this.metadataService.getSeriesMetadataFromPlus(seriesId, libraryType).subscribe(data => {
      if (data === null) {
        this.isLoadingExtra.set(false);
        return;
      }

      // Reviews
      this.reviews.set(data.reviews.filter(r => !r.isExternal));
      this.plusReviews.set(data.reviews.filter(r => r.isExternal));

      if (data.ratings) {
        this.ratings.set([...data.ratings]);
      }


      // Recommendations
      if (data.recommendations) {
        this.combinedRecs.set([...data.recommendations.ownedSeries, ...data.recommendations.externalSeries]);
      }


      this.isLoadingExtra.set(false);
    });
  }

  setContinuePoint() {
    this.readerService.hasSeriesProgress(this.seriesId()).subscribe(hasProgress => {
      this.hasReadingProgress.set(hasProgress);
    });

    this.readerService.getCurrentChapter(this.seriesId()).subscribe(chapter => {
      this.currentlyReadingChapter.set(chapter);
    });
  }

  read(incognitoMode: boolean = false) {
    if (this.bulkSelectionService.hasSelections()) return;

    this.readerService.readSeries(this.series()!, incognitoMode);
  }

  handleRelatedReload(event: RelatedTabChangeEvent) {
    switch (event.entity) {
      case 'bookmark':
        this.loadBookmarks(this.seriesId());
        this.updateSelectedTab();
        break;
      case 'collection':
        this.loadCollections(this.seriesId());
        this.updateSelectedTab();
        break;
      case 'readingList':
        this.loadReadingLists(this.seriesId());
        this.updateSelectedTab();
        break;
      case 'relation':
        this.loadRelatedSeries(this.seriesId());
        this.updateSelectedTab();
        break;
    }
  }

  openEditSeriesModal() {
    const modalRef = this.modalService.open(EditSeriesModalComponent);
    modalRef.componentInstance.series = this.series();
    modalRef.closed.subscribe((closeResult: ModalResult<Series>) => {
      if (closeResult.success) {
        window.scrollTo(0, 0);
      }
      this.loadPageSource.next(false);
    });
  }

  toggleWantToRead() {
    if (this.isWantToRead()) {
      this.actionService.removeMultipleSeriesFromWantToReadList([this.seriesId()]);
    } else {
      this.actionService.addMultipleSeriesToWantToReadList([this.seriesId()]);
    }

    this.isWantToRead.update(x => !x);
  }

  openFilter(field: SeriesFilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }

  toggleScrobbling(evt: any) {
    evt.stopPropagation();

    if (this.isScrobbling()) {
      this.scrobbleService.addHold(this.seriesId()).subscribe();
    } else {
      this.scrobbleService.removeHold(this.seriesId()).subscribe();
    }
    this.isScrobbling.update(x => !x);
  }

  openKavitaPlusDrawer() {
    const ref = this.drawerService.open(KavitaplusDrawerComponent, { position: 'end', panelClass: 'kplus-offcanvas' });
    ref.setInput('seriesId', this.seriesId());
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

  updateChapter(c: Chapter) {
    patchEntitySignal(this.chapters, c);
    patchEntitySignal(this.specials, c);
    patchEntitySignal(this.storylineChapters, c);
    this.setContinuePoint();
  }

  updateVolume(c: Volume) {
    patchEntitySignal(this.volumes, c);
    this.setContinuePoint();
  }

  protected readonly LibraryType = LibraryType;
  protected readonly Tabs = Tabs;
  protected readonly LooseLeafOrSpecialNumber = LooseLeafOrDefaultNumber;
  protected readonly SpecialVolumeNumber = SpecialVolumeNumber;
  protected readonly SettingsTabId = SettingsTabId;
  protected readonly FilterField = SeriesFilterField;
  protected readonly AgeRating = AgeRating;
  protected readonly encodeURIComponent = encodeURIComponent;
  protected readonly Breakpoint = Breakpoint;
  protected readonly READING_HISTORY_PAGE_SIZE = READING_HISTORY_PAGE_SIZE;
}

export default SeriesDetailComponent
