import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal, NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgClass} from "@angular/common";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {EntityTitleComponent} from "../../cards/entity-title/entity-title.component";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {CoverImageChooserComponent} from "../../cards/cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../_services/cover-chooser-config-factory.service";
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
import {DownloadService} from '../../shared/_services/download.service';
import {DownloadEntityType} from '../../shared/_models/download-queue-item';
import {LibraryType} from "../../_models/library/library";
import {PersonRole} from "../../_models/metadata/person";
import {concat} from "rxjs";
import {MangaFormat} from 'src/app/_models/manga-format';
import {MangaFile} from "../../_models/manga-file";
import {BreakpointService} from "../../_services/breakpoint.service";
import {ActionFactoryService} from "../../_services/action-factory.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {Action} from "../../_models/actionables/action";
import {modalDeleted, modalSaved} from "../../_models/modal/modal-result";
import {VolumeService} from "../../_services/volume.service";
import {UpdateVolume} from "../../_models/update-volume";
import {Tabs} from "../../_models/tabs";
import {TabTitlePipe} from "../../_pipes/tab-title.pipe";
import {
  EditExternalMetadataFormComponent
} from "../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";


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
    ReadTimePipe,
    TabTitlePipe,
    EditExternalMetadataFormComponent
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
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  @Input({required: true}) volume!: Volume;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) seriesId!: number;

  activeId = Tabs.Info;
  editForm: FormGroup = new FormGroup({});
  selectedCover: string = '';
  coverImageReset = false;
  coverImageDirty = false;
  chooserConfig: CoverImageChooserConfig = {};

  tasks = this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getVolumeActions(this.seriesId, this.libraryId, this.libraryType), this.blacklist);
  /**
   * A copy of the chapter from init. This is used to compare values for name fields to see if lock was modified
   */
  initVolume!: Volume;
  size: number = 0;
  files: Array<MangaFile> = [];

  constructor() {
    if (!this.accountService.hasAdminRole()) {
      this.activeId = Tabs.Info;
      this.cdRef.markForCheck();
    }
  }

  get blacklist() {
    return [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];
  }


  ngOnInit() {
    this.initVolume = Object.assign({}, this.volume);

    this.files = this.volume.chapters.flatMap(c => c.files);
    this.size = this.files.reduce((sum, v) => sum + v.bytes, 0);

    this.editForm.addControl('coverImageLocked', new FormControl(this.volume.coverImageLocked, []));

    this.chooserConfig = this.coverChooserConfigFactory.forVolume(this.volume, this.libraryType);
  }

  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.volume, true));
    } else {
      this.modal.dismiss();
    }
  }

  save() {
    const model = this.editForm.getRawValue();

    const updateData = {id: this.volume.id, ...model} as UpdateVolume;

    const apis = [
      this.volumeService.updateVolume(updateData)
    ];

    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateVolumeCoverImage(this.volume.id, this.selectedCover, true));
    }

    concat(...apis).subscribe(() => {
      const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
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
        this.downloadService.download(DownloadEntityType.Volume, this.volume, this.libraryId, this.seriesId);
        break;
    }
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.editForm.patchValue({ coverImageLocked: false });
    this.chooserConfig = { ...this.chooserConfig, isLocked: false };
    this.cdRef.markForCheck();
  }

  protected readonly Tabs = Tabs;
  protected readonly Action = Action;
  protected readonly PersonRole = PersonRole;
  protected readonly MangaFormat = MangaFormat;
}
