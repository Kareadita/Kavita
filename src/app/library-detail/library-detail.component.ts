import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-library-detail',
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss']
})
export class LibraryDetailComponent implements OnInit {

  libraryId!: number;
  title = '';
  series: Series[] = [];
  loadingSeries = false;

  pagination!: Pagination;
  pageNumber = 1;
  pageSize = 30; // TODO: Refactor this into UserPreference or ServerSetting

  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService) {
    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.libraryId = parseInt(routeId, 10);
    this.loadPage();
  }

  ngOnInit(): void {
  }

  loadPage() {
    const page = this.route.snapshot.queryParamMap.get('page');
    if (page != null) {
      this.pageNumber = parseInt(page, 10);
    }
    this.loadingSeries = true;
    this.seriesService.getSeriesForLibrary(this.libraryId, this.pageNumber, this.pageSize).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.loadingSeries = false;
      window.scrollTo(0, 0);
    });
  }

  onPageChange(page: number) {
    this.router.navigate(['library', this.libraryId], {replaceUrl: true, queryParamsHandling: 'merge', queryParams: {page} });
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  mangaTrackBy(index: number, manga: Series) {
    return manga.name;
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.originalName}_${item.localizedName}`;

}
