import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {TagBadgeComponent, TagBadgeCursor} from "../shared/tag-badge/tag-badge.component";
import {PageLayoutMode} from "../_models/page-layout-mode";
import {AsyncPipe, DecimalPipe, DOCUMENT, NgStyle} from "@angular/common";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {CarouselReelComponent} from "../carousel/_components/carousel-reel/carousel-reel.component";
import {ExternalListItemComponent} from "../cards/external-list-item/external-list-item.component";
import {ExternalSeriesCardComponent} from "../cards/external-series-card/external-series-card.component";
import {ImageComponent} from "../shared/image/image.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {
  NgbDropdown,
  NgbDropdownItem,
  NgbDropdownMenu,
  NgbDropdownToggle,
  NgbNav,
  NgbNavContent,
  NgbNavLink,
  NgbProgressbar,
  NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {PersonBadgeComponent} from "../shared/person-badge/person-badge.component";
import {ReviewCardComponent} from "../_single-module/review-card/review-card.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {
  SeriesMetadataDetailComponent
} from "../series-detail/_components/series-metadata-detail/series-metadata-detail.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {ActivatedRoute, Router} from "@angular/router";
import {ImageService} from "../_services/image.service";
import {ChapterService} from "../_services/chapter.service";
import {Chapter} from "../_models/chapter";
import {forkJoin} from "rxjs";
import {SeriesService} from "../_services/series.service";
import {Series} from "../_models/series";
import {AgeRating} from "../_models/metadata/age-rating";
import {AgeRatingPipe} from "../_pipes/age-rating.pipe";
import {HourEstimateRange} from "../_models/series-detail/hour-estimate-range";
import {TimeDurationPipe} from "../_pipes/time-duration.pipe";
import {ExternalRatingComponent} from "../series-detail/_components/external-rating/external-rating.component";
import {LibraryType} from "../_models/library/library";
import {LibraryService} from "../_services/library.service";
import {ThemeService} from "../_services/theme.service";

@Component({
  selector: 'app-chapter-detail',
  standalone: true,
  imports: [
    BulkOperationsComponent,
    AsyncPipe,
    CardActionablesComponent,
    CarouselReelComponent,
    DecimalPipe,
    ExternalListItemComponent,
    ExternalSeriesCardComponent,
    ImageComponent,
    LoadingComponent,
    NgbDropdown,
    NgbDropdownItem,
    NgbDropdownMenu,
    NgbDropdownToggle,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    NgbProgressbar,
    NgbTooltip,
    PersonBadgeComponent,
    ReviewCardComponent,
    SeriesCardComponent,
    SeriesMetadataDetailComponent,
    TagBadgeComponent,
    VirtualScrollerModule,
    NgStyle,
    AgeRatingPipe,
    TimeDurationPipe,
    ExternalRatingComponent
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
  private readonly imageService = inject(ImageService);
  private readonly chapterService = inject(ChapterService);
  private readonly seriesService = inject(SeriesService);
  protected readonly libraryService = inject(LibraryService);
  protected readonly themeService = inject(ThemeService);


  protected readonly TagBadgeCursor = TagBadgeCursor;
  protected readonly PageLayoutMode = PageLayoutMode;
  protected readonly AgeRating = AgeRating;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  isLoading: boolean = true;
  coverImage: string = '';
  chapterId: number = 0;
  seriesId: number = 0;
  chapter: Chapter | null = null;
  series: Series | null = null;
  libraryType: LibraryType | null = null;



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

    this.seriesId = parseInt(seriesId, 10);
    this.chapterId = parseInt(chapterId, 10);

    forkJoin({
      series: this.seriesService.getSeries(this.seriesId),
      chapter: this.chapterService.getChapterMetadata(this.chapterId),
      libraryType: this.libraryService.getLibraryType(parseInt(libraryId, 10))
    }).subscribe(results => {
      this.series = results.series;
      this.chapter = results.chapter;
      this.libraryType = results.libraryType;


      this.themeService.setColorScape(this.chapter.primaryColor, this.chapter.secondaryColor);

      this.isLoading = false;
      this.cdRef.markForCheck();
    });

    this.chapterService.getChapterMetadata(this.chapterId).subscribe(metadata => {
      this.chapter = metadata;
      this.isLoading = false;
      console.log('chapter metadata: ', this.chapter);
      this.cdRef.markForCheck();
    });

    this.coverImage = this.imageService.getChapterCoverImage(this.chapterId);
    this.cdRef.markForCheck();
  }
}
