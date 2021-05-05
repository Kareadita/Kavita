import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbModal, NgbRatingConfig } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { forkJoin } from 'rxjs';
import { take } from 'rxjs/operators';
import { ConfirmConfig } from '../shared/confirm-dialog/_models/confirm-config';
import { ConfirmService } from '../shared/confirm.service';
import { CardDetailsModalComponent } from '../shared/_modals/card-details-modal/card-details-modal.component';
import { UtilityService } from '../shared/_services/utility.service';
import { EditSeriesModalComponent } from '../_modals/edit-series-modal/edit-series-modal.component';
import { ReviewSeriesModalComponent } from '../_modals/review-series-modal/review-series-modal.component';
import { Chapter } from '../_models/chapter';
import { LibraryType } from '../_models/library';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from '../_services/account.service';
import { ActionItem, ActionFactoryService, Action } from '../_services/action-factory.service';
import { ImageService } from '../_services/image.service';
import { LibraryService } from '../_services/library.service';
import { ReaderService } from '../_services/reader.service';
import { SeriesService } from '../_services/series.service';


@Component({
  selector: 'app-series-detail',
  templateUrl: './series-detail.component.html',
  styleUrls: ['./series-detail.component.scss']
})
export class SeriesDetailComponent implements OnInit {

  series!: Series;
  volumes: Volume[] = [];
  chapters: Chapter[] = [];
  libraryId = 0;
  isAdmin = false;
  isLoading = true;
  showBook = true;

  currentlyReadingVolume: Volume | undefined = undefined;
  currentlyReadingChapter: Chapter | undefined = undefined;
  hasReadingProgress = false;


  seriesActions: ActionItem<Series>[] = [];
  volumeActions: ActionItem<Volume>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];

  hasSpecials = false;
  specials: Array<Chapter> = [];
  activeTabId = 2;
  hasNonSpecialVolumeChapters = true;

  seriesSummary: string = '';
  userReview: string = '';
  libraryType: LibraryType = LibraryType.Manga;


  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              ratingConfig: NgbRatingConfig, private router: Router,
              private modalService: NgbModal, public readerService: ReaderService,
              private utilityService: UtilityService, private toastr: ToastrService,
              private accountService: AccountService, public imageService: ImageService,
              private actionFactoryService: ActionFactoryService, private libraryService: LibraryService,
              private confirmService: ConfirmService) {
    ratingConfig.max = 5;
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      }
    })
  }

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('seriesId');
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null || libraryId == null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.seriesActions = this.actionFactoryService.getSeriesActions(this.handleSeriesActionCallback.bind(this)).filter(action => action.action !== Action.Edit);
    this.volumeActions = this.actionFactoryService.getVolumeActions(this.handleVolumeActionCallback.bind(this));
    this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this));


    const seriesId = parseInt(routeId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
      this.loadSeries(seriesId);
    });
  }

  handleSeriesActionCallback(action: Action, series: Series) {
    switch(action) {
      case(Action.MarkAsRead):
        this.markSeriesAsRead(series);
        break;
      case(Action.MarkAsUnread):
        this.markSeriesAsUnread(series);
        break;
      case(Action.ScanLibrary):
        this.scanLibrary(series);
        break;
      case(Action.RefreshMetadata):
        this.refreshMetdata(series);
        break;
      case(Action.Delete):
        this.deleteSeries(series);
        break;
      default:
        break;
    }
  }

  handleVolumeActionCallback(action: Action, volume: Volume) {
    switch(action) {
      case(Action.MarkAsRead):
        this.markAsRead(volume);
        break;
      case(Action.MarkAsUnread):
        this.markAsUnread(volume);
        break;
      case(Action.Info):
        this.openViewInfo(volume);
        break;
      default:
        break;
    }
  }

  handleChapterActionCallback(action: Action, chapter: Chapter) {
    switch (action) {
      case(Action.MarkAsRead):
        this.markChapterAsRead(chapter);
        break;
      case(Action.MarkAsUnread):
        this.markChapterAsUnread(chapter);
        break;
      case(Action.Info):
        this.openViewInfo(chapter);
        break;
      default:
        break;
    }
  }

  refreshMetdata(series: Series) {
    this.seriesService.refreshMetadata(series).subscribe((res: any) => {
      this.toastr.success('Refresh started for ' + series.name);
    });
  }

  scanLibrary(series: Series) {
    this.libraryService.scan(series.libraryId).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + series.name);
    });
  }

  async deleteSeries(series: Series) {
    if (!await this.confirmService.confirm('Are you sure you want to delete this series? It will not modify files on disk.')) {
      return;
    }

    this.seriesService.delete(series.id).subscribe((res: boolean) => {
      if (res) {
        this.toastr.success('Series deleted');
        this.router.navigate(['library', this.libraryId]);
      }
    });
  }

  markSeriesAsUnread(series: Series) {
    this.seriesService.markUnread(series.id).subscribe(res => {
      this.toastr.success(series.name + ' is now unread');
      series.pagesRead = 0;
      this.loadSeries(series.id);
    });
  }

  markSeriesAsRead(series: Series) {
    this.seriesService.markRead(series.id).subscribe(res => {
      this.toastr.success(series.name + ' is now read');
      series.pagesRead = series.pages;
      this.loadSeries(series.id);
    });
  }

  loadSeries(seriesId: number) {
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.series = series;
      this.createHTML();

      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        this.chapters = volumes.filter(v => v.number === 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters); 
        this.volumes = volumes.sort(this.utilityService.sortVolumes);

        this.setContinuePoint();
        const vol0 = this.volumes.filter(v => v.number === 0);
        this.hasSpecials = vol0.map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters).filter(c => c.isSpecial || isNaN(parseInt(c.range, 10))).length > 0 ;
        if (this.hasSpecials) {
          this.specials = vol0.map(v => v.chapters || []).flat().filter(c => c.isSpecial || isNaN(parseInt(c.range, 10))).map(c => {
            c.range = c.range.replace(/_/g, ' ');
            return c;
          });
        }

        if (this.volumes.filter(v => v.number !== 0).length === 0 && this.chapters.filter(c => !c.isSpecial).length === 0 && this.specials.length > 0) {
          this.activeTabId = 1;
          this.hasNonSpecialVolumeChapters = false;
        }

        this.isLoading = false;
      });
    });
  }

  createHTML() {
    this.seriesSummary = (this.series.summary === null ? '' : this.series.summary).replace(/\n/g, '<br>');
    this.userReview = (this.series.userReview === null ? '' : this.series.userReview).replace(/\n/g, '<br>');
  }

  setContinuePoint() {
    this.currentlyReadingVolume = undefined;
    this.currentlyReadingChapter = undefined;
    this.hasReadingProgress = this.volumes.filter(v => v.pagesRead > 0).length > 0 || this.chapters.filter(c => c.pagesRead > 0).length > 0;

    const [currentVolume, currentChapter] = this.readerService.getCurrentVolumeAndChapter(this.volumes);
    this.currentlyReadingVolume = currentVolume;
    this.currentlyReadingChapter = currentChapter;
  }

  markAsRead(vol: Volume) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    this.readerService.markVolumeRead(seriesId, vol.id).subscribe(() => {
      vol.pagesRead = vol.pages;
      this.setContinuePoint();
      this.toastr.success('Marked as Read');
    });
  }

  markAsUnread(vol: Volume) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    forkJoin(vol.chapters?.map(chapter => this.readerService.bookmark(seriesId, vol.id, chapter.id, 0))).subscribe(results => {
      vol.pagesRead = 0;
      this.setContinuePoint();
      this.toastr.success('Marked as Unread');
    });
  }

  markChapterAsRead(chapter: Chapter) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    this.readerService.bookmark(seriesId, chapter.volumeId, chapter.id, chapter.pages).subscribe(results => {
      this.toastr.success('Marked as Read');
      chapter.pagesRead = chapter.pages;
      this.setContinuePoint();
    });
  }

  markChapterAsUnread(chapter: Chapter) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    this.readerService.bookmark(seriesId, chapter.volumeId, chapter.id, 0).subscribe(results => {
      chapter.pagesRead = 0;
      this.setContinuePoint();
      this.toastr.success('Marked as Unread');
    });
  }

  read() {
    if (this.currentlyReadingVolume !== undefined) { this.openVolume(this.currentlyReadingVolume); }
    else if (this.currentlyReadingChapter !== undefined) { this.openChapter(this.currentlyReadingChapter); }
    else { this.openVolume(this.volumes[0]); }
  }

  updateRating(rating: any) {
    if (this.series === undefined) {
      return;
    }

    this.seriesService.updateRating(this.series?.id, this.series?.userRating, this.series?.userReview).subscribe(() => {
      this.createHTML();
    });
  }

  openChapter(chapter: Chapter) {
    if (chapter.pages === 0) {
      this.toastr.error('There are no pages. Kavita was not able to read this archive.');
      return;
    }
    if (this.libraryType === LibraryType.Book) {
      this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'book', chapter.id]);
    } else {
      this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'manga', chapter.id]);
    }
  }

  openVolume(volume: Volume) {
    if (volume.chapters === undefined || volume.chapters?.length === 0) {
      this.toastr.error('There are no chapters to this volume. Cannot read.');
      return;
    }
    this.openChapter(volume.chapters[0]);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  openViewInfo(data: Volume | Chapter) {
    const modalRef = this.modalService.open(CardDetailsModalComponent, { size: 'lg', scrollable: true });
    modalRef.componentInstance.data = data;
    modalRef.componentInstance.parentName = this.series?.name;
  }

  openEditSeriesModal() {
    const modalRef = this.modalService.open(EditSeriesModalComponent, {  scrollable: true, size: 'lg', windowClass: 'scrollable-modal' });
    modalRef.componentInstance.series = this.series;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series}) => {
      window.scrollTo(0, 0);
      if (closeResult.success) {
        this.loadSeries(this.series.id);
      }
    });
  }

  async promptToReview() {
    const shouldPrompt = this.isNullOrEmpty(this.series.userReview);
    const config = new ConfirmConfig();
    config.header = 'Confirm';
    config.content = 'Do you want to write a review?';
    config.buttons.push({text: 'No', type: 'secondary'});
    config.buttons.push({text: 'Yes', type: 'primary'});
    if (shouldPrompt && await this.confirmService.confirm('Do you want to write a review?', config)) {
      this.openReviewModal();
    }
  }

  openReviewModal(force = false) {
    const modalRef = this.modalService.open(ReviewSeriesModalComponent, { scrollable: true, size: 'lg' });
    modalRef.componentInstance.series = this.series;
    modalRef.closed.subscribe((closeResult: {success: boolean, review: string, rating: number}) => {
      if (closeResult.success && this.series !== undefined) {
        this.series.userReview = closeResult.review;
        this.series.userRating = closeResult.rating;
        this.createHTML();
      }
    });
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.series);
    }
  }
}
