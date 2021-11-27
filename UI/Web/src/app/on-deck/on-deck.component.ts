import { Component, HostListener, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, ActivatedRoute } from '@angular/router';
import { take } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { UpdateFilterEvent } from '../cards/card-detail-layout/card-detail-layout.component';
import { KEY_CODES } from '../shared/_services/utility.service';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { FilterItem, SeriesFilter, mangaFormatFilters } from '../_models/series-filter';
import { Action } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-on-deck',
  templateUrl: './on-deck.component.html',
  styleUrls: ['./on-deck.component.scss']
})
export class OnDeckComponent implements OnInit {

  isLoading: boolean = true;
  series: Series[] = [];
  pagination!: Pagination;
  libraryId!: number;
  filters: Array<FilterItem> = mangaFormatFilters;
  filter: SeriesFilter = {
    mangaFormat: null
  };

  constructor(private router: Router, private route: ActivatedRoute, private seriesService: SeriesService, private titleService: Title,
    private actionService: ActionService, public bulkSelectionService: BulkSelectionService) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.titleService.setTitle('Kavita - On Deck');
    if (this.pagination === undefined || this.pagination === null) {
      this.pagination = {currentPage: 0, itemsPerPage: 30, totalItems: 0, totalPages: 1};
    }
    this.loadPage();
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

  ngOnInit() {}

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  onPageChange(pagination: Pagination) {
    window.history.replaceState(window.location.href, '', window.location.href.split('?')[0] + '?page=' + this.pagination.currentPage);
    this.loadPage();
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
    this.isLoading = true;
    this.seriesService.getOnDeck(this.libraryId, this.pagination?.currentPage, this.pagination?.itemsPerPage, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.isLoading = false;
      window.scrollTo(0, 0);
    });
  }

  getPage() {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get('page');
  }

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

}
