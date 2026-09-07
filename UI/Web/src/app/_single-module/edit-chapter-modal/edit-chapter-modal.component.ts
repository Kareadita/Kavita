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
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgClass, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {Chapter} from "../../_models/chapter";
import {LibraryType} from "../../_models/library/library";
import {TypeaheadConfig} from "../../typeahead/_models/typeahead-config";
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
  applyExternalMetadataIdRules,
  EditExternalMetadataFormComponent
} from "../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";
import {NULL_DATE} from "../../_pipes/date-year-range.pipe";
import {DownloadEntityType} from "../../shared/_models/download-queue-item";
import {EditModalShellComponent} from "../../shared/edit-modal-shell/edit-modal-shell.component";
import {EditTabDirective} from "../../shared/_directive/edit-tab.directive";
import {TypeaheadConfigFactoryService} from "../../typeahead-config-factory.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {form, FormField, min, required} from "@angular/forms/signals";
import {IHasMetadataIds} from "../../_models/common/i-has-metadata-ids";
import {lockGroup, standaloneLocks, writeFieldLocks, writeNamedLocks} from "../../_helpers/field-lock";

type PersonLockKey = 'writerLocked' | 'characterLocked' | 'publisherLocked' | 'coverArtistLocked'
  | 'pencillerLocked' | 'inkerLocked' | 'imprintLocked' | 'coloristLocked' | 'lettererLocked'
  | 'editorLocked' | 'translatorLocked' | 'teamLocked' | 'locationLocked';

interface FormModel extends IHasMetadataIds {
  titleName: string;
  sortOrder: number;
  summary: string;
  language: string;
  isbn: string;
  ageRating: string;
  releaseDate: string;
  genres: Genre[];
  tags: Tag[];
  coverImage: string;

  aniListId: number;
  malId: number;
  hardcoverId: number;
  metronId: number;
  comicVineId: string | null;
  mangaBakaId: number;
  cbrId: number;
}

const blackList = [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];

const personLockByRole: Record<PersonRole, PersonLockKey> = {
  [PersonRole.Writer]: 'writerLocked',
  [PersonRole.Penciller]: 'pencillerLocked',
  [PersonRole.Inker]: 'inkerLocked',
  [PersonRole.Colorist]: 'coloristLocked',
  [PersonRole.Letterer]: 'lettererLocked',
  [PersonRole.CoverArtist]: 'coverArtistLocked',
  [PersonRole.Editor]: 'editorLocked',
  [PersonRole.Publisher]: 'publisherLocked',
  [PersonRole.Character]: 'characterLocked',
  [PersonRole.Translator]: 'translatorLocked',
  [PersonRole.Imprint]: 'imprintLocked',
  [PersonRole.Team]: 'teamLocked',
  [PersonRole.Location]: 'locationLocked',
};

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
    FormField,

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
  private readonly typeaheadSettingsFactory = inject(TypeaheadConfigFactoryService);

  @Input({required: true}) chapter!: Chapter;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) seriesId!: number;

  activeId = Tabs.General;
  selectedCover: string = '';
  coverImageReset = false;
  coverImageDirty = false;
  chooserConfig = signal<CoverImageChooserConfig>({});

  private readonly formModel = signal<FormModel>({
    ageRating: AgeRating.Unknown.toString(),
    aniListId: 0,
    cbrId: 0,
    comicVineId: null,
    coverImage: '',
    genres: [],
    hardcoverId: 0,
    isbn: '',
    language: '',
    malId: 0,
    mangaBakaId: 0,
    metronId: 0,
    releaseDate: '',
    sortOrder: 0,
    summary: '',
    tags: [],
    titleName: ''
  });
  formGroup = form(this.formModel, p => {
    required(p.sortOrder);
    min(p.sortOrder, 0);
    applyExternalMetadataIdRules(p);
  });

  tagsSettings = signal<TypeaheadConfig<Tag> | null>(null);
  languageSettings = signal<TypeaheadConfig<Language> | null>(null);
  peopleSettings = signal<Partial<Record<PersonRole, TypeaheadConfig<Person>>>>({});
  genreSettings = signal<TypeaheadConfig<Genre> | null>(null);

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];

  tasks: Array<ActionItem<Chapter>> = [];
  size: number = 0;

  protected readonly locks = lockGroup(this.formGroup, () => this.chapter, [
    'titleName', 'sortOrder', 'isbn', 'ageRating', 'summary',
    'releaseDate', 'genres', 'tags', 'language', 'coverImage',
  ]);
  protected readonly personLocks = standaloneLocks(() => this.chapter, [
    'writerLocked', 'characterLocked', 'publisherLocked', 'coverArtistLocked',
    'pencillerLocked', 'inkerLocked', 'imprintLocked', 'coloristLocked',
    'lettererLocked', 'editorLocked', 'translatorLocked', 'teamLocked', 'locationLocked',
  ]);

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
    this.tasks = this.actionFactoryService.getActionablesForSettingsPage(
      this.actionFactoryService.getChapterActions(this.seriesId, this.libraryId, this.libraryType), blackList);

    this.size = (<Chapter>this.chapter).files.reduce((sum, v) => sum + v.bytes, 0);

    this.chooserConfig.set(this.coverChooserConfigFactory.forChapter(this.chapter, this.libraryType, this.seriesId));

    this.languageSettings.set(this.typeaheadSettingsFactory.forLanguage({id: 'language', currentSelectedLanguage: this.chapter.language}));

    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });

    this.formModel.set({
      titleName: this.chapter.titleName,
      sortOrder: Math.max(0, this.chapter.sortOrder),
      summary: this.chapter.summary || '',
      language: this.chapter.language,
      isbn: this.chapter.isbn,
      ageRating: this.chapter.ageRating.toString(),
      releaseDate: this.chapter.releaseDate !== NULL_DATE ? this.chapter.releaseDate.substring(0, 10) : '',
      genres: this.chapter.genres ?? [],
      tags: this.chapter.tags ?? [],
      aniListId: this.chapter.aniListId,
      malId: this.chapter.malId,
      hardcoverId: this.chapter.hardcoverId,
      metronId: this.chapter.metronId,
      comicVineId: this.chapter.comicVineId,
      mangaBakaId: this.chapter.mangaBakaId,
      cbrId: this.chapter.cbrId,
      coverImage: this.chapter.coverImage, // TODO: Validate this
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
    const model = this.formModel();

    const payload: Chapter = {
      ...this.chapter,
      ...model,
      ageRating: parseInt(model.ageRating + '', 10) as AgeRating,
      releaseDate: model.releaseDate === '' ? NULL_DATE : model.releaseDate + 'T00:00:00',
    };

    writeFieldLocks(payload, this.locks);
    writeNamedLocks(payload, this.personLocks);

    const apis = [
      this.chapterService.updateChapter(payload)
    ];

    const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateChapterCoverImage(this.chapter.id, this.selectedCover, true));
    }

    concat(...apis).subscribe(() => {
      this.modal.close(modalSaved(payload, needsCoverUpdate));
    });
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
        this.actionService.markChapterAsUnread(this.libraryId, this.seriesId, this.chapter, () => {
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
      const settings: Partial<Record<PersonRole, TypeaheadConfig<Person>>> = {};

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
    this.locks.tags.set(true);
    this.cdRef.markForCheck();
  }

  updateGenres(genres: Genre[]) {
    this.genres = genres;
    this.chapter.genres = genres;
    this.locks.genres.set(true);
    this.cdRef.markForCheck();
  }

  updatePerson(persons: Person[], role: PersonRole) {
    this.metadataService.updatePerson(this.chapter, persons, role);
    this.personLocks[personLockByRole[role]].set(true);
    this.cdRef.markForCheck();
  }

  updateLanguage(language: Array<Language>) {
    if (language.length === 0) {
      this.chapter.language = '';
      return;
    }
    this.chapter.language = language[0].isoCode;
    this.locks.language.set(true);
    this.cdRef.markForCheck();
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.locks.coverImage.set(false);
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
  protected readonly form = form;
}
