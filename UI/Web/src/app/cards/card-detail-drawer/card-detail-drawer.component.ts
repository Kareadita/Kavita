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
import { Observable, of, map, shareReplay } from 'rxjs';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/metadata/chapter-metadata';
import { Device } from 'src/app/_models/device/device';
import { LibraryType } from 'src/app/_models/library';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Volume } from 'src/app/_models/volume';
import { AccountService } from 'src/app/_services/account.service';
import { ActionItem, ActionFactoryService, Action } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { UploadService } from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CommonModule} from "@angular/common";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {ImageComponent} from "../../shared/image/image.component";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {EntityInfoCardsComponent} from "../entity-info-cards/entity-info-cards.component";
import {CoverImageChooserComponent} from "../cover-image-chooser/cover-image-chooser.component";
import {ChapterMetadataDetailComponent} from "../chapter-metadata-detail/chapter-metadata-detail.component";
import {CardActionablesComponent} from "../card-item/card-actionables/card-actionables.component";
import {DefaultDatePipe} from "../../pipe/default-date.pipe";
import {BytesPipe} from "../../pipe/bytes.pipe";
import {BadgeExpanderComponent} from "../../shared/badge-expander/badge-expander.component";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";

enum TabID {
  General = 0,
  Metadata = 1,
  Cover = 2,
  Files = 3
}

@Component({
  selector: 'app-card-detail-drawer',
  standalone: true,
  imports: [CommonModule, EntityTitleComponent, NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, ImageComponent, ReadMoreComponent, EntityInfoCardsComponent, CoverImageChooserComponent, ChapterMetadataDetailComponent, CardActionablesComponent, DefaultDatePipe, BytesPipe, NgbNavOutlet, BadgeExpanderComponent, TagBadgeComponent, PersonBadgeComponent, TranslocoDirective],
  templateUrl: './card-detail-drawer.component.html',
  styleUrls: ['./card-detail-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardDetailDrawerComponent implements OnInit {

  @Input() parentName = '';
  @Input() seriesId: number = 0;
  @Input() libraryId: number = 0;
  @Input({required: true}) data!: Volume | Chapter;
  private readonly destroyRef = inject(DestroyRef);
  private readonly translocoService = inject(TranslocoService);


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
    {title: 'info-tab', disabled: false}
  ];
  active = this.tabs[0];

  chapterMetadata!: ChapterMetadata;
  summary: string = '';

  downloadInProgress: boolean = false;

  get MangaFormat() {
    return MangaFormat;
  }

  get Breakpoint() {
    return Breakpoint;
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
    private seriesService: SeriesService, private readerService: ReaderService,
    public activeOffcanvas: NgbActiveOffcanvas, private downloadService: DownloadService, private readonly cdRef: ChangeDetectorRef) {
      this.isAdmin$ = this.accountService.currentUser$.pipe(
        takeUntilDestroyed(this.destroyRef),
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
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }

  performAction(action: ActionItem<any>, chapter: Chapter) {
    if (typeof action.callback === 'function') {
      action.callback(action, chapter);
    }
  }

  applyCoverImage(coverUrl: string) {
    this.uploadService.updateChapterCoverImage(this.chapter.id, coverUrl).subscribe(() => {});
  }

  resetCoverImage() {
    this.uploadService.resetChapterCoverLock(this.chapter.id).subscribe(() => {
      this.toastr.info(this.translocoService.translate('toasts.regen-cover'));
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
      this.toastr.error(this.translocoService.translate('toasts.no-pages'));
      return;
    }

    const params = this.readerService.getQueryParamsObject(incognito, false);
    this.router.navigate(this.readerService.getNavigationArray(this.libraryId, this.seriesId, chapter.id, chapter.files[0].format), {queryParams: params});
    this.close();
  }

  download(chapter: Chapter) {
    if (this.downloadInProgress) {
      this.toastr.info(this.translocoService.translate('toasts.download-in-progress'));
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
