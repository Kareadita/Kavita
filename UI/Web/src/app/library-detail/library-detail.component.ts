import { Component, EventEmitter, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { KEY_CODES, UtilityService } from '../shared/_services/utility.service';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { Library } from '../_models/library';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { FilterEvent, SeriesFilter } from '../_models/series-filter';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { LibraryService } from '../_services/library.service';
import { EVENTS, MessageHubService } from '../_services/message-hub.service';
import { SeriesService } from '../_services/series.service';
import { NavService } from '../_services/nav.service';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { FilterSettings } from '../metadata-filter/filter-settings';

@Component({
  selector: 'app-library-detail',
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss']
})
export class LibraryDetailComponent implements OnInit, OnDestroy {

  libraryId!: number;
  libraryName = '';
  series: Series[] = [];
  loadingSeries = false;
  pagination!: Pagination;
  actions: ActionItem<Library>[] = [];
  filter: SeriesFilter | undefined = undefined;
  onDestroy: Subject<void> = new Subject<void>();
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: SeriesFilter;

  tabs: Array<{title: string, fragment: string, icon: string}> = [
    {title: 'Library', fragment: '', icon: 'fa-landmark'},
    {title: 'Recommended', fragment: 'recomended', icon: 'fa-award'},
  ];
  active = this.tabs[0];


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

  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService, 
    private libraryService: LibraryService, private titleService: Title, private actionFactoryService: ActionFactoryService, 
    private actionService: ActionService, public bulkSelectionService: BulkSelectionService, private hubService: MessageHubService,
    private utilityService: UtilityService, public navService: NavService, private filterUtilityService: FilterUtilitiesService) {
    const routeId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null) {
      this.router.navigateByUrl('/libraries');
      return;
    }


    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.libraryId = parseInt(routeId, 10);
    this.libraryService.getLibraryNames().pipe(take(1)).subscribe(names => {
      this.libraryName = names[this.libraryId];
      this.titleService.setTitle('Kavita - ' + this.libraryName);
    });
    this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
    
    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
    [this.filterSettings.presets, this.filterSettings.openByDefault] = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
    if (this.filterSettings.presets) this.filterSettings.presets.libraries = [this.libraryId];
    // Setup filterActiveCheck to check filter against
    this.filterActiveCheck = this.seriesService.createSeriesFilter();
    this.filterActiveCheck.libraries = [this.libraryId];

    this.filterSettings.libraryDisabled = true;
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(debounceTime(6000), takeUntil(this.onDestroy)).subscribe((event) => {
      if (event.event !== EVENTS.SeriesAdded) return;
      const seriesAdded = event.payload as SeriesAddedEvent;
      if (seriesAdded.libraryId !== this.libraryId) return;
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

  handleAction(action: Action, library: Library) {
    let lib: Partial<Library> = library;
    if (library === undefined) {
      lib = {id: this.libraryId, name: this.libraryName};
    }
    switch (action) {
      case(Action.ScanLibrary):
        this.actionService.scanLibrary(lib);
        break;
      case(Action.RefreshMetadata):
      this.actionService.refreshMetadata(lib);
        break;
      default:
        break;
    }
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, undefined);
    }
  }

  updateFilter(data: FilterEvent) {
    this.filter = data.filter;

    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.pagination, this.filter);
    this.loadPage();
  }

  loadPage() {
    // The filter is out of sync with the presets from typeaheads on first load but syncs afterwards
    if (this.filter == undefined) {
      this.filter = this.seriesService.createSeriesFilter();
      this.filter.libraries.push(this.libraryId);
    }

    this.loadingSeries = true;
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.seriesService.getSeriesForLibrary(0, this.pagination?.currentPage, this.pagination?.itemsPerPage, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.loadingSeries = false;
      window.scrollTo(0, 0);
    });
  }

  onPageChange(pagination: Pagination) {
    this.filterUtilityService.updateUrlFromFilter(this.pagination, undefined);
    this.loadPage();
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.originalName}_${item.localizedName}_${item.pagesRead}`;
}
