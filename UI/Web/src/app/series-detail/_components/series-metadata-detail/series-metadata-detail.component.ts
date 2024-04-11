import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  inject,
  Input,
  OnChanges, OnInit,
  SimpleChanges,
  ViewEncapsulation
} from '@angular/core';
import {Router} from '@angular/router';
import {TagBadgeComponent, TagBadgeCursor} from '../../../shared/tag-badge/tag-badge.component';
import {FilterUtilitiesService} from '../../../shared/_services/filter-utilities.service';
import {Breakpoint, UtilityService} from '../../../shared/_services/utility.service';
import {MangaFormat} from '../../../_models/manga-format';
import {ReadingList} from '../../../_models/reading-list';
import {Series} from '../../../_models/series';
import {SeriesMetadata} from '../../../_models/metadata/series-metadata';
import {ImageService} from 'src/app/_services/image.service';
import {CommonModule} from "@angular/common";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {ExternalRatingComponent} from "../external-rating/external-rating.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {PersonBadgeComponent} from "../../../shared/person-badge/person-badge.component";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {SeriesInfoCardsComponent} from "../../../cards/series-info-cards/series-info-cards.component";
import {LibraryType} from "../../../_models/library/library";
import {MetadataDetailComponent} from "../metadata-detail/metadata-detail.component";
import {TranslocoDirective} from "@ngneat/transloco";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {ImageComponent} from "../../../shared/image/image.component";
import {Rating} from "../../../_models/rating";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {shareReplay} from "rxjs/operators";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {Observable} from "rxjs";
import {UserCollection} from "../../../_models/collection-tag";


@Component({
  selector: 'app-series-metadata-detail',
  standalone: true,
  imports: [CommonModule, TagBadgeComponent, BadgeExpanderComponent, SafeHtmlPipe, ExternalRatingComponent,
    ReadMoreComponent, A11yClickDirective, PersonBadgeComponent, NgbCollapse, SeriesInfoCardsComponent,
    MetadataDetailComponent, TranslocoDirective, ImageComponent, PromotedIconComponent],
  templateUrl: './series-metadata-detail.component.html',
  styleUrls: ['./series-metadata-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class SeriesMetadataDetailComponent implements OnChanges, OnInit {

  protected readonly imageService = inject(ImageService);
  protected readonly utilityService = inject(UtilityService);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly FilterField = FilterField;
  protected readonly LibraryType = LibraryType;
  protected readonly MangaFormat = MangaFormat;
  protected readonly TagBadgeCursor = TagBadgeCursor;
  protected readonly Breakpoint = Breakpoint;

  @Input({required: true}) seriesMetadata!: SeriesMetadata;
  @Input({required: true}) libraryType!: LibraryType;
  @Input() hasReadingProgress: boolean = false;
  /**
   * Reading lists with a connection to the Series
   */
  @Input() readingLists: Array<ReadingList> = [];
  @Input({required: true}) series!: Series;
  @Input({required: true}) ratings: Array<Rating> = [];

  isCollapsed: boolean = true;
  hasExtendedProperties: boolean = false;

  /**
   * Html representation of Series Summary
   */
  seriesSummary: string = '';
  collections$: Observable<UserCollection[]> | undefined;

  get WebLinks() {
    if (this.seriesMetadata?.webLinks === '') return [];
    return this.seriesMetadata?.webLinks.split(',') || [];
  }

  ngOnInit() {
    // If on desktop, we can just have all the data expanded by default:
    this.isCollapsed = this.utilityService.getActiveBreakpoint() < Breakpoint.Desktop;
    // Check if there is a lot of extended data, if so, re-collapse
    const sum = (this.seriesMetadata.colorists.length + this.seriesMetadata.editors.length
      + this.seriesMetadata.coverArtists.length + this.seriesMetadata.inkers.length
      + this.seriesMetadata.letterers.length + this.seriesMetadata.pencillers.length
      + this.seriesMetadata.publishers.length + this.seriesMetadata.characters.length
      + this.seriesMetadata.imprints.length + this.seriesMetadata.translators.length
      + this.seriesMetadata.writers.length + this.seriesMetadata.teams.length + this.seriesMetadata.locations.length) / 13;
    if (sum > 10) {
      this.isCollapsed = true;
    }

    this.collections$ = this.collectionTagService.allCollectionsForSeries(this.series.id).pipe(
      takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
    this.cdRef.markForCheck();


  }

  ngOnChanges(changes: SimpleChanges): void {
    this.hasExtendedProperties = this.seriesMetadata.colorists.length > 0 ||
                                  this.seriesMetadata.editors.length > 0 ||
                                  this.seriesMetadata.coverArtists.length > 0 ||
                                  this.seriesMetadata.inkers.length > 0 ||
                                  this.seriesMetadata.letterers.length > 0 ||
                                  this.seriesMetadata.pencillers.length > 0 ||
                                  this.seriesMetadata.publishers.length > 0 ||
                                  this.seriesMetadata.characters.length > 0 ||
                                  this.seriesMetadata.imprints.length > 0 ||
                                  this.seriesMetadata.teams.length > 0 ||
                                  this.seriesMetadata.locations.length > 0 ||
                                  this.seriesMetadata.translators.length > 0
    ;


    this.seriesSummary = (this.seriesMetadata?.summary === null ? '' : this.seriesMetadata.summary).replace(/\n/g, '<br>');
    this.cdRef.markForCheck();
  }

  toggleView() {
    this.isCollapsed = !this.isCollapsed;
    this.cdRef.markForCheck();
  }

  handleGoTo(event: {queryParamName: FilterField, filter: any}) {
    this.goTo(event.queryParamName, event.filter);
  }

  goTo(queryParamName: FilterField, filter: any) {
    this.filterUtilityService.applyFilter(['library', this.series.libraryId], queryParamName,
      FilterComparison.Equal, filter).subscribe();
  }

  navigate(basePage: string, id: number) {
    this.router.navigate([basePage, id]);
  }
}
