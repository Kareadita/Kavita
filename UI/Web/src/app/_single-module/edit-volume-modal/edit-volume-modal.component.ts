import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
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
import {EntityInfoCardsComponent} from "../../cards/entity-info-cards/entity-info-cards.component";
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
import {SeriesService} from "../../_services/series.service";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {ImageService} from "../../_services/image.service";
import {UploadService} from "../../_services/upload.service";
import {MetadataService} from "../../_services/metadata.service";
import {AccountService} from "../../_services/account.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from "../../shared/_services/download.service";
import {Chapter} from "../../_models/chapter";
import {LibraryType} from "../../_models/library/library";
import {TypeaheadSettings} from "../../typeahead/_models/typeahead-settings";
import {Tag} from "../../_models/tag";
import {Language} from "../../_models/metadata/language";
import {Person, PersonRole} from "../../_models/metadata/person";
import {Genre} from "../../_models/metadata/genre";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {forkJoin, Observable, of} from "rxjs";
import {map} from "rxjs/operators";
import {EditChapterModalCloseResult} from "../edit-chapter-modal/edit-chapter-modal.component";
import { MangaFormat } from 'src/app/_models/manga-format';
import {MangaFile} from "../../_models/manga-file";
import {VolumeService} from "../../_services/volume.service";

enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
  Info = 'info-tab',
  People = 'people-tab',
  Tasks = 'tasks-tab',
  Progress = 'progress-tab',
  Tags = 'tags-tab'
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
    EntityInfoCardsComponent,
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
  private readonly seriesService = inject(SeriesService);
  public readonly utilityService = inject(UtilityService);
  public readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
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

  activeId = TabID.General;
  editForm: FormGroup = new FormGroup({});
  selectedCover: string = '';
  coverImageReset = false;

  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<Language> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  genreSettings: TypeaheadSettings<Genre> = new TypeaheadSettings();

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];
  validLanguages: Array<Language> = [];

  tasks = this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getChapterActions(this.runTask.bind(this)), blackList);
  /**
   * A copy of the chapter from init. This is used to compare values for name fields to see if lock was modified
   */
  initVolume!: Volume;
  imageUrls: Array<string> = [];
  size: number = 0;
  files: Array<MangaFile> = [];



  ngOnInit() {
    this.initVolume = Object.assign({}, this.volume);
    this.imageUrls.push(this.imageService.getChapterCoverImage(this.volume.id));

    this.files = this.volume.chapters.flatMap(c => c.files);


    this.editForm.addControl('coverImageIndex', new FormControl(0, []));
    this.editForm.addControl('coverImageLocked', new FormControl(this.volume.coverImageLocked, []));
  }

  close() {
    this.modal.dismiss();
  }

  save() {
    const model = this.editForm.value;
    const selectedIndex = this.editForm.get('coverImageIndex')?.value || 0;

    //this.volume.releaseDate = model.releaseDate;


    const apis = [

    ];


    if (selectedIndex > 0 && this.selectedCover !== '') {
      apis.push(this.uploadService.updateVolumeCoverImage(model.id, this.selectedCover, !this.coverImageReset));
    }

    forkJoin(apis).subscribe(results => {
      this.modal.close({success: true, volume: model, coverImageUpdate: selectedIndex > 0 || this.coverImageReset, needsReload: false, isDeleted: false} as EditVolumeModalCloseResult);
    });
  }


  async runTask(action: ActionItem<Chapter>) {
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