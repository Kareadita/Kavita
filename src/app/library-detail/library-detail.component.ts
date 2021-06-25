import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { LibraryService } from '../_services/library.service';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-library-detail',
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss']
})
export class LibraryDetailComponent implements OnInit {

  libraryId!: number;
  libraryName = '';
  series: Series[] = [];
  loadingSeries = false;
  pagination!: Pagination;

  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService, private libraryService: LibraryService, private titleService: Title) {
    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.libraryId = parseInt(routeId, 10);
    this.libraryService.getLibraryNames().pipe(take(1)).subscribe(names => {
      this.libraryName = names[this.libraryId];
      this.titleService.setTitle('Kavita - ' + this.libraryName);
    })
    this.loadPage();
  }

  ngOnInit(): void {
  }

  loadPage() {
    const page = this.route.snapshot.queryParamMap.get('page');
    if (page != null) {
      if (this.pagination == undefined || this.pagination == null) {
        this.pagination = {currentPage: 0, itemsPerPage: 30, totalItems: 0, totalPages: 1};
      }
      this.pagination.currentPage = parseInt(page, 10);
    }
    this.loadingSeries = true;
    this.seriesService.getSeriesForLibrary(this.libraryId, this.pagination?.currentPage, this.pagination?.itemsPerPage).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.loadingSeries = false;
      window.scrollTo(0, 0);
    });
  }

  onPageChange(pagination: Pagination) {
    this.router.navigate(['library', this.libraryId], {replaceUrl: true, queryParamsHandling: 'merge', queryParams: {page: this.pagination.currentPage} });
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.originalName}_${item.localizedName}`;

}
