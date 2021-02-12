import { Component, OnInit } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbModal, NgbRatingConfig } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin } from 'rxjs';
import { CardItemAction } from '../shared/card-item/card-item.component';
import { CardDetailsModalComponent } from '../shared/_modals/card-details-modal/card-details-modal.component';
import { UtilityService } from '../shared/_services/utility.service';
import { Chapter } from '../_models/chapter';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { ReaderService } from '../_services/reader.service';
import { SeriesService } from '../_services/series.service';


@Component({
  selector: 'app-series-detail',
  templateUrl: './series-detail.component.html',
  styleUrls: ['./series-detail.component.scss']
})
export class SeriesDetailComponent implements OnInit {

  series: Series | undefined;
  volumes: Volume[] = [];
  chapters: Chapter[] = [];
  libraryId = 0;

  currentlyReadingVolume: Volume | undefined = undefined;
  currentlyReadingChapter: Chapter | undefined = undefined;
  safeImage!: SafeUrl;
  placeholderImage = 'assets/images/image-placeholder.jpg';

  testMap: any;
  showBook = false;
  isLoading = true;

  volumeActions: CardItemAction[] = [];


  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              ratingConfig: NgbRatingConfig, private router: Router,
              private sanitizer: DomSanitizer, private modalService: NgbModal,
              private readerService: ReaderService, private utilityService: UtilityService) {
    ratingConfig.max = 5;
  }

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('seriesId');
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null || libraryId == null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.volumeActions = [
      {title: 'Mark Read', callback: (data: Volume) => this.markAsRead(data)},
      {title: 'Mark Unread', callback: (data: Volume) => this.markAsUnread(data)},
      {
      title: 'Info',
      callback: (data: Volume) => {
        this.openViewInfo(data);
      }
    }];


    const seriesId = parseInt(routeId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.series = series;
      this.safeImage = this.sanitizer.bypassSecurityTrustUrl('data:image/jpeg;base64,' + series.coverImage);

      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        this.chapters = volumes.filter(v => !v.isSpecial && v.number === 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters);
        this.volumes = volumes.sort(this.utilityService.sortVolumes);

        this.setContinuePoint();
        this.isLoading = false;
      });
    });
  }

  setContinuePoint() {
    this.volumes.forEach(v => {
      if (v.pagesRead >= v.pages - 1) {
        return;
      } else if (v.pagesRead === 0) {
        return;
      } else {
        this.currentlyReadingVolume = v;
      }
    });

    if (this.currentlyReadingVolume === undefined) {
      // We need to check against chapters
      this.chapters.forEach(c => {
        if (c.pagesRead >= c.pages - 1) {
          return;
        } else if (c.pagesRead === 0) {
          return;
        } else {
          this.currentlyReadingChapter = c;
        }
      });
      if (this.currentlyReadingChapter === undefined) {
        // Default to first chapter
        this.currentlyReadingChapter = this.chapters[0];
      }
    }
  }

  markAsRead(vol: Volume) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    forkJoin(vol.chapters?.map(chapter => this.readerService.bookmark(seriesId, vol.id, chapter.id, chapter.pages))).subscribe(results => {
      vol.pagesRead = vol.pages;
    });
  }

  markAsUnread(vol: Volume) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    forkJoin(vol.chapters?.map(chapter => this.readerService.bookmark(seriesId, vol.id, chapter.id, 0))).subscribe(results => {
      vol.pagesRead = 0;
    });
  }

  read() {
    if (this.currentlyReadingVolume !== undefined) { this.openVolume(this.currentlyReadingVolume); }
    if (this.currentlyReadingChapter !== undefined) { this.openChapter(this.currentlyReadingChapter); }
    else { this.openVolume(this.volumes[0]); }
  }

  updateRating(rating: any) {
    if (this.series === undefined) {
      return;
    }

    console.log('Rating is: ', this.series?.userRating);
    this.seriesService.updateRating(this.series?.id, this.series?.userRating, this.series?.userReview).subscribe(() => {});
  }

  openChapter(chapter: Chapter) {
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

  openViewInfo(data: Volume | Chapter) {
    const modalRef = this.modalService.open(CardDetailsModalComponent, { size: 'lg' });
    modalRef.componentInstance.data = data;
    modalRef.componentInstance.parentName = this.series?.name;
  }

}
