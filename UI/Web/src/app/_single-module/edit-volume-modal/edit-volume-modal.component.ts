import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal, NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgClass} from "@angular/common";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {EntityTitleComponent} from "../../cards/entity-title/entity-title.component";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {CoverImageChooserComponent} from "../../cards/cover-image-chooser/cover-image-chooser.component";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {Volume} from "../../_models/volume";
import {UtilityService} from "../../shared/_services/utility.service";
import {ImageService} from "../../_services/image.service";
import {UploadService} from "../../_services/upload.service";
import {AccountService} from "../../_services/account.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from "../../shared/_services/download.service";
import {LibraryType} from "../../_models/library/library";
import {PersonRole} from "../../_models/metadata/person";
import {forkJoin} from "rxjs";
import {MangaFormat} from 'src/app/_models/manga-format';
import {MangaFile} from "../../_models/manga-file";
import {BreakpointService} from "../../_services/breakpoint.service";
import {ActionFactoryService} from "../../_services/action-factory.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {Action} from "../../_models/actionables/action";
import {modalDeleted, modalSaved} from "../../_models/modal/modal-result";

enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
  Info = 'info-tab',
  Tasks = 'tasks-tab',
  Progress = 'progress-tab',
}


@Component({
  selector: 'app-edit-volume-modal',
  imports: [
    FormsModule,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    TranslocoDirective,
    NgbNavOutlet,
    ReactiveFormsModule,
    NgbNavItem,
    SettingItemComponent,
    NgClass,
    EntityTitleComponent,
    SettingButtonComponent,
    CoverImageChooserComponent,
    CompactNumberPipe,
    DefaultDatePipe,
    UtcToLocalTimePipe,
    BytesPipe,
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
  protected readonly breakpointService = inject(BreakpointService);

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
  
  tasks = this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getVolumeActions(this.seriesId, this.libraryId, this.libraryType), this.blacklist);
  /**
   * A copy of the chapter from init. This is used to compare values for name fields to see if lock was modified
   */
  initVolume!: Volume;
  imageUrls: Array<string> = [];
  size: number = 0;
  files: Array<MangaFile> = [];

  constructor() {
    if (!this.accountService.hasAdminRole()) {
      this.activeId = TabID.Info;
      this.cdRef.markForCheck();
    }
  }

  get blacklist() {
    return [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];
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
      const needsCoverUpdate = selectedIndex > 0 || this.coverImageReset;
      this.modal.close(modalSaved(this.volume, needsCoverUpdate));
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
          this.modal.close(modalDeleted(this.volume));
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
