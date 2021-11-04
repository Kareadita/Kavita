import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, take, takeUntil, takeWhile } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { UpdateFilterEvent } from '../cards/card-detail-layout/card-detail-layout.component';
import { KEY_CODES } from '../shared/_services/utility.service';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { Library } from '../_models/library';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { FilterItem, mangaFormatFilters, SeriesFilter } from '../_models/series-filter';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { LibraryService } from '../_services/library.service';
import { MessageHubService } from '../_services/message-hub.service';
import { SeriesService } from '../_services/series.service';

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
  filters: Array<FilterItem> = mangaFormatFilters;
  filter: SeriesFilter = {
    mangaFormat: null
  };
  onDestroy: Subject<void> = new Subject<void>();

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
    private actionService: ActionService, public bulkSelectionService: BulkSelectionService, private hubService: MessageHubService) {
    const routeId = this.route.snapshot.paramMap.get('id');
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
    this.pagination = {currentPage: 0, itemsPerPage: 30, totalItems: 0, totalPages: 1};
    this.loadPage();
  }

  ngOnInit(): void {
    this.hubService.seriesAdded.pipe(takeWhile(event => event.libraryId === this.libraryId), debounceTime(6000), takeUntil(this.onDestroy)).subscribe((event: SeriesAddedEvent) => {
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

  updateFilter(data: UpdateFilterEvent) {
    this.filter.mangaFormat = data.filterItem.value;
    if (this.pagination !== undefined && this.pagination !== null) {
      this.pagination.currentPage = 1;
      this.onPageChange(this.pagination);
    } else {
      this.loadPage();
    }
  }

  loadPage() {
    const page = this.getPage();
    if (page != null) {
      this.pagination.currentPage = parseInt(page, 10);
    }
    this.loadingSeries = true;

    this.seriesService.getSeriesForLibrary(this.libraryId, this.pagination?.currentPage, this.pagination?.itemsPerPage, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.loadingSeries = false;
      window.scrollTo(0, 0);
    });
  }

  onPageChange(pagination: Pagination) {
    window.history.replaceState(window.location.href, '', window.location.href.split('?')[0] + '?page=' + this.pagination.currentPage);
    this.loadPage();
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.originalName}_${item.localizedName}_${item.pagesRead}`;

  getPage() {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get('page');
  }

}
