import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
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


  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService) {
    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }
    this.libraryId = parseInt(routeId, 10);
    this.loadPage();
  }

  ngOnInit(): void {
  }

  loadPage() {
    this.loadingSeries = true;
    this.seriesService.getSeriesForLibrary(this.libraryId).subscribe(series => {
      this.series = series;
      this.loadingSeries = false;
    });
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

}
