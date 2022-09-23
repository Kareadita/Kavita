import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NgbActiveOffcanvas } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { finalize, Observable, of, Subject, take, takeWhile, takeUntil, map, shareReplay } from 'rxjs';
import { Download } from 'src/app/shared/_models/download';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/chapter-metadata';
import { LibraryType } from 'src/app/_models/library';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { PersonRole } from 'src/app/_models/person';
import { Volume } from 'src/app/_models/volume';
import { AccountService } from 'src/app/_services/account.service';
import { ActionItem, ActionFactoryService, Action } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { UploadService } from 'src/app/_services/upload.service';

enum TabID {
  General = 0, 
  Metadata = 1,
  Cover = 2,
  Files = 3
}

@Component({
  selector: 'app-card-detail-drawer',
  templateUrl: './card-detail-drawer.component.html',
  styleUrls: ['./card-detail-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardDetailDrawerComponent implements OnInit, OnDestroy {

  @Input() parentName = '';
  @Input() seriesId: number = 0;
  @Input() libraryId: number = 0;
  @Input() data!: Volume | Chapter;
  
  /**
   * If this is a volume, this will be first chapter for said volume.
   */
  chapter!: Chapter;
  isChapter = false;
  chapters: Chapter[] = [];

  imageUrls: Array<string> = [];
  /**
   * Cover image for the entity
   */
  coverImageUrl!: string;

  isAdmin$: Observable<boolean> = of(false);


  actions: ActionItem<any>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];
  libraryType: LibraryType = LibraryType.Manga; 


  tabs = [{title: 'General', disabled: false}, {title: 'Metadata', disabled: false}, {title: 'Cover', disabled: false}, {title: 'Info', disabled: false}];
  active = this.tabs[0];

  chapterMetadata!: ChapterMetadata;
  summary: string = '';

  download$: Observable<Download> | null = null;
  downloadInProgress: boolean = false;
  
  private readonly onDestroy = new Subject<void>();

  get MangaFormat() {
    return MangaFormat;
  }

  get Breakpoint() {
    return Breakpoint;
  }

  get PersonRole() {
    return PersonRole;
  }

  get LibraryType() {
    return LibraryType;
  }

  get TabID() {
    return TabID;
  }

  constructor(public utilityService: UtilityService, 
    public imageService: ImageService, private uploadService: UploadService, private toastr: ToastrService, 
    private accountService: AccountService, private actionFactoryService: ActionFactoryService, 
    private actionService: ActionService, private router: Router, private libraryService: LibraryService,
    private seriesService: SeriesService, private readerService: ReaderService, public metadataService: MetadataService, 
    public activeOffcanvas: NgbActiveOffcanvas, private downloadService: DownloadService, private readonly cdRef: ChangeDetectorRef) {
      this.isAdmin$ = this.accountService.currentUser$.pipe(
        takeUntil(this.onDestroy), 
        map(user => (user && this.accountService.hasAdminRole(user)) || false), 
        shareReplay()
      );
  }

  ngOnInit(): void {
    this.imageUrls = this.chapters.map(c => this.imageService.getChapterCoverImage(c.id));
    this.isChapter = this.utilityService.isChapter(this.data);
    this.chapter = this.utilityService.isChapter(this.data) ? (this.data as Chapter) : (this.data as Volume).chapters[0];

    this.seriesService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
      this.chapterMetadata = metadata;
      this.cdRef.markForCheck();
    });

    if (this.isChapter) {
      this.coverImageUrl = this.imageService.getChapterCoverImage(this.data.id);
      this.summary = this.utilityService.asChapter(this.data).summary || '';
      this.chapters.push(this.data as Chapter);
    } else {
      this.coverImageUrl = this.imageService.getVolumeCoverImage(this.data.id);
      this.summary = this.utilityService.asVolume(this.data).chapters[0].summary || '';
      this.chapters.push(...(this.data as Volume).chapters);
    }

    this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this))
                                .filter(item => item.action !== Action.Edit);
    this.chapterActions.push({title: 'Read', action: Action.Read, callback: this.handleChapterActionCallback.bind(this), requiresAdmin: false, children: []});    

    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
      this.cdRef.markForCheck();
    });

    
    var collator = new Intl.Collator(undefined, {numeric: true, sensitivity: 'base'});
    this.chapters.forEach((c: Chapter) => {
      c.files.sort((a: MangaFile, b: MangaFile) => collator.compare(a.filePath, b.filePath));
    });

    this.imageUrls = this.chapters.map(c => this.imageService.getChapterCoverImage(c.id));

    this.cdRef.markForCheck();
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  close() {
    this.activeOffcanvas.close();
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }

  performAction(action: ActionItem<any>, chapter: Chapter) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, chapter);
    }
  }

  applyCoverImage(coverUrl: string) {
    this.uploadService.updateChapterCoverImage(this.chapter.id, coverUrl).subscribe(() => {});
  }

  resetCoverImage() {
    this.uploadService.resetChapterCoverLock(this.chapter.id).subscribe(() => {
      this.toastr.info('A job has been enqueued to regenerate the cover image');
    });
  }

  markChapterAsRead(chapter: Chapter) {
    if (this.seriesId === 0) {
      return;
    }
    
    this.actionService.markChapterAsRead(this.seriesId, chapter, () => { this.cdRef.markForCheck(); });
  }

  markChapterAsUnread(chapter: Chapter) {
    if (this.seriesId === 0) {
      return;
    }

    this.actionService.markChapterAsUnread(this.seriesId, chapter, () => { this.cdRef.markForCheck(); });
  }

  handleChapterActionCallback(action: Action, chapter: Chapter) {
    switch (action) {
      case(Action.MarkAsRead):
        this.markChapterAsRead(chapter);
        break;
      case(Action.MarkAsUnread):
        this.markChapterAsUnread(chapter);
        break;
        case(Action.AddToReadingList):
        this.actionService.addChapterToReadingList(chapter, this.seriesId);
        break;
      case (Action.IncognitoRead):
        this.readChapter(chapter, true);
        break;
      case (Action.Download):
        this.download(chapter);
        break;
      case (Action.Read):
        this.readChapter(chapter, false);
        break;
      default:
        break;
    }
  }

  readChapter(chapter: Chapter, incognito: boolean = false) {
    if (chapter.pages === 0) {
      this.toastr.error('There are no pages. Kavita was not able to read this archive.');
      return;
    }

    const params = this.readerService.getQueryParamsObject(incognito, false);
    this.router.navigate(this.readerService.getNavigationArray(this.libraryId, this.seriesId, chapter.id, chapter.files[0].format), {queryParams: params});
    this.close();
  }

  download(chapter: Chapter) {
    if (this.downloadInProgress === true) {
      this.toastr.info('Download is already in progress. Please wait.');
      return;
    }

    this.downloadInProgress = true;
    this.cdRef.markForCheck();
    this.downloadService.download('chapter', chapter, (d) => {
      if (d) return;
      this.downloadInProgress = false;
      this.cdRef.markForCheck();
    });
  }

}
