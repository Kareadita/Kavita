import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit
} from '@angular/core';
import { Router } from '@angular/router';
import {
  NgbActiveOffcanvas,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet
} from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Observable, of } from 'rxjs';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import {Chapter, LooseLeafOrDefaultNumber} from 'src/app/_models/chapter';
import { Device } from 'src/app/_models/device/device';
import { LibraryType } from 'src/app/_models/library/library';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Volume } from 'src/app/_models/volume';
import { AccountService } from 'src/app/_services/account.service';
import { ActionItem, ActionFactoryService, Action } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { UploadService } from 'src/app/_services/upload.service';
import {CommonModule} from "@angular/common";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {ImageComponent} from "../../shared/image/image.component";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {EntityInfoCardsComponent} from "../entity-info-cards/entity-info-cards.component";
import {CoverImageChooserComponent} from "../cover-image-chooser/cover-image-chooser.component";
import {ChapterMetadataDetailComponent} from "../chapter-metadata-detail/chapter-metadata-detail.component";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {BadgeExpanderComponent} from "../../shared/badge-expander/badge-expander.component";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {EditChapterProgressComponent} from "../edit-chapter-progress/edit-chapter-progress.component";

enum TabID {
  General = 0,
  Metadata = 1,
  Cover = 2,
  Progress = 3,
  Files = 4
}

@Component({
  selector: 'app-card-detail-drawer',
  standalone: true,
  imports: [CommonModule, EntityTitleComponent, NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, ImageComponent, ReadMoreComponent, EntityInfoCardsComponent, CoverImageChooserComponent, ChapterMetadataDetailComponent, CardActionablesComponent, DefaultDatePipe, BytesPipe, NgbNavOutlet, BadgeExpanderComponent, TagBadgeComponent, PersonBadgeComponent, TranslocoDirective, EditChapterProgressComponent],
  templateUrl: './card-detail-drawer.component.html',
  styleUrls: ['./card-detail-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardDetailDrawerComponent implements OnInit {

  protected readonly utilityService = inject(UtilityService);
  protected readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  private readonly toastr = inject(ToastrService);
  protected readonly accountService = inject(AccountService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly router = inject(Router);
  private readonly libraryService = inject(LibraryService);
  private readonly readerService = inject(ReaderService);
  protected readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly downloadService = inject(DownloadService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly MangaFormat = MangaFormat;
  protected readonly Breakpoint = Breakpoint;
  protected readonly LibraryType = LibraryType;
  protected readonly TabID = TabID;
  protected readonly LooseLeafOrSpecialNumber = LooseLeafOrDefaultNumber;

  @Input() parentName = '';
  @Input() seriesId: number = 0;
  @Input() libraryId: number = 0;
  @Input({required: true}) data!: Volume | Chapter;

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


  tabs = [
    {title: 'general-tab', disabled: false},
    {title: 'metadata-tab', disabled: false},
    {title: 'cover-tab', disabled: false},
    {title: 'progress-tab', disabled: false},
    {title: 'info-tab', disabled: false}
  ];
  active = this.tabs[0];

  summary: string = '';
  downloadInProgress: boolean = false;

  ngOnInit(): void {
    this.imageUrls = this.chapters.map(c => this.imageService.getChapterCoverImage(c.id));
    this.isChapter = this.utilityService.isChapter(this.data);
    this.chapter = this.utilityService.isChapter(this.data) ? (this.data as Chapter) : (this.data as Volume).chapters[0];


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
    this.chapterActions.push({title: 'read', action: Action.Read, callback: this.handleChapterActionCallback.bind(this), requiresAdmin: false, children: []});
    if (this.isChapter) {
      const chapter = this.utilityService.asChapter(this.data);
      this.chapterActions = this.actionFactoryService.filterSendToAction(this.chapterActions, chapter);
    } else {
      this.chapterActions = this.actionFactoryService.filterSendToAction(this.chapterActions, this.chapters[0]);
    }

    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
      this.cdRef.markForCheck();
    });


    const collator = new Intl.Collator(undefined, {numeric: true, sensitivity: 'base'});
    this.chapters.forEach((c: Chapter) => {
      c.files.sort((a: MangaFile, b: MangaFile) => collator.compare(a.filePath, b.filePath));
    });

    this.imageUrls = this.chapters.map(c => this.imageService.getChapterCoverImage(c.id));

    this.cdRef.markForCheck();
  }


  close() {
    this.activeOffcanvas.close();
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.minNumber === LooseLeafOrDefaultNumber) {
      return '1';
    }
    return chapter.range + '';
  }

  performAction(action: ActionItem<any>, chapter: Chapter) {
    if (typeof action.callback === 'function') {
      action.callback(action, chapter);
    }
  }

  applyCoverImage(coverUrl: string) {
    this.uploadService.updateChapterCoverImage(this.chapter.id, coverUrl).subscribe(() => {});
  }

  updateCoverImageIndex(selectedIndex: number) {
    if (selectedIndex <= 0) return;
    this.applyCoverImage(this.imageUrls[selectedIndex]);
  }

  resetCoverImage() {
    this.uploadService.resetChapterCoverLock(this.chapter.id).subscribe(() => {
      this.toastr.info(translate('toasts.regen-cover'));
    });
  }

  markChapterAsRead(chapter: Chapter) {
    if (this.seriesId === 0) {
      return;
    }

    this.actionService.markChapterAsRead(this.libraryId, this.seriesId, chapter, () => { this.cdRef.markForCheck(); });
  }

  markChapterAsUnread(chapter: Chapter) {
    if (this.seriesId === 0) {
      return;
    }

    this.actionService.markChapterAsUnread(this.libraryId, this.seriesId, chapter, () => { this.cdRef.markForCheck(); });
  }

  handleChapterActionCallback(action: ActionItem<Chapter>, chapter: Chapter) {
    switch (action.action) {
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
      case (Action.SendTo):
      {
        const device = (action._extra!.data as Device);
        this.actionService.sendToDevice([chapter.id], device);
        break;
      }
      default:
        break;
    }
  }

  readChapter(chapter: Chapter, incognito: boolean = false) {
    if (chapter.pages === 0) {
      this.toastr.error(translate('toasts.no-pages'));
      return;
    }

    const params = this.readerService.getQueryParamsObject(incognito, false);
    this.router.navigate(this.readerService.getNavigationArray(this.libraryId, this.seriesId, chapter.id, chapter.files[0].format), {queryParams: params});
    this.close();
  }

  download(chapter: Chapter) {
    if (this.downloadInProgress) {
      this.toastr.info(translate('toasts.download-in-progress'));
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
