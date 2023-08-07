import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  HostListener,
  inject,
  OnInit
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs';
import { BulkSelectionService } from 'src/app/cards/bulk-selection.service';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { KEY_CODES } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { PageBookmark } from 'src/app/_models/readers/page-bookmark';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { FilterEvent, SeriesFilter } from 'src/app/_models/metadata/series-filter';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { DecimalPipe } from '@angular/common';
import { CardItemComponent } from '../../../cards/card-item/card-item.component';
import { CardDetailLayoutComponent } from '../../../cards/card-detail-layout/card-detail-layout.component';
import { BulkOperationsComponent } from '../../../cards/bulk-operations/bulk-operations.component';
import { SideNavCompanionBarComponent } from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoModule, TranslocoService} from "@ngneat/transloco";

@Component({
    selector: 'app-bookmarks',
    templateUrl: './bookmarks.component.html',
    styleUrls: ['./bookmarks.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, BulkOperationsComponent, CardDetailLayoutComponent, CardItemComponent, DecimalPipe, TranslocoModule]
})
export class BookmarksComponent implements OnInit {

  bookmarks: Array<PageBookmark> = [];
  series: Array<Series> = [];
  loadingBookmarks: boolean = false;
  seriesIds: {[id: number]: number} = {};
  downloadingSeries: {[id: number]: boolean} = {};
  clearingSeries: {[id: number]: boolean} = {};
  actions: ActionItem<Series>[] = [];
  jumpbarKeys: Array<JumpKey> = [];

  pagination!: Pagination;
  filter: SeriesFilter | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: SeriesFilter;

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;
  refresh: EventEmitter<void> = new EventEmitter();

  private readonly translocoService = inject(TranslocoService);

  constructor(private readerService: ReaderService, private seriesService: SeriesService,
    private downloadService: DownloadService, private toastr: ToastrService,
    private confirmService: ConfirmService, public bulkSelectionService: BulkSelectionService,
    public imageService: ImageService, private actionFactoryService: ActionFactoryService,
    private router: Router, private readonly cdRef: ChangeDetectorRef,
    private filterUtilityService: FilterUtilitiesService, private route: ActivatedRoute,
    private jumpbarService: JumpbarService) {
      this.filterSettings.ageRatingDisabled = true;
      this.filterSettings.collectionDisabled = true;
      this.filterSettings.formatDisabled = true;
      this.filterSettings.genresDisabled = true;
      this.filterSettings.languageDisabled = true;
      this.filterSettings.libraryDisabled = true;
      this.filterSettings.peopleDisabled = true;
      this.filterSettings.publicationStatusDisabled = true;
      this.filterSettings.ratingDisabled = true;
      this.filterSettings.readProgressDisabled = true;
      this.filterSettings.tagsDisabled = true;
      this.filterSettings.sortDisabled = true;
    }

  ngOnInit(): void {
    this.actions = this.actionFactoryService.getBookmarkActions(this.handleAction.bind(this));
    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
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

  async handleAction(action: ActionItem<Series>, series: Series) {
    switch (action.action) {
      case(Action.Delete):
        this.clearBookmarks(series);
        break;
      case(Action.DownloadBookmark):
        this.downloadBookmarks(series);
        break;
      case(Action.ViewSeries):
        this.router.navigate(['library', series.libraryId, 'series', series.id]);
        break;
      default:
        break;
    }
  }

  bulkActionCallback = async (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('bookmark');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));
    const seriesIds = selectedSeries.map(item => item.id);

    switch (action.action) {
      case Action.DownloadBookmark:
        this.downloadService.download('bookmark', this.bookmarks.filter(bmk => seriesIds.includes(bmk.seriesId)), (d) => {
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
          this.loadBookmarks();
        });
        break;
      default:
        break;
    }
  }

  loadBookmarks() {
    // The filter is out of sync with the presets from typeaheads on first load but syncs afterwards
    if (this.filter == undefined) {
      this.filter = this.filterUtilityService.createSeriesFilter();
      this.cdRef.markForCheck();
    }
    this.loadingBookmarks = true;
    this.cdRef.markForCheck();

    this.readerService.getAllBookmarks(this.filter).pipe(take(1)).subscribe(bookmarks => {
      this.bookmarks = bookmarks;
      this.seriesIds = {};
      this.bookmarks.forEach(bmk => {
        if (!this.seriesIds.hasOwnProperty(bmk.seriesId)) {
          this.seriesIds[bmk.seriesId] = 1;
        } else {
          this.seriesIds[bmk.seriesId] += 1;
        }
        this.downloadingSeries[bmk.seriesId] = false;
        this.clearingSeries[bmk.seriesId] = false;
      });

      const ids = Object.keys(this.seriesIds).map(k => parseInt(k, 10));
      this.seriesService.getAllSeriesByIds(ids).subscribe(series => {
        this.jumpbarKeys = this.jumpbarService.getJumpKeys(series, (t: Series) => t.name);
        this.series = series;
        this.loadingBookmarks = false;
        this.cdRef.markForCheck();
      });
      this.cdRef.markForCheck();
    });
  }

  viewBookmarks(series: Series) {
    this.router.navigate(['library', series.libraryId, 'series', series.id, 'manga', 0], {queryParams: {incognitoMode: false, bookmarkMode: true}});
  }

  async clearBookmarks(series: Series) {
    if (!await this.confirmService.confirm(this.translocoService.translate('bookmarks.confirm-single-delete', {seriesName: series.name}))) {
      return;
    }

    this.clearingSeries[series.id] = true;
    this.cdRef.markForCheck();
    this.readerService.clearBookmarks(series.id).subscribe(() => {
      const index = this.series.indexOf(series);
      if (index > -1) {
        this.series.splice(index, 1);
      }
      this.clearingSeries[series.id] = false;
      this.toastr.success(this.translocoService.translate('delete-single-success', {seriesName: series.name}));
      this.refresh.emit();
      this.cdRef.markForCheck();
    });
  }

  getBookmarkPages(seriesId: number) {
    return this.seriesIds[seriesId];
  }

  downloadBookmarks(series: Series) {
    this.downloadingSeries[series.id] = true;
    this.cdRef.markForCheck();
    this.downloadService.download('bookmark', this.bookmarks.filter(bmk => bmk.seriesId === series.id), (d) => {
      if (!d) {
        this.downloadingSeries[series.id] = false;
        this.cdRef.markForCheck();
      }
    });
  }

  updateFilter(data: FilterEvent) {
    this.filter = data.filter;

    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.pagination, this.filter);
    this.loadBookmarks();
  }

}
