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

  constructor(private route: ActivatedRoute, private seriesService: SeriesService, private ratingConfig: NgbRatingConfig) {
    ratingConfig.max = 5;
  }

  ngOnInit(): void {

    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId === null) {
      console.error('No library id was passed. Redirecting to home');
      //this.router.navigateByUrl('/home');
      return;
    }
    const seriesId = parseInt(routeId, 10);
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.series = series;
      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        this.volumes = volumes;
      });
    });
  }

  openVolume(volume: Volume) {
    alert('TODO: Let user read Manga');
  }

}
