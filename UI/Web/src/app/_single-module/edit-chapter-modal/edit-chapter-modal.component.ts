import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  effect,
  inject,
  Input,
  OnInit,
  signal
} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgClass, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {Chapter} from "../../_models/chapter";
import {LibraryType} from "../../_models/library/library";
import {TypeaheadSettings} from "../../typeahead/_models/typeahead-settings";
import {Tag} from "../../_models/tag";
import {Language} from "../../_models/metadata/language";
import {Person, PersonRole} from "../../_models/metadata/person";
import {Genre} from "../../_models/metadata/genre";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {ImageService} from "../../_services/image.service";
import {UploadService} from "../../_services/upload.service";
import {MetadataService} from "../../_services/metadata.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from '../../shared/_services/download.service';
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {concat} from "rxjs";
import {EntityTitleComponent} from "../../cards/entity-title/entity-title.component";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {CoverImageChooserComponent} from "../../cards/cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../_services/cover-chooser-config-factory.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {MangaFormat} from "../../_models/manga-format";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {ImageComponent} from "../../shared/image/image.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {ChapterService} from "../../_services/chapter.service";
import {AgeRating} from "../../_models/metadata/age-rating";
import {BreakpointService} from "../../_services/breakpoint.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {Action} from "../../_models/actionables/action";
import {ActionFactoryService} from "../../_services/action-factory.service";
import {modalDeleted, modalSaved} from "../../_models/modal/modal-result";
import {Tabs} from "../../_models/tabs";
import {
  addMetadataIdControls,
  EditExternalMetadataFormComponent
} from "../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";
import {NULL_DATE} from "../../_pipes/date-year-range.pipe";
import {DownloadEntityType} from "../../shared/_models/download-queue-item";
import {EditModalShellComponent} from "../../shared/edit-modal-shell/edit-modal-shell.component";
import {EditTabDirective} from "../../shared/_directive/edit-tab.directive";
import {TypeaheadSettingsFactoryService} from "../../typeahead-settings-factory.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";


const blackList = [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];

@Component({
  selector: 'app-edit-chapter-modal',
  imports: [
    FormsModule,
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    NgTemplateOutlet,
    NgClass,
    TypeaheadComponent,
    EntityTitleComponent,
    TitleCasePipe,
    SettingButtonComponent,
    CoverImageChooserComponent,
    CompactNumberPipe,
    DefaultDatePipe,
    UtcToLocalTimePipe,
    BytesPipe,
    ImageComponent,
    SafeHtmlPipe,
    ReadTimePipe,
    EditExternalMetadataFormComponent,
    EditModalShellComponent,
    EditTabDirective,
    FormFieldDirective,

  ],
  templateUrl: './edit-chapter-modal.component.html',
  styleUrl: './edit-chapter-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditChapterModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  public readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly downloadService = inject(DownloadService);
  private readonly chapterService = inject(ChapterService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadSettingsFactoryService);

  @Input({required: true}) chapter!: Chapter;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) seriesId!: number;

  activeId = Tabs.General;
  editForm: FormGroup = new FormGroup({});
  selectedCover: string = '';
  coverImageReset = false;
  coverImageDirty = false;
  chooserConfig = signal<CoverImageChooserConfig>({});


  tagsSettings = signal<TypeaheadSettings<Tag> | null>(null);
  languageSettings = signal<TypeaheadSettings<Language> | null>(null);
  peopleSettings = signal<Partial<Record<PersonRole, TypeaheadSettings<Person>>>>({});
  genreSettings = signal<TypeaheadSettings<Genre> | null>(null);

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];

  tasks = this.actionFactoryService.getActionablesForSettingsPage(
    this.actionFactoryService.getChapterActions(this.seriesId, this.libraryId, this.libraryType), blackList);
  /**
   * A copy of the chapter from init. This is used to compare values for name fields to see if lock was modified
   */
  initChapter!: Chapter;
  size: number = 0;

  get WebLinks() {
    if (this.chapter.webLinks === '') return [];
    return this.chapter.webLinks.split(',');
  }

  constructor() {
    effect(() => {
      if (!this.accountService.hasAdminRole()) {
        this.activeId = Tabs.Info;
        this.cdRef.markForCheck();
      }
    });
  }


  ngOnInit() {
    this.initChapter = Object.assign({}, this.chapter);

    this.size = (<Chapter>this.chapter).files.reduce((sum, v) => sum + v.bytes, 0);

    this.chooserConfig.set(this.coverChooserConfigFactory.forChapter(this.chapter, this.libraryType, this.seriesId));

    this.editForm.addControl('titleName', new FormControl(this.chapter.titleName, []));
    this.editForm.addControl('sortOrder', new FormControl(Math.max(0, this.chapter.sortOrder), {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }));
    this.editForm.addControl('summary', new FormControl(this.chapter.summary || '', []));
    this.editForm.addControl('language', new FormControl(this.chapter.language, []));
    this.editForm.addControl('isbn', new FormControl(this.chapter.isbn, []));
    this.editForm.addControl('ageRating', new FormControl(this.chapter.ageRating, []));
    addMetadataIdControls(this.editForm, this.chapter);

    if (this.chapter.releaseDate !== NULL_DATE) {
      this.editForm.addControl('releaseDate', new FormControl(this.chapter.releaseDate.substring(0, 10), []));
    } else {
      this.editForm.addControl('releaseDate', new FormControl('', []));
    }


    this.editForm.addControl('genres', new FormControl(this.chapter.genres, []));
    this.editForm.addControl('tags', new FormControl(this.chapter.tags, []));


    this.editForm.addControl('coverImageLocked', new FormControl(this.chapter.coverImageLocked, []));

    this.languageSettings.set(this.typeaheadSettingsFactory.forLanguage({id: 'language', currentSelectedLanguage: this.chapter.language}));

    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });

    this.editForm.get('titleName')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.chapter.titleNameLocked = true;
      this.cdRef.markForCheck();
    });

    this.editForm.get('sortOrder')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.chapter.sortOrderLocked = true;
      this.cdRef.markForCheck();
    });

    this.editForm.get('isbn')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.chapter.isbnLocked = true;
      this.cdRef.markForCheck();
    });

    this.editForm.get('ageRating')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.chapter.ageRatingLocked = true;
      this.cdRef.markForCheck();
    });

    this.editForm.get('summary')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.chapter.summaryLocked = true;
      this.cdRef.markForCheck();
    });

    this.editForm.get('releaseDate')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.chapter.releaseDateLocked = true;
      this.cdRef.markForCheck();
    });

    this.setupTypeaheads();

  }


  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.chapter, true));
    } else {
      this.modal.dismiss();
    }
  }

  save() {
    const model = this.editForm.getRawValue();

    // Patch in data from the model that is not typeahead (as those are updated during setting)
    if (model.releaseDate === '') {
      this.chapter.releaseDate = NULL_DATE;
    } else {
      this.chapter.releaseDate = model.releaseDate + 'T00:00:00';
    }

    this.chapter.ageRating = parseInt(model.ageRating + '', 10) as AgeRating;
    this.chapter.sortOrder = model.sortOrder;
    this.chapter.titleName = model.titleName;
    this.chapter.summary = model.summary;
    this.chapter.isbn = model.isbn;
    this.chapter.aniListId = model.aniListId;
    this.chapter.comicVineId = model.comicVineId;
    this.chapter.malId = model.malId;
    this.chapter.hardcoverId = model.hardcoverId;
    this.chapter.metronId = model.metronId;
    this.chapter.language = model.language;


    const apis = [
      this.chapterService.updateChapter(this.chapter)
    ];

    const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateChapterCoverImage(this.chapter.id, this.selectedCover, true));
    }

    concat(...apis).subscribe(results => {
      this.modal.close(modalSaved(model, needsCoverUpdate));
    });
  }

  unlock(b: any, field: string) {
    if (b) {
      b[field] = !b[field];
    }
    this.cdRef.markForCheck();
  }

  async runTask(action: ActionItem<Chapter>) {
    // TODO: Bug: Not properly implemented
    switch (action.action) {

      case Action.MarkAsRead:
        this.actionService.markChapterAsRead(this.libraryId, this.seriesId, this.chapter, (p) => {
          this.chapter.pagesRead = p.pagesRead;
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsUnread:
        this.actionService.markChapterAsUnread(this.libraryId, this.seriesId, this.chapter, (p) => {
          this.chapter.pagesRead = 0;
          this.cdRef.markForCheck();
        });
        break;
      case Action.Delete:
        await this.actionService.deleteChapter(this.chapter.id, (b) => {
          if (!b) return;
          this.modal.close(modalDeleted(this.chapter));
        });
        break;
      case Action.Download:
        this.downloadService.download(DownloadEntityType.Chapter, this.chapter, this.libraryId, this.seriesId);
        break;
    }
  }

  setupTypeaheads() {
    this.tagsSettings.set(this.typeaheadSettingsFactory.forTag({id: 'tags', savedData: this.chapter.tags ?? []}));
    this.genreSettings.set(this.typeaheadSettingsFactory.forGenre({id: 'genres', savedData: this.chapter.genres ?? []}));

    this.setupPersonTypeahead();
  }


  setupPersonTypeahead() {
    const roles: ReadonlyArray<[string, PersonRole, Array<Person> | undefined]> = [
      ['writer', PersonRole.Writer, this.chapter.writers],
      ['character', PersonRole.Character, this.chapter.characters],
      ['colorist', PersonRole.Colorist, this.chapter.colorists],
      ['cover-artist', PersonRole.CoverArtist, this.chapter.coverArtists],
      ['editor', PersonRole.Editor, this.chapter.editors],
      ['inker', PersonRole.Inker, this.chapter.inkers],
      ['letterer', PersonRole.Letterer, this.chapter.letterers],
      ['penciller', PersonRole.Penciller, this.chapter.pencillers],
      ['publisher', PersonRole.Publisher, this.chapter.publishers],
      ['imprint', PersonRole.Imprint, this.chapter.imprints],
      ['translator', PersonRole.Translator, this.chapter.translators],
      ['teams', PersonRole.Team, this.chapter.teams],
      ['locations', PersonRole.Location, this.chapter.locations],
    ];

    this.metadataService.getAllPeople().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(people => {
      const settings: Partial<Record<PersonRole, TypeaheadSettings<Person>>> = {};

      for (const [id, role, preset] of roles) {
        const personSettings = this.typeaheadSettingsFactory.forPerson({id, role});

        if (preset && preset.length > 0) {
          const presetIds = preset.map(p => p.id);
          personSettings.savedData = people.filter(person => presetIds.includes(person.id));
          this.metadataService.updatePerson(this.chapter, personSettings.savedData, role);
        }

        settings[role] = personSettings;
      }

      this.peopleSettings.set(settings);
    });
  }

  updateTags(tags: Tag[]) {
    this.tags = tags;
    this.chapter.tags = tags;
    this.cdRef.markForCheck();
  }

  updateGenres(genres: Genre[]) {
    this.genres = genres;
    this.chapter.genres = genres;
    this.cdRef.markForCheck();
  }

  updatePerson(persons: Person[], role: PersonRole) {
    this.metadataService.updatePerson(this.chapter, persons, role);
    this.chapter.locationLocked = true;
    this.cdRef.markForCheck();
  }

  updateLanguage(language: Array<Language>) {
    if (language.length === 0) {
      this.chapter.language = '';
      return;
    }
    this.chapter.language = language[0].isoCode;
    this.chapter.languageLocked = true;
    this.cdRef.markForCheck();
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.editForm.patchValue({ coverImageLocked: false });
    this.chooserConfig.set({ ...this.chooserConfig(), isLocked: false });
  }

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings()[role];
  }

  changeTab(tab?: Tabs) {
    if (tab) {
      this.activeId = tab;
      this.cdRef.markForCheck();
    }
  }

  protected readonly Tabs = Tabs;
  protected readonly Action = Action;
  protected readonly PersonRole = PersonRole;
  protected readonly MangaFormat = MangaFormat;
}
