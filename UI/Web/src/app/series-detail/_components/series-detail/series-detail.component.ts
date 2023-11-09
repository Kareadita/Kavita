import {DecimalPipe, DOCUMENT, NgFor, NgIf, NgStyle, NgSwitch, NgSwitchCase, NgTemplateOutlet} from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  Inject,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {Title} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
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
  NgbOffcanvas,
  NgbProgressbar,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {catchError, forkJoin, of} from 'rxjs';
import {take} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {CardDetailDrawerComponent} from 'src/app/cards/card-detail-drawer/card-detail-drawer.component';
import {EditSeriesModalComponent} from 'src/app/cards/_modals/edit-series-modal/edit-series-modal.component';
import {TagBadgeCursor} from 'src/app/shared/tag-badge/tag-badge.component';
import {DownloadService} from 'src/app/shared/_services/download.service';
import {KEY_CODES, UtilityService} from 'src/app/shared/_services/utility.service';
import {Chapter} from 'src/app/_models/chapter';
import {Device} from 'src/app/_models/device/device';
import {ScanSeriesEvent} from 'src/app/_models/events/scan-series-event';
import {SeriesRemovedEvent} from 'src/app/_models/events/series-removed-event';
import {LibraryType} from 'src/app/_models/library';
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
import {DeviceService} from 'src/app/_services/device.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {NavService} from 'src/app/_services/nav.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {SeriesService} from 'src/app/_services/series.service';
import {ReviewSeriesModalComponent} from '../../../_single-module/review-series-modal/review-series-modal.component';
import {PageLayoutMode} from 'src/app/_models/page-layout-mode';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {UserReview} from "../../../_single-module/review-card/user-review";
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {ExternalListItemComponent} from '../../../cards/external-list-item/external-list-item.component';
import {ExternalSeriesCardComponent} from '../../../cards/external-series-card/external-series-card.component';
import {SeriesCardComponent} from '../../../cards/series-card/series-card.component';
import {EntityTitleComponent} from '../../../cards/entity-title/entity-title.component';
import {ListItemComponent} from '../../../cards/list-item/list-item.component';
import {CardItemComponent} from '../../../cards/card-item/card-item.component';
import {VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {ReviewCardComponent} from '../../../_single-module/review-card/review-card.component';
import {CarouselReelComponent} from '../../../carousel/_components/carousel-reel/carousel-reel.component';
import {SeriesMetadataDetailComponent} from '../series-metadata-detail/series-metadata-detail.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {TagBadgeComponent} from '../../../shared/tag-badge/tag-badge.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {ExternalSeries} from "../../../_models/series-detail/external-series";
import {
  SeriesPreviewDrawerComponent
} from "../../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {PublicationStatus} from "../../../_models/metadata/publication-status";
import {NextExpectedChapter} from "../../../_models/series-detail/next-expected-chapter";
import {NextExpectedCardComponent} from "../../../cards/next-expected-card/next-expected-card.component";

interface RelatedSeriesPair {
  series: Series;
  relation: RelationKind;
}

enum TabID {
  Related = 0,
  Specials = 1,
  Storyline = 2,
  Volumes = 3,
  Chapters = 4,
  Recommendations = 5
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
  imports: [NgIf, SideNavCompanionBarComponent, CardActionablesComponent, ReactiveFormsModule, NgStyle, TagBadgeComponent, ImageComponent, NgbTooltip, NgbProgressbar, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem, SeriesMetadataDetailComponent, CarouselReelComponent, ReviewCardComponent, BulkOperationsComponent, NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, VirtualScrollerModule, NgFor, CardItemComponent, ListItemComponent, EntityTitleComponent, SeriesCardComponent, ExternalSeriesCardComponent, ExternalListItemComponent, NgbNavOutlet, LoadingComponent, DecimalPipe, TranslocoDirective, NgTemplateOutlet, NgSwitch, NgSwitchCase, NextExpectedCardComponent]
})
export class SeriesDetailComponent implements OnInit, AfterContentChecked {

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;
  private readonly destroyRef = inject(DestroyRef);

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
  showBook = true;

  currentlyReadingChapter: Chapter | undefined = undefined;
  hasReadingProgress = false;


  seriesActions: ActionItem<Series>[] = [];
  volumeActions: ActionItem<Volume>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];

  hasSpecials = false;
  specials: Array<Chapter> = [];
  activeTabId = TabID.Storyline;

  reviews: Array<UserReview> = [];
  libraryType: LibraryType = LibraryType.Manga;
  seriesMetadata: SeriesMetadata | null = null;
  readingLists: Array<ReadingList> = [];
  isWantToRead: boolean = false;
  unreadCount: number = 0;
  totalCount: number = 0;
  /**
   * Poster image for the Series
   */
  seriesImage: string = '';
  downloadInProgress: boolean = false;

  nextExpectedChapter: NextExpectedChapter | undefined;

  /**
   * Track by function for Volume to tell when to refresh card data
   */
  trackByVolumeIdentity = (index: number, item: Volume) => `${item.name}_${item.pagesRead}`;
  /**
   * Track by function for Chapter to tell when to refresh card data
   */
  trackByChapterIdentity = (index: number, item: Chapter) => `${item.title}_${item.number}_${item.volumeId}_${item.pagesRead}`;
  trackByRelatedSeriesIdentify = (index: number, item: RelatedSeriesPair) => `${item.series.name}_${item.series.libraryId}_${item.series.pagesRead}_${item.relation}`;
  trackBySeriesIdentify = (index: number, item: Series) => `${item.name}_${item.libraryId}_${item.pagesRead}`;
  trackByStoryLineIdentity = (index: number, item: StoryLineItem) => {
    if (item.isChapter) {
      return this.trackByChapterIdentity(index, item!.chapter!)
    }
    return this.trackByVolumeIdentity(index, item!.volume!);
  };

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

  bulkActionCallback = (action: ActionItem<any>, data: any) => {
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
    const chapterIndexModifier = this.activeTabId === TabID.Storyline ? this.volumes.length + 1 : 0;
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
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });

        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleAsUnread(seriesId, selectedVolumeIds, chapters,  () => {
          this.setContinuePoint();
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
    }
  }

  get TagBadgeCursor() { return TagBadgeCursor; }

  get TabID() { return TabID; }

  get PageLayoutMode() { return PageLayoutMode; }

  get LibraryType() { return LibraryType; }

  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar!.nativeElement.offsetHeight;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  get ContinuePointTitle() {
    if (this.currentlyReadingChapter === undefined || !this.hasReadingProgress) return '';

    if (!this.currentlyReadingChapter.isSpecial) {
      const vol = this.volumes.filter(v => v.id === this.currentlyReadingChapter?.volumeId);

      // This is a lone chapter
      if (vol.length === 0) {
        return 'Ch ' + this.currentlyReadingChapter.number;
      }

      if (this.currentlyReadingChapter.number === "0") {
        return 'Vol ' + vol[0].number;
      }
      return 'Vol ' + vol[0].number + ' Ch ' + this.currentlyReadingChapter.number;
    }

    return this.currentlyReadingChapter.title;
  }

  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              private router: Router, public bulkSelectionService: BulkSelectionService,
              private modalService: NgbModal, public readerService: ReaderService,
              public utilityService: UtilityService, private toastr: ToastrService,
              private accountService: AccountService, public imageService: ImageService,
              private actionFactoryService: ActionFactoryService, private libraryService: LibraryService,
              private titleService: Title,
              private downloadService: DownloadService, private actionService: ActionService,
              private messageHub: MessageHubService, private readingListService: ReadingListService,
              public navService: NavService,
              private offcanvasService: NgbOffcanvas, @Inject(DOCUMENT) private document: Document,
              private cdRef: ChangeDetectorRef, private scrollService: ScrollService,
              private deviceService: DeviceService, private translocoService: TranslocoService
              ) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
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

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemovedEvent = event.payload as SeriesRemovedEvent;
        if (seriesRemovedEvent.seriesId === this.seriesId) {
          this.toastr.info(this.translocoService.translate('errors.series-doesnt-exist'));
          this.router.navigateByUrl('/home');
        }
      } else if (event.event === EVENTS.ScanSeries) {
        const seriesCoverUpdatedEvent = event.payload as ScanSeriesEvent;
        if (seriesCoverUpdatedEvent.seriesId === this.seriesId) {
          this.loadSeries(this.seriesId);
        }
      }
    });

    this.seriesId = parseInt(routeId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.seriesImage = this.imageService.getSeriesCoverImage(this.seriesId);
    this.cdRef.markForCheck();
    this.loadSeries(this.seriesId, true);

    this.pageExtrasGroup.get('renderMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((val: PageLayoutMode | null) => {
      if (val == null) return;
      this.renderMode = val;
      this.cdRef.markForCheck();
    });
  }

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }

  onNavChange(event: NgbNavChangeEvent) {
    this.bulkSelectionService.deselectAll();
    this.cdRef.markForCheck();
  }

  handleSeriesActionCallback(action: ActionItem<Series>, series: Series) {
    this.cdRef.markForCheck();
    switch(action.action) {
      case(Action.MarkAsRead):
        this.actionService.markSeriesAsRead(series, (series: Series) => {
          this.loadSeries(series.id);
        });
        break;
      case(Action.MarkAsUnread):
        this.actionService.markSeriesAsUnread(series, (series: Series) => {
          this.loadSeries(series.id);
        });
        break;
      case(Action.Scan):
        this.actionService.scanSeries(series);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshMetdata(series);
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

  handleVolumeActionCallback(action: ActionItem<Volume>, volume: Volume) {
    switch(action.action) {
      case(Action.MarkAsRead):
        this.markVolumeAsRead(volume);
        break;
      case(Action.MarkAsUnread):
        this.markVolumeAsUnread(volume);
        break;
      case(Action.Edit):
        this.openViewInfo(volume);
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
        this.openViewInfo(chapter);
        break;
      case(Action.AddToReadingList):
        this.actionService.addChapterToReadingList(chapter, this.seriesId, () => {/* No Operation */ });
        break;
      case(Action.IncognitoRead):
        this.openChapter(chapter, true);
        break;
      case (Action.SendTo):
        {
          const device = (action._extra!.data as Device);
          this.deviceService.sendTo([chapter.id], device.id).subscribe(() => {
            this.toastr.success(this.translocoService.translate('series-detail.send-to', {deviceName: device.name}));
          });
          break;
        }
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
      this.seriesMetadata = metadata;
      this.cdRef.markForCheck();

      if (![PublicationStatus.Ended, PublicationStatus.OnGoing].includes(this.seriesMetadata.publicationStatus)) return;
      this.seriesService.getNextExpectedChapterDate(seriesId).subscribe(date => {
        if (date == null || date.expectedDate === null) return;

        this.nextExpectedChapter = date;
        this.cdRef.markForCheck();
      })
    });

    this.seriesService.isWantToRead(seriesId).subscribe(isWantToRead => {
      this.isWantToRead = isWantToRead;
      this.cdRef.markForCheck();
    });

    this.readingListService.getReadingListsForSeries(seriesId).subscribe(lists => {
      this.readingLists = lists;
      this.cdRef.markForCheck();
    });
    this.setContinuePoint();

    forkJoin({
      libType: this.libraryService.getLibraryType(this.libraryId),
      series: this.seriesService.getSeries(seriesId)
    }).subscribe(results => {
      this.libraryType = results.libType;
      this.series = results.series;

      if (this.libraryType !== LibraryType.Comic && loadExternal) {
        this.loadReviews(true);
      }

      this.titleService.setTitle('Kavita - ' + this.series.name + ' Details');

      this.seriesActions = this.actionFactoryService.getSeriesActions(this.handleSeriesActionCallback.bind(this))
              .filter(action => action.action !== Action.Edit);

      this.volumeActions = this.actionFactoryService.getVolumeActions(this.handleVolumeActionCallback.bind(this));
      this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this));



      this.seriesService.getRelatedForSeries(this.seriesId).subscribe((relations: RelatedSeries) => {
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
        if (detail == null) return;
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


        this.updateSelectedTab();
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

  /**
   * This will update the selected tab
   *
   * This assumes loadPage() has already primed all the calculations and state variables. Do not call directly.
   */
  updateSelectedTab() {
    // Book libraries only have Volumes or Specials enabled
    if (this.libraryType === LibraryType.Book) {
      if (this.volumes.length === 0) {
        this.activeTabId = TabID.Specials;
      } else {
        this.activeTabId = TabID.Volumes;
      }
      return;
    }

    if (this.volumes.length === 0 && this.chapters.length === 0 && this.specials.length > 0) {
      this.activeTabId = TabID.Specials;
    } else {
      this.activeTabId = TabID.Storyline;
    }
  }

  loadRecommendations() {
    this.seriesService.getRecommendationsForSeries(this.seriesId).subscribe(rec => {
      rec.ownedSeries.map(r => {
        this.seriesService.getMetadata(r.id).subscribe(m => r.summary = m.summary);
      });
      this.combinedRecs = [...rec.ownedSeries, ...rec.externalSeries];
      this.hasRecommendations = this.combinedRecs.length > 0;
      this.cdRef.markForCheck();
    });
  }

  loadReviews(loadRecs: boolean = false) {
    this.seriesService.getReviews(this.seriesId).subscribe(reviews => {
      this.reviews = [...reviews];
      if (loadRecs) {
        this.loadRecommendations(); // We do this as first load will spam 3 calls on API layer
      }
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
    if (chapter.pages === 0) {
      this.toastr.error(this.translocoService.translate('series-detail.no-pages'));
      return;
    }
    this.router.navigate(this.readerService.getNavigationArray(this.libraryId, this.seriesId, chapter.id, chapter.files[0].format), {queryParams: {incognitoMode}});
  }

  openVolume(volume: Volume) {
    if (this.bulkSelectionService.hasSelections()) return;
    if (volume.chapters === undefined || volume.chapters?.length === 0) {
      this.toastr.error(this.translocoService.translate('series-detail.no-chapters'));
      return;
    }

    // If user has progress on the volume, load them where they left off
    if (volume.pagesRead < volume.pages && volume.pagesRead > 0) {
      // Find the continue point chapter and load it
      const unreadChapters = volume.chapters.filter(item => item.pagesRead < item.pages);
      if (unreadChapters.length > 0) {
        this.openChapter(unreadChapters[0]);
        return;
      }
      this.openChapter(volume.chapters[0]);
      return;
    }

    // Sort the chapters, then grab first if no reading progress
    this.openChapter([...volume.chapters].sort(this.utilityService.sortChapters)[0]);
  }

  openViewInfo(data: Volume | Chapter) {
    const drawerRef = this.offcanvasService.open(CardDetailDrawerComponent, {position: 'bottom'});
    drawerRef.componentInstance.data = data;
    drawerRef.componentInstance.parentName = this.series?.name;
    drawerRef.componentInstance.seriesId = this.series?.id;
    drawerRef.componentInstance.libraryId = this.series?.libraryId;
  }

  openEditSeriesModal() {
    const modalRef = this.modalService.open(EditSeriesModalComponent, {  size: 'xl' });
    modalRef.componentInstance.series = this.series;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series, coverImageUpdate: boolean}) => {
      if (closeResult.success) {
        window.scrollTo(0, 0);
        this.loadSeries(this.seriesId);
      }

      if (closeResult.coverImageUpdate) {
        this.toastr.info(this.translocoService.translate('series-detail.cover-change'));
      }
    });
  }

  getUserReview() {
    return this.reviews.filter(r => r.username === this.user?.username && !r.isExternal);
  }

  openReviewModal() {
    const userReview = this.getUserReview();

    const modalRef = this.modalService.open(ReviewSeriesModalComponent, { scrollable: true, size: 'lg' });
    if (userReview.length > 0) {
      modalRef.componentInstance.review = userReview[0];
    } else {
      modalRef.componentInstance.review = {
        seriesId: this.series.id,
        tagline: '',
        body: ''
      };
    }
    modalRef.componentInstance.series = this.series;
    modalRef.closed.subscribe((closeResult: {success: boolean}) => {
      if (closeResult.success) {
        this.loadReviews();
      }
    });
  }


  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.series);
    }
  }

  downloadSeries() {
    this.downloadService.download('series', this.series, (d) => {
      if (d) {
        this.downloadInProgress = true;
      } else {
        this.downloadInProgress = false;
      }
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

  previewSeries(item: Series | ExternalSeries, isExternal: boolean) {
    const ref = this.offcanvasService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: 'navbar-offset'});
    ref.componentInstance.isExternalSeries = isExternal;
    ref.componentInstance.name = item.name;

    if (isExternal) {
      const external = item as ExternalSeries;
      ref.componentInstance.aniListId = external.aniListId;
      ref.componentInstance.malId = external.malId;
    } else {
      const local = item as Series;
      ref.componentInstance.seriesId = local.id;
      ref.componentInstance.libraryId = local.libraryId;
    }

  }


}
