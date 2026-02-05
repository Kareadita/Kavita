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
import {ActivatedRoute, Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {DownloadService} from 'src/app/shared/_services/download.service';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {PageBookmark} from 'src/app/_models/readers/page-bookmark';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {FilterEvent, SortField} from 'src/app/_models/metadata/series-filter';
import {Action, ActionItem} from 'src/app/_services/action-factory.service';
import {ImageService} from 'src/app/_services/image.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {DecimalPipe} from '@angular/common';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {Title} from "@angular/platform-browser";
import {WikiLink} from "../../../_models/wiki";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
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

  private readonly translocoService = inject(TranslocoService);
  private readonly readerService = inject(ReaderService);
  private readonly downloadService = inject(DownloadService);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly route = inject(ActivatedRoute);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly titleService = inject(Title);
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
    return this.cardConfigFactory.forBookmark(this.handleAction.bind(this), {
      countFunc: entity => this.seriesIds[entity.seriesId],
    });
  });

  loadingBookmarks: boolean = false;
  seriesIds: {[id: number]: number} = {};
  jumpbarKeys: Array<JumpKey> = [];

  pagination: Pagination = new Pagination();
  filter: FilterV2<FilterField> | undefined = undefined;
  filterSettings: SeriesFilterSettings = new SeriesFilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: FilterV2<FilterField>;

  trackByIdentity = (index: number, item: BookmarkCardEntity) => `${item.data.series!.name}_${item.data.seriesId}_${item.data.series!.pagesRead}`;
  refresh: EventEmitter<void> = new EventEmitter();



  constructor() {

      this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
        this.filter = data['filter'] as FilterV2<FilterField, SortField>;

        if (this.filter == null) {
          this.filter = this.metadataService.createDefaultFilterDto('series');
          this.filter.statements.push(this.metadataService.createDefaultFilterStatement('series') as FilterStatement<FilterField>);
        }

        this.filterActiveCheck = this.metadataService.createDefaultFilterDto('series');
        this.filterActiveCheck.statements.push(this.metadataService.createDefaultFilterStatement('series') as FilterStatement<FilterField>);
        this.filterSettings.presetsV2 =  this.filter;
        this.filterSettings.statementLimit = 1;

        this.cdRef.markForCheck();
      });


      this.titleService.setTitle('Kavita - ' + translate('bookmarks.title'));
    }


  async handleAction(action: ActionItem<PageBookmark>, pageBookmark: PageBookmark) {
    const series = pageBookmark.series;
    if (series == null) return;

    switch (action.action) {
      case(Action.Delete):
        await this.clearBookmarks(series);
        break;
      case(Action.DownloadBookmark):
        this.downloadBookmarks(series);
        break;
      case(Action.ViewSeries):
        await this.router.navigate(['library', series.libraryId, 'series', series.id]);
        break;
      default:
        break;
    }
  }

  bulkActionCallback = async (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('bookmark');
    const selectedSeries = this.series().filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));
    const seriesIds = selectedSeries.map(item => item.id);

    switch (action.action) {
      case Action.DownloadBookmark:
        this.downloadService.download('bookmark', this.bookmarks().filter(bmk => seriesIds.includes(bmk.seriesId)),
          (d) => {
          if (!d) {
            this.bulkSelectionService.deselectAll();
          }
        });
        break;
      case Action.Delete:
        if (!await this.confirmService.confirm(this.translocoService.translate('bookmarks.confirm-delete'))) {
          break;
        }

        this.readerService.clearMultipleBookmarks(seriesIds).subscribe(() => {
          this.toastr.success(this.translocoService.translate('bookmarks.delete-success'));
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      default:
        break;
    }
  }

  loadPage() {
    this.loadingBookmarks = true;
    this.cdRef.markForCheck();

    this.readerService.getAllBookmarks(this.filter).subscribe(bookmarks => {

      // Get the total number of bookmarks per series before deduplication
      bookmarks.forEach(bmk => {
        if (!this.seriesIds.hasOwnProperty(bmk.seriesId)) {
          this.seriesIds[bmk.seriesId] = 0;
        }
        this.seriesIds[bmk.seriesId] += 1;
      });

      // Deduplicate: keep first bookmark per series
      const uniqueBySeriesMap = new Map<number, PageBookmark>();
      bookmarks.forEach(bmk => {
        if (!uniqueBySeriesMap.has(bmk.seriesId)) {
          uniqueBySeriesMap.set(bmk.seriesId, bmk);
        }
      });
      const uniqueBookmarks = Array.from(uniqueBySeriesMap.values());

      this.bookmarks.set(uniqueBookmarks);

      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series(), (t: Series) => t.name);
      this.loadingBookmarks = false;
      this.cdRef.markForCheck();
    });
  }

  // viewBookmarks(series: Series) {
  //   this.router.navigate(['library', series.libraryId, 'series', series.id, 'manga', 0], {queryParams: {incognitoMode: false, bookmarkMode: true}});
  // }

  async clearBookmarks(series: Series) {
    if (!await this.confirmService.confirm(this.translocoService.translate('bookmarks.confirm-single-delete', {seriesName: series.name}))) {
      return;
    }

    this.readerService.clearBookmarks(series.id).subscribe(() => {
      // Filter out the bookmark for this series
      this.bookmarks.update(bookmarks =>
        bookmarks.filter(bmk => bmk.seriesId !== series.id)
      );

      this.toastr.success(this.translocoService.translate('delete-single-success', {seriesName: series.name}));
      this.refresh.emit();
      this.cdRef.markForCheck();
    });
  }

  downloadBookmarks(series: Series) {
    // Find the bookmark for this series
    const bookmark = this.bookmarks().find(bmk => bmk.seriesId === series.id);
    if (bookmark) {
      this.downloadService.download('bookmark', [bookmark]);
    }
  }

  updateFilter(data: FilterEvent<FilterField, SortField>) {
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
