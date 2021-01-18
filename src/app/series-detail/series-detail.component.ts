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

  currentlyReadingVolume!: Volume;

  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              private ratingConfig: NgbRatingConfig, private router: Router) {
    ratingConfig.max = 5;
  }

  ngOnInit(): void {
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
      // TODO: Remove debug code
      //console.error('Debug code present, overriding summary');
      //this.series.summary = `Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.`;
      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        this.volumes = volumes;
        volumes.forEach(v => {
          if (v.pagesRead >= v.pages) {
            return;
          } else if (v.pagesRead === 0) {
            return;
          } else {
            this.currentlyReadingVolume = v;
          }
        });
      });
    });
  }

  read() {
    if (this.currentlyReadingVolume !== undefined) {
      this.openVolume(this.currentlyReadingVolume);
    } else {
      this.openVolume(this.volumes[0]);
    }
  }

  openVolume(volume: Volume) {
    this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'manga', volume.id]);
  }

}
