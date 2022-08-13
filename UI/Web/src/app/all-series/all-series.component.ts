import { Component, EventEmitter, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { take, debounceTime, takeUntil } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { FilterSettings } from '../metadata-filter/filter-settings';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { KEY_CODES, UtilityService } from '../shared/_services/utility.service';
import { JumpKey } from '../_models/jumpbar/jump-key';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { FilterEvent, SeriesFilter } from '../_models/series-filter';
import { Action } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { LibraryService } from '../_services/library.service';
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
  filter: SeriesFilter | undefined = undefined;
  onDestroy: Subject<void> = new Subject<void>();
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActiveCheck!: SeriesFilter;
  filterActive: boolean = false;
  jumpbarKeys: Array<JumpKey> = [];

  bulkActionCallback = (action: Action, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));

    switch (action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
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
    private filterUtilityService: FilterUtilitiesService, private libraryService: LibraryService) {
    
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.titleService.setTitle('Kavita - All Series');

    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
    [this.filterSettings.presets, this.filterSettings.openByDefault]  = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
    this.filterActiveCheck = this.seriesService.createSeriesFilter();
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
    
    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.pagination, this.filter);
    this.loadPage();
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.seriesService.getAllSeries(undefined, undefined, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      const keys: {[key: string]: number} = {};
      const collator = new Intl.Collator();
      series.result.forEach(s => {
        let ch = s.name.toUpperCase().normalize('NFKD').charAt(0);
        if (!/\p{Alpha}/u.test(ch)) {
          ch = '#';
        }
        if (!keys.hasOwnProperty(ch)) {
          keys[ch] = 0;
        }
        keys[ch] += 1;
      });
      this.jumpbarKeys = Object.keys(keys).map(k => {
        return {
          key: k,
          size: keys[k],
          title: k
        };
      }).sort((a, b) => collator.compare(a.key, b.key));
      this.pagination = series.pagination;
      this.loadingSeries = false;
      window.scrollTo(0, 0);
    });
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;
}
