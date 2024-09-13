import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {
  NgbActiveModal,
  NgbInputDatepicker,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AsyncPipe, DatePipe, DecimalPipe, NgClass, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {EntityTitleComponent} from "../../cards/entity-title/entity-title.component";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {CoverImageChooserComponent} from "../../cards/cover-image-chooser/cover-image-chooser.component";
import {EditChapterProgressComponent} from "../../cards/edit-chapter-progress/edit-chapter-progress.component";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {IconAndTitleComponent} from "../../shared/icon-and-title/icon-and-title.component";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {TranslocoDatePipe} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {ImageComponent} from "../../shared/image/image.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {Volume} from "../../_models/volume";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {ImageService} from "../../_services/image.service";
import {UploadService} from "../../_services/upload.service";
import {AccountService} from "../../_services/account.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from "../../shared/_services/download.service";
import {LibraryType} from "../../_models/library/library";
import {PersonRole} from "../../_models/metadata/person";
import {forkJoin} from "rxjs";
import { MangaFormat } from 'src/app/_models/manga-format';
import {MangaFile} from "../../_models/manga-file";
import {VolumeService} from "../../_services/volume.service";
import {User} from "../../_models/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
  Info = 'info-tab',
  Tasks = 'tasks-tab',
  Progress = 'progress-tab',
}

export interface EditVolumeModalCloseResult {
  success: boolean;
  volume: Volume;
  coverImageUpdate: boolean;
  needsReload: boolean;
  isDeleted: boolean;
}

const blackList = [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];

@Component({
  selector: 'app-edit-volume-modal',
  standalone: true,
  imports: [
    FormsModule,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    TranslocoDirective,
    AsyncPipe,
    NgbNavOutlet,
    ReactiveFormsModule,
    NgbNavItem,
    SettingItemComponent,
    NgTemplateOutlet,
    NgClass,
    TypeaheadComponent,
    EntityTitleComponent,
    TitleCasePipe,
    SettingButtonComponent,
    CoverImageChooserComponent,
    EditChapterProgressComponent,
    NgbInputDatepicker,
    CompactNumberPipe,
    IconAndTitleComponent,
    DefaultDatePipe,
    TranslocoDatePipe,
    UtcToLocalTimePipe,
    BytesPipe,
    ImageComponent,
    SafeHtmlPipe,
    DecimalPipe,
    DatePipe,
    ReadTimePipe
  ],
  templateUrl: './edit-volume-modal.component.html',
  styleUrl: './edit-volume-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditVolumeModalComponent implements OnInit {
  public readonly modal = inject(NgbActiveModal);
  public readonly utilityService = inject(UtilityService);
  public readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly accountService = inject(AccountService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly downloadService = inject(DownloadService);
  private readonly volumeService = inject(VolumeService);

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;
  protected readonly Action = Action;
  protected readonly PersonRole = PersonRole;
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) volume!: Volume;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) seriesId!: number;

  activeId = TabID.Info;
  editForm: FormGroup = new FormGroup({});
  selectedCover: string = '';
  coverImageReset = false;
  user!: User;


  tasks = this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getVolumeActions(this.runTask.bind(this)), blackList);
  /**
   * A copy of the chapter from init. This is used to compare values for name fields to see if lock was modified
   */
  initVolume!: Volume;
  imageUrls: Array<string> = [];
  size: number = 0;
  files: Array<MangaFile> = [];

  constructor() {
    this.accountService.currentUser$.subscribe(user => {
      this.user = user!;

      if (!this.accountService.hasAdminRole(user!)) {
        this.activeId = TabID.Info;
      }
      this.cdRef.markForCheck();
    });
  }


  ngOnInit() {
    this.initVolume = Object.assign({}, this.volume);
    this.imageUrls.push(this.imageService.getVolumeCoverImage(this.volume.id));

    this.files = this.volume.chapters.flatMap(c => c.files);
    this.size = this.files.reduce((sum, v) => sum + v.bytes, 0);

    this.editForm.addControl('coverImageIndex', new FormControl(0, []));
    this.editForm.addControl('coverImageLocked', new FormControl(this.volume.coverImageLocked, []));
  }

  close() {
    this.modal.dismiss();
  }

  save() {
    const selectedIndex = this.editForm.get('coverImageIndex')?.value || 0;

    const apis = [];

    if (selectedIndex > 0 || this.coverImageReset) {
      apis.push(this.uploadService.updateVolumeCoverImage(this.volume.id, this.selectedCover, !this.coverImageReset));
    }

    forkJoin(apis).subscribe(results => {
      this.modal.close({success: true, volume: this.volume, coverImageUpdate: selectedIndex > 0 || this.coverImageReset, needsReload: false, isDeleted: false} as EditVolumeModalCloseResult);
    });
  }


  async runTask(action: ActionItem<Volume>) {
    switch (action.action) {
      case Action.MarkAsRead:
        this.actionService.markVolumeAsRead(this.seriesId, this.volume, (p) => {
          this.volume.pagesRead = p.pagesRead;
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsUnread:
        this.actionService.markVolumeAsUnread(this.seriesId, this.volume, (p) => {
          this.volume.pagesRead = 0;
          this.cdRef.markForCheck();
        });
        break;
      case Action.Delete:
        await this.actionService.deleteVolume(this.volume.id, (b) => {
          if (!b) return;
          this.modal.close({success: b, volume: this.volume, coverImageUpdate: false, needsReload: true, isDeleted: b} as EditVolumeModalCloseResult);
        });
        break;
      case Action.Download:
        this.downloadService.download('volume', this.volume);
        break;
    }
  }

  updateSelectedIndex(index: number) {
    this.editForm.patchValue({
      coverImageIndex: index
    });
    this.cdRef.markForCheck();
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.editForm.patchValue({
      coverImageLocked: false
    });
    this.cdRef.markForCheck();
  }
}
