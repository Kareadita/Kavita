import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  signal,
  untracked
} from '@angular/core';
import {form} from "@angular/forms/signals";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
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
import {map, of, switchMap} from "rxjs";
import {BreakpointService} from "../../_services/breakpoint.service";
import {ActionFactoryService} from "../../_services/action-factory.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {Action} from "../../_models/actionables/action";
import {modalDeleted, modalSaved} from "../../_models/modal/modal-result";
import {VolumeService} from "../../_services/volume.service";
import {UpdateVolumeRequest} from "../../_models/update-volume-request";
import {Tabs} from "../../_models/tabs";
import {
  applyExternalMetadataIdRules,
  EditExternalMetadataFormComponent
} from "../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";
import {EditModalShellComponent} from "../../shared/edit-modal-shell/edit-modal-shell.component";
import {EditTabDirective} from "../../shared/_directive/edit-tab.directive";
import {MangaFormat} from "../../_models/manga-format";
import {lockGroup, writeFieldLocks} from "../../_helpers/field-lock";

interface FormModel {
  coverImageLocked: boolean;

  aniListId: number;
  malId: number;
  hardcoverId: number;
  metronId: number;
  comicVineId: string | null;
  mangaBakaId: number;
  cbrId: number;
}

const blacklist = [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];


@Component({
  selector: 'app-edit-volume-modal',
  imports: [
    TranslocoDirective,
    SettingItemComponent,
    EntityTitleComponent,
    SettingButtonComponent,
    CoverImageChooserComponent,
    CompactNumberPipe,
    DefaultDatePipe,
    UtcToLocalTimePipe,
    BytesPipe,
    ReadTimePipe,
    EditExternalMetadataFormComponent,
    EditModalShellComponent,
    EditTabDirective
  ],
  templateUrl: './edit-volume-modal.component.html',
  styleUrl: './edit-volume-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditVolumeModalComponent {
  public readonly modal = inject(NgbActiveModal);
  public readonly utilityService = inject(UtilityService);
  public readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  public readonly accountService = inject(AccountService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly downloadService = inject(DownloadService);
  private readonly volumeService = inject(VolumeService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  volume = model.required<Volume>();
  libraryType = input.required<LibraryType>();
  libraryId = input.required<number>();
  seriesId = input.required<number>();

  activeId = signal<Tabs>(Tabs.Info);

  private selectedCover: string = '';
  private coverImageReset = false;
  private coverImageDirty = false;

  private readonly formModel = signal<FormModel>({
    coverImageLocked: false,
    aniListId: 0,
    malId: 0,
    hardcoverId: 0,
    metronId: 0,
    comicVineId: null,
    mangaBakaId: 0,
    cbrId: 0
  });
  formGroup = form(this.formModel, p => {
    applyExternalMetadataIdRules(p);
  });
  protected readonly locks = lockGroup(this.formGroup, () => this.volume(), [
    'coverImage',
  ]);
  protected readonly chooserConfig = computed<CoverImageChooserConfig>(() => ({
    ...this.coverChooserConfigFactory.forVolume(this.volume(), this.libraryType()),
    isLocked: this.locks.coverImage()
  }));

  tasks = computed(() => {
    return this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getVolumeActions(this.seriesId(), this.libraryId(), this.libraryType()), blacklist);
  });

  files = computed(() => {
    const vol = this.volume();
    if (!vol) return [];

    return vol.chapters.flatMap(c => c.files);
  });
  size = computed(() => {
    return this.files().reduce((sum, v) => sum + v.bytes, 0);
  });

  constructor() {
    if (!this.accountService.hasAdminRole()) {
      this.activeId.set(Tabs.Info);
    }

    effect(() => {
      untracked(() => {
        this.formModel.set({
          coverImageLocked: this.volume().coverImageLocked,
          aniListId: this.volume().aniListId,
          malId: this.volume().malId,
          hardcoverId: this.volume().hardcoverId,
          metronId: this.volume().metronId,
          comicVineId: this.volume().comicVineId,
          mangaBakaId: this.volume().mangaBakaId,
          cbrId: this.volume().cbrId,
        });
      });
      this.locks.coverImage.set(this.volume().coverImageLocked);
    });
  }


  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.volume(), true));
    } else {
      this.modal.dismiss();
    }
  }

  save() {
    const model = this.formModel();

    const updateData = {id: this.volume().id, ...model} as UpdateVolumeRequest;
    writeFieldLocks(updateData, this.locks);

    this.volumeService.updateVolume(updateData).pipe(
      switchMap(vol => this.coverImageDirty
        ? this.uploadService.updateVolumeCoverImage(this.volume().id, this.selectedCover, true).pipe(map(() => vol))
        : of(vol))
    ).subscribe((v) => {
      this.volume.set(v);
      const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
      this.modal.close(modalSaved(this.volume(), needsCoverUpdate));
    });
  }


  async runTask(action: ActionItem<Volume>) {
    switch (action.action) {
      case Action.MarkAsRead:
        this.actionService.markVolumeAsRead(this.seriesId(), this.volume(), (p) => {
          this.volume.update(c => ({...c, pagesRead: p.pagesRead}));
        });
        break;
      case Action.MarkAsUnread:
        this.actionService.markVolumeAsUnread(this.seriesId(), this.volume(), (p) => {
          this.volume.update(c => ({...c, pagesRead: 0}));
        });
        break;
      case Action.Delete:
        await this.actionService.deleteVolume(this.volume().id, (b) => {
          if (!b) return;
          this.modal.close(modalDeleted(this.volume()));
        });
        break;
      case Action.Download:
        this.downloadService.download(DownloadEntityType.Volume, this.volume(), this.libraryId(), this.seriesId());
        break;
    }
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
  }

  handleReset() {
    this.coverImageReset = true;
    this.formModel.update(m => ({...m, coverImageLocked: false}));
    this.locks.coverImage.set(false);
  }

  changeTab(tab?: Tabs) {
    if (!tab) return;
    this.activeId.set(tab);
  }

  protected readonly Tabs = Tabs;
  protected readonly Action = Action;
  protected readonly PersonRole = PersonRole;
  protected readonly MangaFormat = MangaFormat;
}
