import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Library } from '../_models/library';
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
  title = '';
  series: Series[] = [];


  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService) {
    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId === null) {
      console.error('No library id was passed. Redirecting to home');
      this.router.navigateByUrl('/home');
      return;
    }
    this.libraryId = parseInt(routeId, 10);
    this.loadPage();
  }

  ngOnInit(): void {
  }

  loadPage() {
    this.seriesService.getSeriesForLibrary(this.libraryId, true).subscribe(series => {
      this.series = series;
    });
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

}
