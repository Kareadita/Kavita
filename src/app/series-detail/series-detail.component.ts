import { Component, OnInit } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbRatingConfig } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin, Observable } from 'rxjs';
import { Chatper } from '../_models/chapter';
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
  chapters: Chatper[] = [];
  libraryId = 0;

  currentlyReadingVolume!: Volume;
  safeImage!: SafeUrl;
  placeholderImage = 'assets/images/image-placeholder.jpg';

  testMap: any;
  showBook = false;


  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              private ratingConfig: NgbRatingConfig, private router: Router, private sanitizer: DomSanitizer) {
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
      this.safeImage = this.sanitizer.bypassSecurityTrustUrl('data:image/jpeg;base64,' + series.coverImage);

      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        // TODO: Extract sorter to separate file.

        this.chapters = volumes.filter(v => !v.isSpecial && v.number === 0).map(v => v.chapters || []).flat();
        this.volumes = volumes.sort((a, b) => {
          if (a === b) { return 0; }
          else if (a.number === 0) { return 1; }
          else if (b.number === 0) { return -1; }
          else {
            return a.number < b.number ? -1 : 1;
          }
        });

        this.volumes.forEach(v => {
          v.name = v.number === 0 ? 'Latest Chapters' : 'Volume ' + v.number;
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

  updateRating(rating: any) {
    if (this.series === undefined) {
      return;
    }

    console.log('Rating is: ', this.series?.userRating);
    this.seriesService.updateRating(this.series?.id, this.series?.userRating, this.series?.userReview).subscribe(() => {});
  }

  openChapter(chapter: Chatper) {
    this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'manga', chapter.id]);
  }

  openVolume(volume: Volume) {
    if (volume.chapters === undefined) {
      console.error('openVolume not implemented. Need to fetch chapter information.');
      return;
    }
    this.openChapter(volume.chapters[0]);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

}
