import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  signal
} from '@angular/core';
import {ActivatedRoute} from '@angular/router';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {PageBookmark} from 'src/app/_models/readers/page-bookmark';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {FilterEvent, SeriesSortField} from 'src/app/_models/metadata/series-filter';
import {ImageService} from 'src/app/_services/image.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {DecimalPipe} from '@angular/common';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {WikiLink} from "../../../_models/wiki";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {SeriesFilterSettings} from "../../../metadata-filter/filter-settings";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {MetadataService} from "../../../_services/metadata.service";
import {EntityCardComponent} from "../../../cards/entity-card/entity-card.component";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {BookmarkCardEntity, CardEntityFactory} from "../../../_models/card/card-entity";

@Component({
  selector: 'app-bookmarks',
  templateUrl: './bookmarks.component.html',
  styleUrls: ['./bookmarks.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, BulkOperationsComponent, CardDetailLayoutComponent, DecimalPipe, TranslocoDirective, EntityCardComponent]
})
export class BookmarksComponent {

  private readonly readerService = inject(ReaderService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly route = inject(ActivatedRoute);
  private readonly jumpbarService = inject(JumpbarService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly imageService = inject(ImageService);
  public readonly metadataService = inject(MetadataService);
  public readonly destroyRef = inject(DestroyRef);
  public readonly cardConfigFactory = inject(CardConfigFactory);

  protected readonly WikiLink = WikiLink;

  bookmarks = signal<PageBookmark[]>([]);
  bookmarkEntities = computed(() => {
    return this.bookmarks().map(s => CardEntityFactory.bookmark(s));
  });
  series = computed(() => this.bookmarks().map(b => b.series!));
  bookmarkConfig = computed(() => {
    const seriesIds = this.seriesIds();
    return this.cardConfigFactory.forBookmark({overrides: {
        countFunc: entity => seriesIds[entity.seriesId],
      }});
  });

  isLoadingBookmarks = signal<boolean>(false);
  seriesIds = computed(() => {
    const bookmarks = this.bookmarks();
    const seriesIds = {} as {[id: number]: number};

    bookmarks.forEach(bmk => {
      if (!seriesIds.hasOwnProperty(bmk.seriesId)) {
        seriesIds[bmk.seriesId] = 0;
      }
      seriesIds[bmk.seriesId] += 1;
    });
    return seriesIds;
  });
  jumpbarKeys = computed(() => {
    const series = this.series();
    return this.jumpbarService.getJumpKeys(series, (t: Series) => t.name);
  });

  pagination: Pagination = new Pagination();
  filter: FilterV2<SeriesFilterField> | undefined = undefined;
  filterSettings: SeriesFilterSettings = new SeriesFilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: FilterV2<SeriesFilterField>;

  trackByIdentity = (index: number, item: BookmarkCardEntity) => `${item.data.series!.name}_${item.data.seriesId}_${item.data.series!.pagesRead}`;
  refresh: EventEmitter<void> = new EventEmitter();



  constructor() {
    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.filter = data['filter'] as FilterV2<SeriesFilterField, SeriesSortField>;

      if (this.filter == null) {
        this.filter = this.metadataService.createDefaultFilterDto('series');
        this.filter.statements.push(this.metadataService.createDefaultFilterStatement('series') as FilterStatement<SeriesFilterField>);
      }

      this.filterActiveCheck = this.metadataService.createDefaultFilterDto('series');
      this.filterActiveCheck.statements.push(this.metadataService.createDefaultFilterStatement('series') as FilterStatement<SeriesFilterField>);
      this.filterSettings.presetsV2 =  this.filter;
      this.filterSettings.statementLimit = 1;

      this.cdRef.markForCheck();
    });

    this.bulkSelectionService.registerDataSource('bookmark', () => this.series());
    this.bulkSelectionService.registerDataSource('bookmarkData', () => this.bookmarks());
    this.bulkSelectionService.registerPostAction(res => {
      if (res.effect === 'none') return;
      this.loadPage();
    });


  }

  loadPage() {
    this.isLoadingBookmarks.set(true);

    this.readerService.getAllBookmarks(this.filter).subscribe(bookmarks => {
      // Deduplicate: keep first bookmark per series
      const uniqueBySeriesMap = new Map<number, PageBookmark>();
      bookmarks.forEach(bmk => {
        if (!uniqueBySeriesMap.has(bmk.seriesId)) {
          uniqueBySeriesMap.set(bmk.seriesId, bmk);
        }
      });
      const uniqueBookmarks = Array.from(uniqueBySeriesMap.values());

      this.bookmarks.set([...uniqueBookmarks]);
      this.isLoadingBookmarks.set(false);
    });
  }

  // The backend state is already handled by the action service. This just needs to handle the side-effect.
  clearBookmarks(series: Series) {
    // Filter out the bookmark for this series
    this.bookmarks.update(bookmarks =>
      bookmarks.filter(bmk => bmk.seriesId !== series.id)
    );

    this.refresh.emit();
  }

  updateFilter(data: FilterEvent<SeriesFilterField, SeriesSortField>) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      this.loadPage();
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((encodedFilter) => {
      this.loadPage();
    });
  }
}
