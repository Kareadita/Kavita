import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { UpdateFilterEvent } from '../shared/card-detail-layout/card-detail-layout.component';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { FilterItem, SeriesFilter, mangaFormatFilters } from '../_models/series-filter';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-in-progress',
  templateUrl: './in-progress.component.html',
  styleUrls: ['./in-progress.component.scss']
})
export class InProgressComponent implements OnInit {

  isLoading: boolean = true;
  recentlyAdded: Series[] = [];
  pagination!: Pagination;
  libraryId!: number;
  filters: Array<FilterItem> = mangaFormatFilters;
  filter: SeriesFilter = {
    mangaFormat: null
  };

  constructor(private router: Router, private route: ActivatedRoute, private seriesService: SeriesService) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => true;
  }

  ngOnInit() {
    this.loadPage();
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  onPageChange(pagination: Pagination) {
    this.router.navigate(['recently-added'], {replaceUrl: true, queryParamsHandling: 'merge', queryParams: {page: this.pagination.currentPage} });
  }

  updateFilter(data: UpdateFilterEvent) {
    this.filter.mangaFormat = data.filterItem.value;
    if (this.pagination !== undefined && this.pagination !== null) {
      this.pagination.currentPage = 1;
      this.onPageChange(this.pagination);
    }
    
    this.loadPage();
  }

  loadPage() {
    if (this.pagination === undefined || this.pagination === null) {
      this.pagination = {currentPage: 0, itemsPerPage: 30, totalItems: 0, totalPages: 1};
    }
    const page = this.route.snapshot.queryParamMap.get('page');
    if (page != null) {
      this.pagination.currentPage = parseInt(page, 10);
    }
    this.isLoading = true;
    this.seriesService.getInProgress(this.libraryId, this.pagination?.currentPage, this.pagination?.itemsPerPage, this.filter).subscribe(series => {
      this.recentlyAdded = series.result;
      this.pagination = series.pagination;
      this.isLoading = false;
      window.scrollTo(0, 0);
    });
  }

}
