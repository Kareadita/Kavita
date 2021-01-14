import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbRatingConfig } from '@ng-bootstrap/ng-bootstrap';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-series-detail',
  templateUrl: './series-detail.component.html',
  styleUrls: ['./series-detail.component.scss']
})
export class SeriesDetailComponent implements OnInit {

  series: Series | undefined;
  volumes: Volume[] = [];
  libraryId = 0;

  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              private ratingConfig: NgbRatingConfig, private router: Router) {
    ratingConfig.max = 5;
  }

  ngOnInit(): void {

    console.log('Params: ', this.route.snapshot.paramMap);
    const routeId = this.route.snapshot.paramMap.get('seriesId');
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null || libraryId == null) {
      this.router.navigateByUrl('/home');
      return;
    }
    const seriesId = parseInt(routeId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.series = series;
      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        this.volumes = volumes;
      });
    });
  }

  openVolume(volume: Volume) {
    this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'manga', volume.id]);
  }

}
