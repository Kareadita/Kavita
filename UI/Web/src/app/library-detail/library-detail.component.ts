import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, HostListener, OnDestroy, OnInit } from '@angular/core';
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
import { JumpKey } from '../_models/jumpbar/jump-key';
import { SeriesRemovedEvent } from '../_models/events/series-removed-event';

@Component({
  selector: 'app-library-detail',
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
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
  refresh: EventEmitter<void> = new EventEmitter();

  jumpKeys: Array<JumpKey> = [];

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
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleSeriesAsRead(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        
        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleSeriesAsUnread(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleSeries(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
    }
  }

  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService, 
    private libraryService: LibraryService, private titleService: Title, private actionFactoryService: ActionFactoryService, 
    private actionService: ActionService, public bulkSelectionService: BulkSelectionService, private hubService: MessageHubService,
    private utilityService: UtilityService, public navService: NavService, private filterUtilityService: FilterUtilitiesService,
    private readonly cdRef: ChangeDetectorRef) {
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
      this.cdRef.markForCheck();
    });

    this.libraryService.getJumpBar(this.libraryId).subscribe(barDetails => {
      this.jumpKeys = barDetails;
      this.cdRef.markForCheck();
    });

    this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
    
    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
    [this.filterSettings.presets, this.filterSettings.openByDefault] = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
    if (this.filterSettings.presets) this.filterSettings.presets.libraries = [this.libraryId];
    // Setup filterActiveCheck to check filter against
    this.filterActiveCheck = this.filterUtilityService.createSeriesFilter();
    this.filterActiveCheck.libraries = [this.libraryId];

    this.filterSettings.libraryDisabled = true;
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(takeUntil(this.onDestroy)).subscribe((event) => {
      if (event.event === EVENTS.SeriesAdded) {
        const seriesAdded = event.payload as SeriesAddedEvent;
        if (seriesAdded.libraryId !== this.libraryId) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPage();
          return;
        }
        this.seriesService.getSeries(seriesAdded.seriesId).subscribe(s => {
          this.series = [...this.series, s].sort((s1: Series, s2: Series) => {
            if (s1.sortName < s2.sortName) return -1;
            if (s1.sortName > s2.sortName) return 1;
            return 0;
          });
          this.pagination.totalItems++;
          this.cdRef.markForCheck();
          this.refresh.emit();
        });
        
        
      } else if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemoved = event.payload as SeriesRemovedEvent;
        if (seriesRemoved.libraryId !== this.libraryId) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPage();
          return;
        }

        this.series = this.series.filter(s => s.id != seriesRemoved.seriesId);
        this.pagination.totalItems--;
        this.cdRef.markForCheck();
        this.refresh.emit();
      }
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
      case(Action.Scan):
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
      this.filter = this.filterUtilityService.createSeriesFilter();
      this.filter.libraries.push(this.libraryId);
      this.cdRef.markForCheck();
    }

    this.loadingSeries = true;
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.cdRef.markForCheck();
    
    this.seriesService.getSeriesForLibrary(0, undefined, undefined, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result; 
      this.pagination = series.pagination;
      this.loadingSeries = false;
      this.cdRef.markForCheck();
      window.scrollTo(0, 0);
    });
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  trackByIdentity = (index: number, item: Series) => `${item.id}_${item.name}_${item.localizedName}_${item.pagesRead}`;
}
