import { Component, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { UploadService } from 'src/app/_services/upload.service';
import { ChangeCoverImageModalComponent } from '../change-cover-image/change-cover-image-modal.component';
import { LibraryType } from '../../../_models/library';
import { LibraryService } from '../../../_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';
import { Series } from 'src/app/_models/series';



@Component({
  selector: 'app-card-details-modal',
  templateUrl: './card-details-modal.component.html',
  styleUrls: ['./card-details-modal.component.scss']
})
export class CardDetailsModalComponent implements OnInit {

  @Input() parentName = '';
  @Input() seriesId: number = 0;
  @Input() libraryId: number = 0;
  @Input() data!: any; // Volume | Chapter
  isChapter = false;
  chapters: Chapter[] = [];
  seriesVolumes: any[] = [];
  isLoadingVolumes = false;
  formatKeys = Object.keys(MangaFormat);
  /**
   * If a cover image update occured. 
   */
  coverImageUpdate: boolean = false; 
  isAdmin: boolean = false;
  actions: ActionItem<any>[] = [];
  chapterActions: ActionItem<Chapter>[] = [];
  libraryType: LibraryType = LibraryType.Manga; 
  series: Series | undefined = undefined;

  get LibraryType(): typeof LibraryType {
    return LibraryType;
  }

  constructor(private modalService: NgbModal, public modal: NgbActiveModal, public utilityService: UtilityService, 
    public imageService: ImageService, private uploadService: UploadService, private toastr: ToastrService, 
    private accountService: AccountService, private actionFactoryService: ActionFactoryService, 
    private actionService: ActionService, private router: Router, private libraryService: LibraryService,
    private seriesService: SeriesService) { }

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.data);

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      }
    });

    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
    });

    this.chapterActions = this.actionFactoryService.getChapterActions(this.handleChapterActionCallback.bind(this)).filter(item => item.action !== Action.Edit);

    if (this.isChapter) {
      this.chapters.push(this.data);
    } else if (!this.isChapter) {
      this.chapters.push(...this.data?.chapters);
    }
    this.chapters.sort(this.utilityService.sortChapters);
    this.chapters.forEach(c => c.coverImage = this.imageService.getChapterCoverImage(c.id));
    // Try to show an approximation of the reading order for files
    var collator = new Intl.Collator(undefined, {numeric: true, sensitivity: 'base'});
    this.chapters.forEach((c: Chapter) => {
      c.files.sort((a: MangaFile, b: MangaFile) => collator.compare(a.filePath, b.filePath));
    });

    this.seriesService.getSeries(this.seriesId).subscribe(series => {
      this.series = series;
    })
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

  updateCover() {
    const modalRef = this.modalService.open(ChangeCoverImageModalComponent, {  size: 'lg' }); // scrollable: true, size: 'lg', windowClass: 'scrollable-modal' (these don't work well on mobile)
    if (this.utilityService.isChapter(this.data)) {
      const chapter = this.utilityService.asChapter(this.data)
      chapter.coverImage = this.imageService.getChapterCoverImage(chapter.id);
      modalRef.componentInstance.chapter = chapter;
      modalRef.componentInstance.title = 'Select ' + (chapter.isSpecial ? '' : this.utilityService.formatChapterName(this.libraryType, false, true)) + chapter.range + '\'s Cover';
    } else {
      const volume = this.utilityService.asVolume(this.data);
      const chapters = volume.chapters;
      if (chapters && chapters.length > 0) {
        modalRef.componentInstance.chapter = chapters[0];
        modalRef.componentInstance.title = 'Select Volume ' + volume.number + '\'s Cover';
      }
    }
    
    modalRef.closed.subscribe((closeResult: {success: boolean, chapter: Chapter, coverImageUpdate: boolean}) => {
      if (closeResult.success) {
        this.coverImageUpdate = closeResult.coverImageUpdate;
        if (!this.coverImageUpdate) {
          this.uploadService.resetChapterCoverLock(closeResult.chapter.id).subscribe(() => {
            this.toastr.info('Please refresh in a bit for the cover image to be reflected.');
          });
        } else {
          closeResult.chapter.coverImage = this.imageService.randomize(this.imageService.getChapterCoverImage(closeResult.chapter.id));
        }
      }
    });
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
}
