import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {AsyncPipe, DecimalPipe, DOCUMENT, NgStyle, NgClass} from "@angular/common";
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {ImageService} from "../_services/image.service";
import {SeriesService} from "../_services/series.service";
import {LibraryService} from "../_services/library.service";
import {ThemeService} from "../_services/theme.service";
import {DownloadEvent, DownloadService} from "../shared/_services/download.service";
import {BulkSelectionService} from "../cards/bulk-selection.service";
import {ToastrService} from "ngx-toastr";
import {ReaderService} from "../_services/reader.service";
import {AccountService} from "../_services/account.service";
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
  NgbProgressbar,
  NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {Chapter} from "../_models/chapter";
import {Series} from "../_models/series";
import {LibraryType} from "../_models/library/library";
import {forkJoin, map, Observable, shareReplay, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {EditChapterModalComponent} from "../_single-module/edit-chapter-modal/edit-chapter-modal.component";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterField} from '../_models/metadata/v2/filter-field';
import {AgeRating} from '../_models/metadata/age-rating';
import {Volume} from "../_models/volume";
import {VolumeService} from "../_services/volume.service";
import {LoadingComponent} from "../shared/loading/loading.component";
import {DetailsTabComponent} from "../_single-module/details-tab/details-tab.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {Person} from "../_models/metadata/person";
import {hasAnyCast, IHasCast} from "../_models/common/i-has-cast";
import {ReadTimePipe} from "../_pipes/read-time.pipe";
import {AgeRatingPipe} from "../_pipes/age-rating.pipe";
import {EntityTitleComponent} from "../cards/entity-title/entity-title.component";
import {ImageComponent} from "../shared/image/image.component";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {Action, ActionFactoryService, ActionItem} from "../_services/action-factory.service";
import {UtilityService} from "../shared/_services/utility.service";
import {ChapterCardComponent} from "../cards/chapter-card/chapter-card.component";
import {DefaultValuePipe} from "../_pipes/default-value.pipe";
import {
  EditVolumeModalCloseResult,
  EditVolumeModalComponent
} from "../_single-module/edit-volume-modal/edit-volume-modal.component";
import {Genre} from "../_models/metadata/genre";
import {Tag} from "../_models/tag";
import {RelatedTabComponent} from "../_single-modules/related-tab/related-tab.component";
import {ReadingList} from "../_models/reading-list";
import {ReadingListService} from "../_services/reading-list.service";
import {AgeRatingImageComponent} from "../_single-modules/age-rating-image/age-rating-image.component";
import {CompactNumberPipe} from "../_pipes/compact-number.pipe";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {
  MetadataDetailRowComponent
} from "../series-detail/_components/metadata-detail-row/metadata-detail-row.component";
import {DownloadButtonComponent} from "../series-detail/_components/download-button/download-button.component";

enum TabID {

  Chapters = 'chapters-tab',
  Related = 'related-tab',
  Reviews = 'reviews-tab', // Only applicable for books
  Details = 'details-tab',
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
  standalone: true,
  imports: [
    LoadingComponent,
    NgbNavOutlet,
    DetailsTabComponent,
    NgbNavItem,
    NgbNavLink,
    NgbNavContent,
    NgbNav,
    ReadMoreComponent,
    AsyncPipe,
    NgbDropdownItem,
    NgbDropdownMenu,
    NgbDropdown,
    NgbDropdownToggle,
    ReadTimePipe,
    AgeRatingPipe,
    EntityTitleComponent,
    RouterLink,
    NgbProgressbar,
    DecimalPipe,
    NgbTooltip,
    ImageComponent,
    NgStyle,
    NgClass,
    TranslocoDirective,
    CardItemComponent,
    VirtualScrollerModule,
    ChapterCardComponent,
    DefaultValuePipe,
    RelatedTabComponent,
    AgeRatingImageComponent,
    CompactNumberPipe,
    BadgeExpanderComponent,
    MetadataDetailRowComponent,
    DownloadButtonComponent
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
  private readonly modalService = inject(NgbModal);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  protected readonly utilityService = inject(UtilityService);
  private readonly readingListService = inject(ReadingListService);


  protected readonly AgeRating = AgeRating;
  protected readonly TabID = TabID;
  protected readonly FilterField = FilterField;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  isLoading: boolean = true;
  coverImage: string = '';
  volumeId: number = 0;
  seriesId: number = 0;
  libraryId: number = 0;
  volume: Volume | null = null;
  series: Series | null = null;
  libraryType: LibraryType | null = null;
  hasReadingProgress = false;
  activeTabId = TabID.Chapters;
  readingLists: ReadingList[] = [];
  mobileSeriesImgBackground: string | undefined;

  volumeActions: Array<ActionItem<Chapter>> = this.actionFactoryService.getVolumeActions(this.handleVolumeAction.bind(this));

  /**
   * This is the download we get from download service.
   */
  download$: Observable<DownloadEvent | null> | null = null;
  showDetailsTab: boolean = true;

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

  get UseBookLogic() {
    return this.libraryType === LibraryType.Book || this.libraryType === LibraryType.LightNovel;
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
    this.seriesId = parseInt(seriesId, 10);
    this.volumeId = parseInt(volumeId, 10);
    this.libraryId = parseInt(libraryId, 10);

    this.coverImage = this.imageService.getVolumeCoverImage(this.volumeId);



    forkJoin({
      series: this.seriesService.getSeries(this.seriesId),
      volume: this.volumeService.getVolumeMetadata(this.volumeId),
      libraryType: this.libraryService.getLibraryType(this.libraryId)
    }).subscribe(results => {

      if (results.volume === null) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.series = results.series;
      this.volume = results.volume;
      this.libraryType = results.libraryType;

      this.themeService.setColorScape(this.volume!.primaryColor, this.volume!.secondaryColor);

      // Set up the download in progress
      this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
        return this.downloadService.mapToEntityType(events, this.volume!);
      }));

      this.route.fragment.pipe(tap(frag => {
        if (frag !== null && this.activeTabId !== (frag as TabID)) {
          this.activeTabId = frag as TabID;
          this.updateUrl(this.activeTabId);
          this.cdRef.markForCheck();
        }
      }), takeUntilDestroyed(this.destroyRef)).subscribe();

      if (this.volume.chapters.length === 1) {
        this.readingListService.getReadingListsForChapter(this.volume.chapters[0].id).subscribe(lists => {
          this.readingLists = lists;
          this.cdRef.markForCheck();
        });
      }

      // Calculate all the writes/artists for all chapters
      this.volumeCast.writers = this.volume.chapters
        .flatMap(c => c.writers)  // Flatten the array of writers from all chapters
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name) // Check for distinct names
        );

      this.volumeCast.coverArtists = this.volume.chapters
        .flatMap(c => c.coverArtists)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );

      this.volumeCast.characters = this.volume.chapters
        .flatMap(c => c.characters)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );
      this.volumeCast.colorists = this.volume.chapters
        .flatMap(c => c.colorists)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );
      this.volumeCast.editors = this.volume.chapters
        .flatMap(c => c.editors)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );
      this.volumeCast.imprints = this.volume.chapters
        .flatMap(c => c.imprints)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );
      this.volumeCast.inkers = this.volume.chapters
        .flatMap(c => c.inkers)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );
      this.volumeCast.letterers = this.volume.chapters
        .flatMap(c => c.letterers)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );
      this.volumeCast.locations = this.volume.chapters
        .flatMap(c => c.locations)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );

      this.volumeCast.teams = this.volume.chapters
        .flatMap(c => c.teams)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );

      this.volumeCast.translators = this.volume.chapters
        .flatMap(c => c.translators)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );

      this.volumeCast.publishers = this.volume.chapters
        .flatMap(c => c.publishers)
        .filter((person, index, self) =>
          index === self.findIndex(w => w.name === person.name)
        );

      this.genres = this.volume.chapters
        .flatMap(c => c.genres)
        .filter((tag, index, self) =>
          index === self.findIndex(w => w.title === tag.title)
        );

      this.tags = this.volume.chapters
        .flatMap(c => c.tags)
        .filter((tag, index, self) =>
          index === self.findIndex(w => w.title === tag.title)
        );

      this.maxAgeRating = Math.max(
        ...this.volume.chapters
          .flatMap(c => c.ageRating)
      );

      this.showDetailsTab = hasAnyCast(this.volumeCast) || (this.genres || []).length > 0 || (this.tags || []).length > 0;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });

    this.cdRef.markForCheck();
  }

  readChapter(chapter: Chapter, incognitoMode: boolean = false) {
    if (this.bulkSelectionService.hasSelections()) return;

    this.readerService.readChapter(this.libraryId, this.seriesId, chapter, incognitoMode);
  }

  readVolume(incognitoMode: boolean = false) {
    if (!this.volume) return;

    this.readerService.readVolume(this.libraryId, this.seriesId, this.volume, incognitoMode);
  }

  openEditModal() {
    const ref = this.modalService.open(EditVolumeModalComponent, { size: 'xl' });
    ref.componentInstance.volume = this.volume;
    ref.componentInstance.libraryType = this.libraryType;
    ref.componentInstance.libraryId = this.libraryId;
    ref.componentInstance.seriesId = this.series!.id;

    ref.closed.subscribe((res: EditVolumeModalCloseResult) => {
      // TODO
    });
  }

  onNavChange(event: NgbNavChangeEvent) {
    this.bulkSelectionService.deselectAll();
    this.updateUrl(event.nextId);
    this.cdRef.markForCheck();
  }

  updateUrl(activeTab: TabID) {
    const newUrl = `${this.router.url.split('#')[0]}#${activeTab}`;
    //this.router.navigateByUrl(newUrl, { onSameUrlNavigation: 'ignore' });
  }

  openPerson(field: FilterField, value: number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }

  handleVolumeAction(action: ActionItem<Volume>) {
    // TODO: Implement actionables
    switch (action.action) {
      case Action.Delete:
        break;
      case Action.MarkAsRead:
        break;
      case Action.MarkAsUnread:
        break;
      case Action.MarkAsRead:
        break;
    }
  }

}
