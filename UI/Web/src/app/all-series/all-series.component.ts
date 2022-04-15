import { Component, EventEmitter, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { take, debounceTime, takeUntil } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { FilterSettings } from '../metadata-filter/filter-settings';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { KEY_CODES, UtilityService } from '../shared/_services/utility.service';
import { Library } from '../_models/library';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { FilterEvent, SeriesFilter } from '../_models/series-filter';
import { ActionItem, Action } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { EVENTS, Message, MessageHubService } from '../_services/message-hub.service';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-all-series',
  templateUrl: './all-series.component.html',
  styleUrls: ['./all-series.component.scss']
})
export class AllSeriesComponent implements OnInit, OnDestroy {

  series: Series[] = [];
  loadingSeries = false;
  pagination!: Pagination;
  actions: ActionItem<Library>[] = [];
  filter: SeriesFilter | undefined = undefined;
  onDestroy: Subject<void> = new Subject<void>();
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;

  bulkActionCallback = (action: Action, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));

    switch (action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleSeriesAsRead(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        
        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleSeriesAsUnread(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleSeries(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
    }
  }

  constructor(private router: Router, private seriesService: SeriesService, 
    private titleService: Title, private actionService: ActionService, 
    public bulkSelectionService: BulkSelectionService, private hubService: MessageHubService,
    private utilityService: UtilityService, private route: ActivatedRoute, 
    private filterUtilityService: FilterUtilitiesService) {
    
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.titleService.setTitle('Kavita - All Series');

    this.pagination = this.filterUtilityService.pagination();
    [this.filterSettings.presets, this.filterSettings.openByDefault]  = this.filterUtilityService.filterPresetsFromUrl();
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(debounceTime(6000), takeUntil(this.onDestroy)).subscribe((event: Message<any>) => {
      if (event.event !== EVENTS.SeriesAdded) return;
      this.loadPage();
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
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

  updateFilter(data: FilterEvent) {
    this.filter = data.filter;
    console.log('updateFilter: ', data.filter);
    
    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.pagination, this.filter);
    this.loadPage();
  }

  loadPage() {
    // The filter is out of sync with the presets from typeaheads on first load but syncs afterwards
    if (this.filter == undefined) {
      console.log('setting new filter as filter was undefined');
      this.filter = this.seriesService.createSeriesFilter();
    }

    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterSettings.presets);
    this.seriesService.getAllSeries(this.pagination?.currentPage, this.pagination?.itemsPerPage, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.loadingSeries = false;
      window.scrollTo(0, 0);
    });
  }

  onPageChange(pagination: Pagination) {
    this.filterUtilityService.updateUrlFromPagination(this.pagination);
    this.loadPage();
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.originalName}_${item.localizedName}_${item.pagesRead}`;
}
