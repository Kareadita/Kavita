import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbModal, NgbRatingConfig } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { forkJoin, Subject } from 'rxjs';
import { finalize, take, takeUntil, takeWhile } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { CardDetailsModalComponent } from '../cards/_modals/card-details-modal/card-details-modal.component';
import { EditSeriesModalComponent } from '../cards/_modals/edit-series-modal/edit-series-modal.component';
import { ConfirmConfig } from '../shared/confirm-dialog/_models/confirm-config';
import { ConfirmService } from '../shared/confirm.service';
import { TagBadgeCursor } from '../shared/tag-badge/tag-badge.component';
import { DownloadService } from '../shared/_services/download.service';
import { KEY_CODES, UtilityService } from '../shared/_services/utility.service';
import { ReviewSeriesModalComponent } from '../_modals/review-series-modal/review-series-modal.component';
import { Chapter } from '../_models/chapter';
import { ScanSeriesEvent } from '../_models/events/scan-series-event';
import { SeriesRemovedEvent } from '../_models/events/series-removed-event';
import { LibraryType } from '../_models/library';
import { MangaFormat } from '../_models/manga-format';
import { Series } from '../_models/series';
import { SeriesMetadata } from '../_models/series-metadata';
import { Volume } from '../_models/volume';
import { AccountService } from '../_services/account.service';
import { ActionItem, ActionFactoryService, Action } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { ImageService } from '../_services/image.service';
import { LibraryService } from '../_services/library.service';
import { EVENTS, MessageHubService } from '../_services/message-hub.service';
import { ReaderService } from '../_services/reader.service';
import { SeriesService } from '../_services/series.service';


@Component({
  selector: 'app-series-detail',
  templateUrl: './series-detail.component.html',
  styleUrls: ['./series-detail.component.scss']
})
export class SeriesDetailComponent implements OnInit, OnDestroy {

  series!: Series;
  volumes: Volume[] = [];
  chapters: Chapter[] = [];
  libraryId = 0;
  isAdmin = false;
  hasDownloadingRole = false;
  isLoading = true;
  showBook = true;

  currentlyReadingChapter: Chapter | undefined = undefined;
  hasReadingProgress = false;


  seriesActions: ActionItem<Series>[] = [];
  volumeActions: ActionItem<Volume>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];
  bulkActions: ActionItem<any>[] = [];

  hasSpecials = false;
  specials: Array<Chapter> = [];
  activeTabId = 2;
  hasNonSpecialVolumeChapters = true;

  seriesSummary: string = '';
  userReview: string = '';
  libraryType: LibraryType = LibraryType.Manga;
  seriesMetadata: SeriesMetadata | null = null;
  /**
   * Poster image for the Series
   */
  seriesImage: string = '';
  downloadInProgress: boolean = false;

  /**
   * Tricks the cover images for volume/chapter cards to update after we update one of them
   */
  coverImageOffset: number = 0;

  /**
   * If an action is currently being done, don't let the user kick off another action
   */
  actionInProgress: boolean = false;

  /**
   * Track by function for Volume to tell when to refresh card data
   */
  trackByVolumeIdentity = (index: number, item: Volume) => `${item.name}_${item.pagesRead}`;
  /**
   * Track by function for Chapter to tell when to refresh card data
   */
  trackByChapterIdentity = (index: number, item: Chapter) => `${item.title}_${item.number}_${item.pagesRead}`;

  bulkActionCallback = (action: Action, data: any) => {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;
    // we need to figure out what is actually selected now
    const selectedVolumeIndexes = this.bulkSelectionService.getSelectedCardsForSource('volume');
    const selectedChapterIndexes = this.bulkSelectionService.getSelectedCardsForSource('chapter');
    const selectedSpecialIndexes = this.bulkSelectionService.getSelectedCardsForSource('special');

    const selectedChapterIds = this.chapters.filter((_chapter, index: number) => selectedChapterIndexes.includes(index + ''));
    const selectedVolumeIds = this.volumes.filter((_volume, index: number) => selectedVolumeIndexes.includes(index + ''));
    const selectedSpecials = this.specials.filter((_chapter, index: number) => selectedSpecialIndexes.includes(index + ''));
    const chapters = [...selectedChapterIds, ...selectedSpecials];

    switch (action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleToReadingList(seriesId, selectedVolumeIds, chapters, () => {
          this.actionInProgress = false;
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleAsRead(seriesId, selectedVolumeIds, chapters,  () => {
          this.setContinuePoint();
          this.actionInProgress = false;
          this.bulkSelectionService.deselectAll();
        });
        
        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleAsUnread(seriesId, selectedVolumeIds, chapters,  () => {
          this.setContinuePoint();
          this.actionInProgress = false;
          this.bulkSelectionService.deselectAll();
        });
        break;
    }
  }

  private onDestroy: Subject<void> = new Subject();


  get LibraryType(): typeof LibraryType {
    return LibraryType;
  }

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  get TagBadgeCursor(): typeof TagBadgeCursor {
    return TagBadgeCursor;
  }

  constructor(private route: ActivatedRoute, private seriesService: SeriesService,
              private ratingConfig: NgbRatingConfig, private router: Router,
              private modalService: NgbModal, public readerService: ReaderService,
              public utilityService: UtilityService, private toastr: ToastrService,
              private accountService: AccountService, public imageService: ImageService,
              private actionFactoryService: ActionFactoryService, private libraryService: LibraryService,
              private confirmService: ConfirmService, private titleService: Title,
              private downloadService: DownloadService, private actionService: ActionService,
              public imageSerivce: ImageService, private messageHub: MessageHubService, 
              public bulkSelectionService: BulkSelectionService) {
    ratingConfig.max = 5;
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.hasDownloadingRole = this.accountService.hasDownloadRole(user);
      }
    });
  }

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('seriesId');
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null || libraryId == null) {
      this.router.navigateByUrl('/libraries');
      return;
    }

    this.messageHub.scanSeries.pipe(takeUntil(this.onDestroy)).subscribe((event: ScanSeriesEvent) => {
      if (event.seriesId == this.series.id)
      this.loadSeries(seriesId);
      this.seriesImage = this.imageService.randomize(this.imageService.getSeriesCoverImage(this.series.id));
      this.toastr.success('Scan series completed');
    });

    this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(event => {
      if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemovedEvent = event.payload as SeriesRemovedEvent;
        if (seriesRemovedEvent.seriesId === this.series.id) {
          this.toastr.info('This series no longer exists');
          this.router.navigateByUrl('/libraries');
        }
      }
    });

    const seriesId = parseInt(routeId, 10);
    this.libraryId = parseInt(libraryId, 10);
    this.seriesImage = this.imageService.getSeriesCoverImage(seriesId);
    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
      this.loadSeries(seriesId);
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }

  handleSeriesActionCallback(action: Action, series: Series) {
    this.actionInProgress = true;
    switch(action) {
      case(Action.MarkAsRead):
        this.actionService.markSeriesAsRead(series, (series: Series) => {
          this.actionInProgress = false;
          this.loadSeries(series.id);
        });
        break;
      case(Action.MarkAsUnread):
        this.actionService.markSeriesAsUnread(series, (series: Series) => {
          this.actionInProgress = false;
          this.loadSeries(series.id);
        });
        break;
      case(Action.ScanLibrary):
        this.actionService.scanSeries(series, () => this.actionInProgress = false);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshMetdata(series, () => this.actionInProgress = false);
        break;
      case(Action.Delete):
        this.deleteSeries(series);
        break;
      case(Action.Bookmarks):
        this.actionService.openBookmarkModal(series, () => this.actionInProgress = false);
        break;
      case(Action.AddToReadingList):
        this.actionService.addSeriesToReadingList(series, () => this.actionInProgress = false);
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
      case(Action.Edit):
        this.openViewInfo(volume);
        break;
      case(Action.AddToReadingList):
        this.actionService.addVolumeToReadingList(volume, this.series.id, () => {/* No Operation */ });
        break;
      case(Action.IncognitoRead):
        if (volume.chapters != undefined && volume.chapters?.length >= 1) {
          this.openChapter(volume.chapters.sort(this.utilityService.sortChapters)[0], true);
        }
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
      case(Action.Edit):
        this.openViewInfo(chapter);
        break;
      case(Action.AddToReadingList):
        this.actionService.addChapterToReadingList(chapter, this.series.id, () => {/* No Operation */ });
        break;
      case(Action.IncognitoRead):
        this.openChapter(chapter, true);
        break;
      default:
        break;
    }
  }


  async deleteSeries(series: Series) {
    if (!await this.confirmService.confirm('Are you sure you want to delete this series? It will not modify files on disk.')) {
      this.actionInProgress = false;
      return;
    }

    this.seriesService.delete(series.id).subscribe((res: boolean) => {
      if (res) {
        this.toastr.success('Series deleted');
        this.router.navigate(['library', this.libraryId]);
      }
      this.actionInProgress = false;
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
      series.pagesRead = series.pages;
      this.toastr.success(series.name + ' is now read');
      this.loadSeries(series.id);
    });
  }

  loadSeries(seriesId: number) {
    this.coverImageOffset = 0;

    forkJoin([
      this.seriesService.getMetadata(seriesId),
      this.seriesService.getSeries(seriesId)
    ]).subscribe(results => {
      this.seriesMetadata = results[0];
      this.series = results[1];

      this.createHTML();

      this.titleService.setTitle('Kavita - ' + this.series.name + ' Details');

      this.seriesActions = this.actionFactoryService.getSeriesActions(this.handleSeriesActionCallback.bind(this))
              .filter(action => action.action !== Action.Edit)
              .filter(action => this.actionFactoryService.filterBookmarksForFormat(action, this.series));
      this.volumeActions = this.actionFactoryService.getVolumeActions(this.handleVolumeActionCallback.bind(this));
      this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this));
      
      

      this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
        this.chapters = volumes.filter(v => v.number === 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters); 
        this.volumes = volumes.sort(this.utilityService.sortVolumes);

        this.setContinuePoint();
        const vol0 = this.volumes.filter(v => v.number === 0);
        this.hasSpecials = vol0.map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters).filter(c => c.isSpecial || isNaN(parseInt(c.range, 10))).length > 0 ;
        if (this.hasSpecials) {
          this.specials = vol0.map(v => v.chapters || [])
          .flat()
          .filter(c => c.isSpecial || isNaN(parseInt(c.range, 10)))
          .map(c => {
            c.title = this.utilityService.cleanSpecialTitle(c.title);
            c.range = this.utilityService.cleanSpecialTitle(c.range);
            return c;
          });
        }

        if (this.volumes.filter(v => v.number !== 0).length === 0 && this.chapters.filter(c => !c.isSpecial).length === 0 && this.specials.length > 0) {
          this.activeTabId = 1;
          this.hasNonSpecialVolumeChapters = false;
        }

        // If an update occured and we were on specials, re-activate Volumes/Chapters 
        if (!this.hasSpecials && this.activeTabId != 2) {
          this.activeTabId = 2;
        }

        this.isLoading = false;
      });
    }, err => {
      this.router.navigateByUrl('/libraries');
    });
  }

  createHTML() {
    if (this.seriesMetadata !== null) {
      // ?! BUG: This is broken. Need undefined check
      this.seriesSummary = (this.seriesMetadata.summary === null ? '' : this.seriesMetadata.summary).replace(/\n/g, '<br>');
    }
    
    this.userReview = (this.series.userReview === null ? '' : this.series.userReview).replace(/\n/g, '<br>');
  }

  setContinuePoint() {
    this.hasReadingProgress = this.volumes.filter(v => v.pagesRead > 0).length > 0 || this.chapters.filter(c => c.pagesRead > 0).length > 0;
    this.currentlyReadingChapter = this.readerService.getCurrentChapter(this.volumes);
  }

  markAsRead(vol: Volume) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    this.actionService.markVolumeAsRead(seriesId, vol, () => {
      this.setContinuePoint();
      this.actionInProgress = false;
    });
  }

  markAsUnread(vol: Volume) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    this.actionService.markVolumeAsUnread(seriesId, vol, () => {
      this.setContinuePoint();
      this.actionInProgress = false;
    });
  }

  markChapterAsRead(chapter: Chapter) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;
    
    this.actionService.markChapterAsRead(seriesId, chapter, () => {
      this.setContinuePoint();
      this.actionInProgress = false;
    });
  }

  markChapterAsUnread(chapter: Chapter) {
    if (this.series === undefined) {
      return;
    }
    const seriesId = this.series.id;

    this.actionService.markChapterAsUnread(seriesId, chapter, () => {
      this.setContinuePoint();
      this.actionInProgress = false;
    });
  }

  read() {
    if (this.currentlyReadingChapter !== undefined) { this.openChapter(this.currentlyReadingChapter); }
  }

  updateRating(rating: any) {
    if (this.series === undefined) {
      return;
    }

    this.seriesService.updateRating(this.series?.id, this.series?.userRating, this.series?.userReview).subscribe(() => {
      this.createHTML();
    });
  }

  openChapter(chapter: Chapter, incognitoMode = false) {
    if (chapter.pages === 0) {
      this.toastr.error('There are no pages. Kavita was not able to read this archive.');
      return;
    }

    if (chapter.files.length > 0 && chapter.files[0].format === MangaFormat.EPUB) {
      this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'book', chapter.id], {queryParams: {incognitoMode}});
    } else {
      this.router.navigate(['library', this.libraryId, 'series', this.series?.id, 'manga', chapter.id], {queryParams: {incognitoMode}});
    }
  }

  openVolume(volume: Volume) {
    if (volume.chapters === undefined || volume.chapters?.length === 0) {
      this.toastr.error('There are no chapters to this volume. Cannot read.');
      return;
    }
    // NOTE: When selecting a volume, we might want to ask the user which chapter they want or an "Automatic" option. For Volumes 
    // made up of lots of chapter files, it makes it more versitile. The modal can have pages read / pages with colored background 
    // to help the user make a good choice. 

    // If user has progress on the volume, load them where they left off
    if (volume.pagesRead < volume.pages && volume.pagesRead > 0) {
      // Find the continue point chapter and load it
      this.openChapter(this.readerService.getCurrentChapter([volume]));
      return;
    }

    // Sort the chapters, then grab first if no reading progress
    this.openChapter([...volume.chapters].sort(this.utilityService.sortChapters)[0]);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  openViewInfo(data: Volume | Chapter) {
    const modalRef = this.modalService.open(CardDetailsModalComponent, { size: 'lg' }); // , scrollable: true (these don't work well on mobile)
    modalRef.componentInstance.data = data;
    modalRef.componentInstance.parentName = this.series?.name;
    modalRef.componentInstance.seriesId = this.series?.id;
    modalRef.componentInstance.libraryId = this.series?.libraryId;
    modalRef.closed.subscribe((result: {coverImageUpdate: boolean}) => {
      if (result.coverImageUpdate) {
        this.coverImageOffset += 1;
      }
    });
  }

  openEditSeriesModal() {
    const modalRef = this.modalService.open(EditSeriesModalComponent, {  size: 'lg' }); // scrollable: true, size: 'lg', windowClass: 'scrollable-modal' (these don't work well on mobile)
    modalRef.componentInstance.series = this.series;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series, coverImageUpdate: boolean}) => {
      window.scrollTo(0, 0);
      if (closeResult.success) {
        this.loadSeries(this.series.id);
        if (closeResult.coverImageUpdate) {
          // Random triggers a load change without any problems with API
          this.seriesImage = this.imageService.randomize(this.imageService.getSeriesCoverImage(this.series.id));
        }
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

  downloadSeries() {
    this.downloadService.downloadSeriesSize(this.series.id).pipe(take(1)).subscribe(async (size) => {
      const wantToDownload = await this.downloadService.confirmSize(size, 'series');
      if (!wantToDownload) { return; }
      this.downloadInProgress = true;
      this.downloadService.downloadSeries(this.series).pipe(
        takeWhile(val => {
          return val.state != 'DONE';
        }),
        finalize(() => {
          this.downloadInProgress = false;
        })).subscribe(() => {/* No Operation */});;
    });
  }
}
