import { Component, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { UploadService } from 'src/app/_services/upload.service';
import { LibraryType } from '../../../_models/library';
import { LibraryService } from '../../../_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';
import { Series } from 'src/app/_models/series';
import { PersonRole } from 'src/app/_models/person';
import { Volume } from 'src/app/_models/volume';
import { ChapterMetadata } from 'src/app/_models/chapter-metadata';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { ReaderService } from 'src/app/_services/reader.service';
import { MetadataService } from 'src/app/_services/metadata.service';



@Component({
  selector: 'app-card-details-modal',
  templateUrl: './card-details-modal.component.html',
  styleUrls: ['./card-details-modal.component.scss']
})
export class CardDetailsModalComponent implements OnInit {

  @Input() parentName = '';
  @Input() seriesId: number = 0;
  @Input() libraryId: number = 0;
  @Input() data!: Volume | Chapter; // Volume | Chapter
  
  /**
   * If this is a volume, this will be first chapter for said volume.
   */
  chapter!: Chapter;
  isChapter = false;
  chapters: Chapter[] = [];

  
  /**
   * If a cover image update occured. 
   */
  coverImageUpdate: boolean = false; 
  coverImageIndex: number = 0;
  /**
   * Url of the selected cover
   */
  selectedCover: string = '';
  coverImageLocked: boolean = false;
  /**
   * When the API is doing work
   */
  coverImageSaveLoading: boolean = false;
  imageUrls: Array<string> = [];


  actions: ActionItem<any>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];
  libraryType: LibraryType = LibraryType.Manga; 

  bookmarks: PageBookmark[] = [];

  tabs = [{title: 'General', disabled: false}, {title: 'Metadata', disabled: false}, {title: 'Cover', disabled: false}, {title: 'Bookmarks', disabled: false}, {title: 'Info', disabled: false}];
  active = this.tabs[0];

  chapterMetadata!: ChapterMetadata;
  ageRating!: string;
  

  get Breakpoint(): typeof Breakpoint {
    return Breakpoint;
  }

  get PersonRole() {
    return PersonRole;
  }

  get LibraryType(): typeof LibraryType {
    return LibraryType;
  }

  constructor(public modal: NgbActiveModal, public utilityService: UtilityService, 
    public imageService: ImageService, private uploadService: UploadService, private toastr: ToastrService, 
    private accountService: AccountService, private actionFactoryService: ActionFactoryService, 
    private actionService: ActionService, private router: Router, private libraryService: LibraryService,
    private seriesService: SeriesService, private readerService: ReaderService, public metadataService: MetadataService) { }

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.data);

    this.chapter = this.utilityService.isChapter(this.data) ? (this.data as Chapter) : (this.data as Volume).chapters[0];

    this.imageUrls.push(this.imageService.getChapterCoverImage(this.chapter.id));

    let bookmarkApi;
    if (this.isChapter) {
      bookmarkApi = this.readerService.getBookmarks(this.chapter.id);
    } else {
      bookmarkApi = this.readerService.getBookmarksForVolume(this.data.id);
    }

    bookmarkApi.pipe(take(1)).subscribe(bookmarks => {
      this.bookmarks = bookmarks;
    });

    this.seriesService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
      this.chapterMetadata = metadata;

      this.metadataService.getAgeRating(this.chapterMetadata.ageRating).subscribe(ageRating => this.ageRating = ageRating);
    });
    

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        if (!this.accountService.hasAdminRole(user)) {
          this.tabs.find(s => s.title === 'Cover')!.disabled = true;
        }
      }
    });

    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
    });

    this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this)).filter(item => item.action !== Action.Edit);

    if (this.isChapter) {
      this.chapters.push(this.data as Chapter);
    } else if (!this.isChapter) {
      this.chapters.push(...(this.data as Volume).chapters);
    }
    // TODO: Move this into the backend
    this.chapters.sort(this.utilityService.sortChapters);
    this.chapters.forEach(c => c.coverImage = this.imageService.getChapterCoverImage(c.id));
    // Try to show an approximation of the reading order for files
    var collator = new Intl.Collator(undefined, {numeric: true, sensitivity: 'base'});
    this.chapters.forEach((c: Chapter) => {
      c.files.sort((a: MangaFile, b: MangaFile) => collator.compare(a.filePath, b.filePath));
    });
  }

  close() {
    this.modal.close({coverImageUpdate: this.coverImageUpdate});
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

  updateSelectedIndex(index: number) {
    this.coverImageIndex = index;
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
  }

  handleReset() {
    this.coverImageLocked = false;
  }

  saveCoverImage() {
    this.coverImageSaveLoading = true;
    const selectedIndex = this.coverImageIndex || 0;
    if (selectedIndex > 0) {
      this.uploadService.updateChapterCoverImage(this.chapter.id, this.selectedCover).subscribe(() => {
        if (this.coverImageIndex > 0) {
          this.chapter.coverImageLocked = true;
          this.coverImageUpdate = true;
        }
        this.coverImageSaveLoading = false;
      }, err => this.coverImageSaveLoading = false);
    } else if (this.coverImageLocked === false) {
      this.uploadService.resetChapterCoverLock(this.chapter.id).subscribe(() => {
        this.toastr.info('Cover image reset');
        this.coverImageSaveLoading = false;
        this.coverImageUpdate = true;
      });
    }
  }

  markChapterAsRead(chapter: Chapter) {
    if (this.seriesId === 0) {
      return;
    }
    
    this.actionService.markChapterAsRead(this.seriesId, chapter, () => { /* No Action */ });
  }

  markChapterAsUnread(chapter: Chapter) {
    if (this.seriesId === 0) {
      return;
    }

    this.actionService.markChapterAsUnread(this.seriesId, chapter, () => { /* No Action */ });
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
      default:
        break;
    }
  }

  readChapter(chapter: Chapter) {
    if (chapter.pages === 0) {
      this.toastr.error('There are no pages. Kavita was not able to read this archive.');
      return;
    }

    if (chapter.files.length > 0 && chapter.files[0].format === MangaFormat.EPUB) {
      this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'book', chapter.id]);
    } else {
      this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'manga', chapter.id]);
    }
  }

  removeBookmark(bookmark: PageBookmark, index: number) {
    this.readerService.unbookmark(bookmark.seriesId, bookmark.volumeId, bookmark.chapterId, bookmark.page).subscribe(() => {
      this.bookmarks.splice(index, 1);
    });
  }
}
