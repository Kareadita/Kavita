import {AsyncPipe, DOCUMENT, Location, NgClass, NgStyle, NgTemplateOutlet} from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  Inject,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {Title} from '@angular/platform-browser';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {
  NgbDropdown,
  NgbDropdownItem,
  NgbDropdownMenu,
  NgbDropdownToggle,
  NgbModal,
  NgbNav,
  NgbNavChangeEvent,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {catchError, debounceTime, forkJoin, Observable, of, ReplaySubject, tap} from 'rxjs';
import {map} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {
  EditSeriesModalCloseResult,
  EditSeriesModalComponent
} from 'src/app/cards/_modals/edit-series-modal/edit-series-modal.component';
import {DownloadEvent, DownloadService} from 'src/app/shared/_services/download.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {Chapter, LooseLeafOrDefaultNumber, SpecialVolumeNumber} from 'src/app/_models/chapter';
import {Device} from 'src/app/_models/device/device';
import {ScanSeriesEvent} from 'src/app/_models/events/scan-series-event';
import {SeriesRemovedEvent} from 'src/app/_models/events/series-removed-event';
import {LibraryType} from 'src/app/_models/library/library';
import {ReadingList} from 'src/app/_models/reading-list';
import {Series} from 'src/app/_models/series';
import {RelatedSeries} from 'src/app/_models/series-detail/related-series';
import {RelationKind} from 'src/app/_models/series-detail/relation-kind';
import {SeriesMetadata} from 'src/app/_models/metadata/series-metadata';
import {User} from 'src/app/_models/user';
import {Volume} from 'src/app/_models/volume';
import {AccountService} from 'src/app/_services/account.service';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {ActionService} from 'src/app/_services/action.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {NavService} from 'src/app/_services/nav.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {SeriesService} from 'src/app/_services/series.service';
import {
  ReviewSeriesModalCloseAction,
  ReviewSeriesModalCloseEvent,
  ReviewSeriesModalComponent
} from '../../../_single-module/review-series-modal/review-series-modal.component';
import {PageLayoutMode} from 'src/app/_models/page-layout-mode';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {UserReview} from "../../../_single-module/review-card/user-review";
import {ExternalSeriesCardComponent} from '../../../cards/external-series-card/external-series-card.component';
import {SeriesCardComponent} from '../../../cards/series-card/series-card.component';
import {VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {ReviewCardComponent} from '../../../_single-module/review-card/review-card.component';
import {CarouselReelComponent} from '../../../carousel/_components/carousel-reel/carousel-reel.component';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {PublicationStatus} from "../../../_models/metadata/publication-status";
import {NextExpectedChapter} from "../../../_models/series-detail/next-expected-chapter";
import {NextExpectedCardComponent} from "../../../cards/next-expected-card/next-expected-card.component";
import {MetadataService} from "../../../_services/metadata.service";
import {Rating} from "../../../_models/rating";
import {ThemeService} from "../../../_services/theme.service";
import {DetailsTabComponent} from "../../../_single-module/details-tab/details-tab.component";
import {
  EditChapterModalCloseResult,
  EditChapterModalComponent
} from "../../../_single-module/edit-chapter-modal/edit-chapter-modal.component";
import {ChapterRemovedEvent} from "../../../_models/events/chapter-removed-event";
import {ChapterCardComponent} from "../../../cards/chapter-card/chapter-card.component";
import {VolumeCardComponent} from "../../../cards/volume-card/volume-card.component";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
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
import {EditVolumeModalComponent} from "../../../_single-module/edit-volume-modal/edit-volume-modal.component";
import {CoverUpdateEvent} from "../../../_models/events/cover-update-event";
import {RelatedSeriesPair, RelatedTabComponent} from "../../../_single-module/related-tab/related-tab.component";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {UserCollection} from "../../../_models/collection-tag";
import {CoverImageComponent} from "../../../_single-module/cover-image/cover-image.component";
import {DefaultModalOptions} from "../../../_models/default-modal-options";
import {LicenseService} from "../../../_services/license.service";
import {PageBookmark} from "../../../_models/readers/page-bookmark";


enum TabID {
  Related = 'related-tab',
  Specials = 'specials-tab',
  Storyline = 'storyline-tab',
  Volumes = 'volume-tab',
  Chapters = 'chapter-tab',
  Recommendations = 'recommendations-tab',
  Reviews = 'reviews-tab',
  Details = 'details-tab',
}

interface StoryLineItem {
  chapter?: Chapter;
  volume?: Volume;
  isChapter: boolean;
}

@Component({
    selector: 'app-series-detail',
    templateUrl: './series-detail.component.html',
    styleUrls: ['./series-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [CardActionablesComponent, ReactiveFormsModule, NgStyle,
    NgbTooltip, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu,
    NgbDropdownItem, CarouselReelComponent, ReviewCardComponent, BulkOperationsComponent,
    NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, VirtualScrollerModule, SeriesCardComponent, ExternalSeriesCardComponent, NgbNavOutlet,
    TranslocoDirective, NgTemplateOutlet, NextExpectedCardComponent,
    NgClass, AsyncPipe, DetailsTabComponent, ChapterCardComponent,
    VolumeCardComponent, DefaultValuePipe, ExternalRatingComponent, ReadMoreComponent, RouterLink, BadgeExpanderComponent,
    PublicationStatusPipe, MetadataDetailRowComponent, DownloadButtonComponent, RelatedTabComponent, CoverImageComponent]
})
export class SeriesDetailComponent implements OnInit, AfterContentChecked {

  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly seriesService = inject(SeriesService);
  private readonly metadataService = inject(MetadataService);
  private readonly router = inject(Router);
  private readonly modalService = inject(NgbModal);
  private readonly toastr = inject(ToastrService);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly libraryService = inject(LibraryService);
  private readonly titleService = inject(Title);
  private readonly downloadService = inject(DownloadService);
  private readonly actionService = inject(ActionService);
  private readonly messageHub = inject(MessageHubService);
  private readonly readingListService = inject(ReadingListService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrollService = inject(ScrollService);
  private readonly translocoService = inject(TranslocoService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly imageService = inject(ImageService);
  protected readonly navService = inject(NavService);
  protected readonly readerService = inject(ReaderService);
  protected readonly themeService = inject(ThemeService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly scrobbleService = inject(ScrobblingService);
  private readonly location = inject(Location);

  protected readonly LibraryType = LibraryType;
  protected readonly TabID = TabID;
  protected readonly LooseLeafOrSpecialNumber = LooseLeafOrDefaultNumber;
  protected readonly SpecialVolumeNumber = SpecialVolumeNumber;
  protected readonly SettingsTabId = SettingsTabId;
  protected readonly FilterField = FilterField;
  protected readonly AgeRating = AgeRating;
  protected readonly Breakpoint = Breakpoint;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;


  /**
   * Series Id. Set at load before UI renders
   */
  seriesId!: number;
  series!: Series;
  volumes: Volume[] = [];
  chapters: Chapter[] = [];
  storyChapters: Chapter[] = [];
  storylineItems: StoryLineItem[] = [];
  libraryId = 0;
  isAdmin = false;
  hasDownloadingRole = false;
  isLoading = true;
  isLoadingExtra = false;
  libraryAllowsScrobbling = false;
  isScrobbling: boolean = true;
  mobileSeriesImgBackground: string | undefined;

  currentlyReadingChapter: Chapter | undefined = undefined;
  hasReadingProgress = false;


  seriesActions: ActionItem<Series>[] = [];
  volumeActions: ActionItem<Volume>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];

  hasSpecials = false;
  specials: Array<Chapter> = [];
  activeTabId = TabID.Storyline;

  reviews: Array<UserReview> = [];
  plusReviews: Array<UserReview> = [];
  bookmarks: Array<PageBookmark> = [];
  ratings: Array<Rating> = [];
  libraryType: LibraryType = LibraryType.Manga;
  seriesMetadata: SeriesMetadata | null = null;
  readingLists: Array<ReadingList> = [];
  collections: Array<UserCollection> = [];
  isWantToRead: boolean = false;
  unreadCount: number = 0;
  totalCount: number = 0;
  readingTimeLeft: HourEstimateRange | null = null;
  /**
   * Poster image for the Series
   */
  seriesImage: string = '';
  downloadInProgress: boolean = false;

  nextExpectedChapter: NextExpectedChapter | undefined;
  loadPageSource = new ReplaySubject<boolean>(1);
  loadPage$ = this.loadPageSource.asObservable();

  /**
   * Track by function for Volume to tell when to refresh card data
   */
  trackByVolumeIdentity = (index: number, item: Volume) => `${item.name}_${item.pagesRead}`;
  /**
   * Track by function for Chapter to tell when to refresh card data
   */
  trackByChapterIdentity = (index: number, item: Chapter) => `${item.title}_${item.minNumber}_${item.maxNumber}_${item.volumeId}_${item.pagesRead}`;

  /**
   * Are there any related series
   */
  hasRelations: boolean = false;
  /**
   * Are there recommendations
   */
  hasRecommendations: boolean = false;

  /**
   * Related Series. Sorted by backend
   */
  relations: Array<RelatedSeriesPair> = [];
  relationShips: RelatedSeries | null = null;
  /**
   * Recommended Series
   */
  combinedRecs: Array<any> = [];

  sortingOptions: Array<{value: string, text: string}> = [
    {value: 'Storyline', text: 'Storyline'},
    {value: 'Release', text: 'Release'},
    {value: 'Added', text: 'Added'},
  ];
  renderMode: PageLayoutMode = PageLayoutMode.Cards;

  pageExtrasGroup = new FormGroup({
    'sortingOption': new FormControl(this.sortingOptions[0].value, []),
    'renderMode': new FormControl(this.renderMode, []),
  });

  user: User | undefined;
  showVolumeTab = true;
  showStorylineTab = true;
  showChapterTab = true;
  showDetailsTab = true;

  /**
   * This is the download we get from download service.
   */
  download$: Observable<DownloadEvent | null> | null = null;

  bulkActionCallback = async (action: ActionItem<any>, data: any) => {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;
    // we need to figure out what is actually selected now
    const selectedVolumeIndexes = this.bulkSelectionService.getSelectedCardsForSource('volume');
    const selectedChapterIndexes = this.bulkSelectionService.getSelectedCardsForSource('chapter');
    const selectedSpecialIndexes = this.bulkSelectionService.getSelectedCardsForSource('special');

    // NOTE: This needs to check current tab as chapter array will be different
    let chapterArray = this.storyChapters;
    if (this.activeTabId === TabID.Chapters) chapterArray = this.chapters;

    // We must augment chapter indices as Bulk Selection assumes all on one page, but Storyline has mixed
    const chapterIndexModifier = this.activeTabId === TabID.Storyline ? this.volumes.length : 0;
    const selectedChapterIds = chapterArray.filter((_chapter, index: number) => {
      const mappedIndex = index + chapterIndexModifier;
      return selectedChapterIndexes.includes(mappedIndex + '');
    });
    const selectedVolumeIds = this.volumes.filter((_volume, index: number) => selectedVolumeIndexes.includes(index + ''));
    const selectedSpecials = this.specials.filter((_chapter, index: number) => selectedSpecialIndexes.includes(index + ''));
    const chapters = [...selectedChapterIds, ...selectedSpecials];

    switch (action.action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleToReadingList(seriesId, selectedVolumeIds, chapters, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleAsRead(seriesId, selectedVolumeIds, chapters,  () => {
          this.setContinuePoint();
          // TODO: BUG: This doesn't update series pagesRead
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });

        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleAsUnread(seriesId, selectedVolumeIds, chapters,  () => {
          this.setContinuePoint();
          // TODO: BUG: This doesn't update series pagesRead
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.SendTo:
        const device = (action._extra!.data as Device);
        this.actionService.sendToDevice(chapters.map(c => c.id), device);
        this.bulkSelectionService.deselectAll();
        this.cdRef.markForCheck();
        break;
      case Action.Delete:
        await this.actionService.deleteMultipleChapters(seriesId, chapters, () => {
          // No need to update the page as the backend will spam volume/chapter deletions
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
    }
  }


  get UseBookLogic() {
    return this.libraryType === LibraryType.Book || this.libraryType === LibraryType.LightNovel;
  }

  get WebLinks() {
    if (!this.seriesMetadata || this.seriesMetadata?.webLinks === '') return [];
    return this.seriesMetadata.webLinks.split(',');
  }

  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar?.nativeElement.offsetHeight || 0;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  get ContinuePointTitle() {
    if (this.currentlyReadingChapter === undefined || !this.hasReadingProgress) return '';

    if (!this.currentlyReadingChapter.isSpecial) {
      const vol = this.volumes.filter(v => v.id === this.currentlyReadingChapter?.volumeId);

      let chapterLocaleKey = 'common.chapter-num-shorthand';
      let volumeLocaleKey = 'common.volume-num-shorthand';
      switch (this.libraryType) {
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
        if (this.currentlyReadingChapter.minNumber === LooseLeafOrDefaultNumber) {
          return this.currentlyReadingChapter.titleName;
        }
        return translate(chapterLocaleKey, {num: this.currentlyReadingChapter.minNumber});
      }

      if (this.currentlyReadingChapter.minNumber === LooseLeafOrDefaultNumber) {
        return translate(volumeLocaleKey, {num: vol[0].minNumber});
      }

      return translate(volumeLocaleKey, {num: vol[0].minNumber})
        + ' ' + translate(chapterLocaleKey, {num: this.currentlyReadingChapter.minNumber});
    }

    return this.currentlyReadingChapter.title;
  }


  constructor(@Inject(DOCUMENT) private document: Document) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;


    this.accountService.currentUser$.subscribe(user => {
      if (user) {
        this.user = user;
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.hasDownloadingRole = this.accountService.hasDownloadRole(user);
        this.renderMode = user.preferences.globalPageLayoutMode;
        this.pageExtrasGroup.get('renderMode')?.setValue(this.renderMode);
        this.cdRef.markForCheck();
      }
    });
  }

  ngAfterContentChecked(): void {
    this.scrollService.setScrollContainer(this.scrollingBlock);
  }

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('seriesId');
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null || libraryId == null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.mobileSeriesImgBackground = getComputedStyle(document.documentElement)
      .getPropertyValue('--mobile-series-img-background').trim();


    // Set up the download in progress
    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
      return this.downloadService.mapToEntityType(events, this.series);
    }));

    this.loadPage$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(300), tap(val => this.loadSeries(this.seriesId, val))).subscribe();

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemovedEvent = event.payload as SeriesRemovedEvent;
        if (seriesRemovedEvent.seriesId === this.seriesId) {
          this.toastr.info(this.translocoService.translate('errors.series-doesnt-exist'));
          this.router.navigateByUrl('/home');
        }
      } else if (event.event === EVENTS.ScanSeries) {
        const seriesScanEvent = event.payload as ScanSeriesEvent;
        if (seriesScanEvent.seriesId === this.seriesId) {
          this.loadPageSource.next(false);
        }
      } else if (event.event === EVENTS.CoverUpdate) {
        const coverUpdateEvent = event.payload as CoverUpdateEvent;
        if (coverUpdateEvent.id === this.seriesId) {
          this.themeService.refreshColorScape('series', this.seriesId).subscribe();
        }
      } else if (event.event === EVENTS.ChapterRemoved) {
        const removedEvent = event.payload as ChapterRemovedEvent;
        if (removedEvent.seriesId !== this.seriesId) return;
        this.loadPageSource.next(false);
      }
    });

    this.seriesId = parseInt(routeId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.seriesImage = this.imageService.getSeriesCoverImage(this.seriesId);
    this.cdRef.markForCheck();

    this.scrobbleService.hasHold(this.seriesId).subscribe(res => {
      this.isScrobbling = !res;
      this.cdRef.markForCheck();
    });

    this.scrobbleService.libraryAllowsScrobbling(this.seriesId).subscribe(res => {
      this.libraryAllowsScrobbling = res;
      this.cdRef.markForCheck();
    });



    this.route.fragment.pipe(tap(frag => {
      if (frag !== null && this.activeTabId !== (frag as TabID)) {
        this.activeTabId = frag as TabID;
        this.updateUrl(this.activeTabId);
        this.cdRef.markForCheck();
      }
    }), takeUntilDestroyed(this.destroyRef)).subscribe();

    //this.loadSeries(this.seriesId, true);
    this.loadPageSource.next(true);

    this.pageExtrasGroup.get('renderMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((val: PageLayoutMode | null) => {
      if (val == null) return;
      this.renderMode = val;
      this.cdRef.markForCheck();
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

  handleSeriesActionCallback(action: ActionItem<Series>, series: Series) {
    this.cdRef.markForCheck();
    switch(action.action) {
      case(Action.MarkAsRead):
        this.actionService.markSeriesAsRead(series, (series: Series) => {
          this.loadPageSource.next(false);
        });
        break;
      case(Action.MarkAsUnread):
        this.actionService.markSeriesAsUnread(series, (series: Series) => {
          this.loadPageSource.next(false);
        });
        break;
      case(Action.Scan):
        this.actionService.scanSeries(series);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshSeriesMetadata(series, undefined, true, false);
        break;
      case(Action.GenerateColorScape):
        this.actionService.refreshSeriesMetadata(series, undefined, false, true);
        break;
      case(Action.Delete):
        this.deleteSeries(series);
        break;
      case(Action.AddToReadingList):
        this.actionService.addSeriesToReadingList(series);
        break;
      case(Action.AddToCollection):
        this.actionService.addMultipleSeriesToCollectionTag([series]);
        break;
      case (Action.AnalyzeFiles):
        this.actionService.analyzeFilesForSeries(series);
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList([series.id]);
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList([series.id]);
        break;
      case Action.Download:
        if (this.downloadInProgress) return;
        this.downloadSeries();
        break;
      case Action.Match:
        this.actionService.matchSeries(this.series, (refreshNeeded) => {
          if (refreshNeeded) {
            this.loadSeries(this.series.id, refreshNeeded);
          }
        });
        break;
      case Action.SendTo:
        {
          const chapterIds = [...this.volumes.map(v => v.chapters.map(c => c.id)).flat(), ...this.specials.map(c => c.id)]
          const device = (action._extra!.data as Device);
          this.actionService.sendToDevice(chapterIds, device);
          break;
        }
      default:
        break;
    }
  }

  async handleVolumeActionCallback(action: ActionItem<Volume>, volume: Volume) {
    switch(action.action) {
      case(Action.MarkAsRead):
        this.markVolumeAsRead(volume);
        break;
      case(Action.MarkAsUnread):
        this.markVolumeAsUnread(volume);
        break;
      case(Action.Edit):
        this.openEditVolume(volume);
        break;
      case(Action.Delete):
        await this.actionService.deleteVolume(volume.id, (b) => {
          if (!b) return;
          this.loadPageSource.next(false);
        });
        break;
      case(Action.AddToReadingList):
        this.actionService.addVolumeToReadingList(volume, this.seriesId, () => {/* No Operation */ });
        break;
      case(Action.IncognitoRead):
        if (volume.chapters != undefined && volume.chapters?.length >= 1) {
          this.openChapter(volume.chapters.sort(this.utilityService.sortChapters)[0], true);
        }
        break;
      case (Action.SendTo):
        {
          const device = (action._extra!.data as Device);
          this.actionService.sendToDevice(volume.chapters.map(c => c.id), device);
          break;
        }
      default:
        break;
    }
  }

  handleChapterActionCallback(action: ActionItem<Chapter>, chapter: Chapter) {
    switch (action.action) {
      case(Action.MarkAsRead):
        this.markChapterAsRead(chapter);
        break;
      case(Action.MarkAsUnread):
        this.markChapterAsUnread(chapter);
        break;
      case(Action.Edit):
        this.openEditChapter(chapter);
        break;
      case(Action.AddToReadingList):
        this.actionService.addChapterToReadingList(chapter, this.seriesId, () => {/* No Operation */ });
        break;
      case(Action.IncognitoRead):
        this.openChapter(chapter, true);
        break;
      case (Action.SendTo):
        const device = (action._extra!.data as Device);
        this.actionService.sendToDevice([chapter.id], device);
        break;
      default:
        break;
    }
  }


  async deleteSeries(series: Series) {
    await this.actionService.deleteSeries(series, (result: boolean) => {
      this.cdRef.markForCheck();
      if (result) {
        this.router.navigate(['library', this.libraryId]);
      }
    });
  }

  loadSeries(seriesId: number, loadExternal: boolean = false) {
    this.seriesService.getMetadata(seriesId).subscribe(metadata => {
      this.seriesMetadata = {...metadata};
      this.cdRef.markForCheck();

      if (![PublicationStatus.Ended, PublicationStatus.OnGoing].includes(this.seriesMetadata.publicationStatus)) return;

      this.seriesService.getNextExpectedChapterDate(seriesId).subscribe(date => {
        if (date == null || date.expectedDate === null) {
          if (this.nextExpectedChapter !== undefined) {
            // Clear out the data so the card removes
            this.nextExpectedChapter = undefined;
            this.cdRef.markForCheck();
          }
          return;
        }

        this.nextExpectedChapter = date;
        this.cdRef.markForCheck();
      });
    });

    this.seriesService.isWantToRead(seriesId).subscribe(isWantToRead => {
      this.isWantToRead = isWantToRead;
      this.cdRef.markForCheck();
    });

    this.readingListService.getReadingListsForSeries(seriesId).subscribe(lists => {
      this.readingLists = lists;
      this.cdRef.markForCheck();
    });

    this.collectionTagService.allCollectionsForSeries(seriesId, false).subscribe(tags => {
      this.collections = tags;
      this.cdRef.markForCheck();
    });


    this.readerService.getBookmarksForSeries(seriesId).subscribe(bookmarks => {
      if (bookmarks.length > 0) {
        this.bookmarks = Object.values(
          bookmarks.reduce((acc, bookmark) => {
            if (!acc[bookmark.seriesId]) {
              acc[bookmark.seriesId] = bookmark; // Select the first one per seriesId
            }
            return acc;
          }, {} as Record<number, PageBookmark>)
        );
      } else {
        this.bookmarks = [];
      }
      this.cdRef.markForCheck();
    });

    this.readerService.getTimeLeft(seriesId).subscribe((timeLeft) => {
      this.readingTimeLeft = timeLeft;
      this.cdRef.markForCheck();
    });

    this.setContinuePoint();


    forkJoin({
      libType: this.libraryService.getLibraryType(this.libraryId),
      series: this.seriesService.getSeries(seriesId)
    }).subscribe(results => {
      this.libraryType = results.libType;
      this.series = results.series;

      this.themeService.setColorScape(this.series.primaryColor, this.series.secondaryColor);

      if (loadExternal) {
        this.loadPlusMetadata(this.seriesId, this.libraryType);
      }

      if (this.libraryType === LibraryType.LightNovel) {
        this.renderMode = PageLayoutMode.List;
        this.pageExtrasGroup.get('renderMode')?.setValue(this.renderMode);
        this.cdRef.markForCheck();
      }


      this.titleService.setTitle('Kavita - ' + this.series.name + ' Details');

      this.volumeActions = this.actionFactoryService.getVolumeActions(this.handleVolumeActionCallback.bind(this));
      this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this));
      this.seriesActions = this.actionFactoryService.getSeriesActions(this.handleSeriesActionCallback.bind(this))
              .filter(action => action.action !== Action.Edit);

      this.licenseService.hasValidLicense$.subscribe(hasLic => {
        if (!hasLic) {
          this.seriesActions = this.seriesActions.filter(action => action.action !== Action.Match);
          this.cdRef.markForCheck();
        }
      });


      this.seriesService.getRelatedForSeries(this.seriesId).subscribe((relations: RelatedSeries) => {
        this.relationShips = relations;
        this.relations = [
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
        ];

        if (this.relations.length > 0) {
          this.hasRelations = true;
          this.cdRef.markForCheck();
        } else {
          this.hasRelations = false;
          this.cdRef.markForCheck();
        }
      });

      this.seriesService.getSeriesDetail(this.seriesId).pipe(catchError(err => {
        this.router.navigateByUrl('/home');
        return of(null);
      })).subscribe(detail => {
        if (detail == null) {
          this.router.navigateByUrl('/home');
          return;
        }

        this.unreadCount = detail.unreadCount;
        this.totalCount = detail.totalCount;

        this.hasSpecials = detail.specials.length > 0;
        this.specials = detail.specials;

        this.chapters = detail.chapters;
        this.volumes = detail.volumes;
        this.storyChapters = detail.storylineChapters;

        this.storylineItems = [];
        const v = this.volumes.map(v => {
          return {volume: v, chapter: undefined, isChapter: false} as StoryLineItem;
        });
        this.storylineItems.push(...v);
        const c = this.storyChapters.map(c => {
          return {volume: undefined, chapter: c, isChapter: true} as StoryLineItem;
        });
        this.storylineItems.push(...c);

        this.updateWhichTabsToShow();

        if (!this.router.url.includes('#')) {
          this.updateSelectedTab();
        }



        this.isLoading = false;
        this.cdRef.markForCheck();
      });
    }, err => {
      this.router.navigateByUrl('/home');
    });
  }

  createRelatedSeries(series: Series, relation: RelationKind) {
    return {series, relation} as RelatedSeriesPair;
  }

  shouldShowStorylineTab() {
    if (this.libraryType === LibraryType.ComicVine) return false;
    // Edge case for bad pdf parse
    if ((this.libraryType === LibraryType.Book || this.libraryType === LibraryType.LightNovel) && (this.volumes.length === 0 && this.chapters.length === 0 && this.storyChapters.length > 0)) return true;

    return (this.libraryType !== LibraryType.Book && this.libraryType !== LibraryType.LightNovel && this.libraryType !== LibraryType.Comic)
      && (this.volumes.length > 0 || this.chapters.length > 0);
  }

  shouldShowVolumeTab() {
    if (this.libraryType === LibraryType.ComicVine) {
      if (this.volumes.length > 1) return true;
      if (this.specials.length === 0 && this.chapters.length === 0) return true;
      return false;
    }
    return this.volumes.length > 0;
  }

  shouldShowChaptersTab() {
    return this.chapters.length > 0;
  }

  updateWhichTabsToShow() {
    this.showVolumeTab = this.shouldShowVolumeTab();
    this.showStorylineTab = this.shouldShowStorylineTab();
    this.showChapterTab = this.shouldShowChaptersTab();
    this.showDetailsTab = hasAnyCast(this.seriesMetadata) || (this.seriesMetadata?.genres || []).length > 0
      || (this.seriesMetadata?.tags || []).length > 0 || (this.seriesMetadata?.webLinks || []).length > 0;
    this.cdRef.markForCheck();
  }

  /**
   * This will update the selected tab
   *
   * This assumes loadPage() has already primed all the calculations and state variables. Do not call directly.
   */
  updateSelectedTab() {
    // Book libraries only have Volumes or Specials enabled
    if (this.libraryType === LibraryType.Book || this.libraryType === LibraryType.LightNovel) {
      if (this.volumes.length === 0) {
        if (this.specials.length === 0 && this.storyChapters.length > 0) {
          // NOTE: This is an edge case caused by bad parsing of pdf files. Once the new pdf parser is in place, this should be removed
          this.activeTabId = TabID.Storyline;
        } else {
          this.activeTabId = TabID.Specials;
        }
      } else {
        this.activeTabId = TabID.Volumes;
      }
      this.updateUrl(this.activeTabId);
      this.cdRef.markForCheck();
      return;
    }

    if (this.volumes.length === 0 && this.chapters.length === 0 && this.specials.length > 0) {
      this.activeTabId = TabID.Specials;
    } else {
      if (this.libraryType == LibraryType.Comic || this.libraryType == LibraryType.ComicVine) {
        if (this.chapters.length === 0) {
          if (this.specials.length > 0) {
            this.activeTabId = TabID.Specials;
          } else {
            this.activeTabId = TabID.Volumes;
          }
        } else {
          this.activeTabId = TabID.Chapters;
        }
      } else {
        this.activeTabId = TabID.Storyline;
      }
    }

    this.updateUrl(this.activeTabId);
    this.cdRef.markForCheck();
  }


  loadPlusMetadata(seriesId: number, libraryType: LibraryType) {
    this.isLoadingExtra = true;
    this.cdRef.markForCheck();

    this.metadataService.getSeriesMetadataFromPlus(seriesId, libraryType).subscribe(data => {
      if (data === null) {
        this.isLoadingExtra = false;
        this.cdRef.markForCheck();
        return;
      }

      // Reviews
      this.reviews = data.reviews.filter(r => !r.isExternal);
      this.plusReviews = data.reviews.filter(r => r.isExternal);

      if (data.ratings) {
        this.ratings = [...data.ratings];
      }


      // Recommendations
      if (data.recommendations) {
        this.combinedRecs = [...data.recommendations.ownedSeries, ...data.recommendations.externalSeries];
      }

      this.hasRecommendations = this.combinedRecs.length > 0;

      this.isLoadingExtra = false;
      this.cdRef.markForCheck();
    });
  }

  setContinuePoint() {
    this.readerService.hasSeriesProgress(this.seriesId).subscribe(hasProgress => {
      this.hasReadingProgress = hasProgress;
      this.cdRef.markForCheck();
    });
    this.readerService.getCurrentChapter(this.seriesId).subscribe(chapter => {
      this.currentlyReadingChapter = chapter;
      this.cdRef.markForCheck();
    });
  }

  markVolumeAsRead(vol: Volume) {
    if (this.series === undefined) {
      return;
    }

    this.actionService.markVolumeAsRead(this.seriesId, vol, () => {
      this.setContinuePoint();
    });
  }

  markVolumeAsUnread(vol: Volume) {
    if (this.series === undefined) {
      return;
    }

    this.actionService.markVolumeAsUnread(this.seriesId, vol, () => {
      this.setContinuePoint();
    });
  }

  markChapterAsRead(chapter: Chapter) {
    if (this.series === undefined) {
      return;
    }

    this.actionService.markChapterAsRead(this.libraryId, this.seriesId, chapter, () => {
      this.setContinuePoint();
    });
  }

  markChapterAsUnread(chapter: Chapter) {
    if (this.series === undefined) {
      return;
    }

    this.actionService.markChapterAsUnread(this.libraryId, this.seriesId, chapter, () => {
      this.setContinuePoint();
    });
  }

  read(incognitoMode: boolean = false) {
    if (this.currentlyReadingChapter !== undefined) {
      this.openChapter(this.currentlyReadingChapter, incognitoMode);
      return;
    }

    this.readerService.getCurrentChapter(this.seriesId).subscribe(chapter => {
      this.openChapter(chapter, incognitoMode);
    });
  }

  openChapter(chapter: Chapter, incognitoMode = false) {
    if (this.bulkSelectionService.hasSelections()) return;
    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'chapter', chapter.id]);

    this.readerService.readChapter(this.libraryId, this.seriesId, chapter, incognitoMode);

  }

  openVolume(volume: Volume) {
    if (this.bulkSelectionService.hasSelections()) return;
    if (volume.chapters === undefined || volume.chapters?.length === 0) {
      this.toastr.error(this.translocoService.translate('series-detail.no-chapters'));
      return;
    }

    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'volume', volume.id]);
    return;


    this.readerService.readVolume(this.libraryId, this.seriesId, volume, false);
  }

  openEditChapter(chapter: Chapter) {
    const ref = this.modalService.open(EditChapterModalComponent, DefaultModalOptions);
    ref.componentInstance.chapter = chapter;
    ref.componentInstance.libraryType = this.libraryType;
    ref.componentInstance.seriesId = this.series?.id;
    ref.componentInstance.libraryId = this.series?.libraryId;

    ref.closed.subscribe((res: EditChapterModalCloseResult) => {
      if (res.success && res.isDeleted) {
        this.loadPageSource.next(false);
      }
    });
  }

  openEditVolume(volume: Volume) {
    const ref = this.modalService.open(EditVolumeModalComponent, DefaultModalOptions);
    ref.componentInstance.volume = volume;
    ref.componentInstance.libraryType = this.libraryType;
    ref.componentInstance.seriesId = this.series?.id;
    ref.componentInstance.libraryId = this.series?.libraryId;

    ref.closed.subscribe((res: EditChapterModalCloseResult) => {
      if (res.success && res.isDeleted) {
        this.loadPageSource.next(false);
      }
    });
  }

  openEditSeriesModal() {
    const modalRef = this.modalService.open(EditSeriesModalComponent, DefaultModalOptions);
    modalRef.componentInstance.series = this.series;
    modalRef.closed.subscribe((closeResult: EditSeriesModalCloseResult) => {
      if (closeResult.success) {
        window.scrollTo(0, 0);
        this.loadPageSource.next(closeResult.updateExternal);
      } else if (closeResult.updateExternal) {
        this.loadPageSource.next(closeResult.updateExternal);
      }
    });
  }

  getUserReview() {
    return this.reviews.filter(r => r.username === this.user?.username && !r.isExternal);
  }

  openReviewModal() {
    const userReview = this.getUserReview();

    const modalRef = this.modalService.open(ReviewSeriesModalComponent, DefaultModalOptions);
    modalRef.componentInstance.series = this.series;
    if (userReview.length > 0) {
      modalRef.componentInstance.review = userReview[0];
    } else {
      modalRef.componentInstance.review = {
        seriesId: this.series.id,
        tagline: '',
        body: ''
      };
    }

    modalRef.closed.subscribe((closeResult) => {
      this.updateOrDeleteReview(closeResult);
    });

  }

  updateOrDeleteReview(closeResult: ReviewSeriesModalCloseEvent) {
    if (closeResult.action === ReviewSeriesModalCloseAction.Close) return;

    const index = this.reviews.findIndex(r => r.username === closeResult.review!.username);
    if (closeResult.action === ReviewSeriesModalCloseAction.Edit) {
      if (index === -1 ) {
        // A new series was added:
        this.reviews = [closeResult.review, ...this.reviews];
        this.cdRef.markForCheck();
        return;
      }
      // An edit occurred
      this.reviews[index] = closeResult.review;
      this.cdRef.markForCheck();
      return;
    }

    if (closeResult.action === ReviewSeriesModalCloseAction.Delete) {
      // An edit occurred
      this.reviews = [...this.reviews.filter(r => r.username !== closeResult.review!.username)];
      this.cdRef.markForCheck();
      return;
    }
  }


  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.series);
    }
  }

  downloadSeries() {
    this.downloadService.download('series', this.series, (d) => {
      this.downloadInProgress = !!d;
      this.cdRef.markForCheck();
    });
  }

  toggleWantToRead() {
    if (this.isWantToRead) {
      this.actionService.removeMultipleSeriesFromWantToReadList([this.series.id]);
    } else {
      this.actionService.addMultipleSeriesToWantToReadList([this.series.id]);
    }

    this.isWantToRead = !this.isWantToRead;
    this.cdRef.markForCheck();
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }


  toggleScrobbling(evt: any) {
    evt.stopPropagation();
    if (this.isScrobbling) {
      this.scrobbleService.addHold(this.series.id).subscribe(() => {
        this.isScrobbling = !this.isScrobbling;
        this.cdRef.markForCheck();
      });
    } else {
      this.scrobbleService.removeHold(this.series.id).subscribe(() => {
        this.isScrobbling = !this.isScrobbling;
        this.cdRef.markForCheck();
      });
    }
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

    protected readonly encodeURIComponent = encodeURIComponent;
}
