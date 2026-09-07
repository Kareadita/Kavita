import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  model,
  OnInit,
  signal
} from '@angular/core';
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
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {ImageComponent} from "../../shared/image/image.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {ChapterService} from "../../_services/chapter.service";
import {AgeRating} from "../../_models/metadata/age-rating";
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
    TranslocoDirective,
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
  protected readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  protected readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly downloadService = inject(DownloadService);
  private readonly chapterService = inject(ChapterService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadConfigFactoryService);

  chapter = model.required<Chapter>();
  libraryType = input.required<LibraryType>();
  libraryId = input.required<number>();
  seriesId = input.required<number>();

  protected readonly activeId = signal(Tabs.General);
  private selectedCover = '';
  private coverImageReset = false;
  private coverImageDirty = false;

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
  protected readonly formGroup = form(this.formModel, p => {
    required(p.sortOrder);
    min(p.sortOrder, 0);
    applyExternalMetadataIdRules(p);
  });

  protected readonly peopleSettings = signal<Partial<Record<PersonRole, TypeaheadConfig<Person>>>>({});
  protected readonly ageRatings = toSignal(this.metadataService.getAllAgeRatings(), {initialValue: [] as Array<AgeRatingDto>});

  protected readonly locks = lockGroup(this.formGroup, () => this.chapter(), [
    'titleName', 'sortOrder', 'isbn', 'ageRating', 'summary',
    'releaseDate', 'genres', 'tags', 'language', 'coverImage',
  ]);
  protected readonly personLocks = standaloneLocks(() => this.chapter(), [
    'writerLocked', 'characterLocked', 'publisherLocked', 'coverArtistLocked',
    'pencillerLocked', 'inkerLocked', 'imprintLocked', 'coloristLocked',
    'lettererLocked', 'editorLocked', 'translatorLocked', 'teamLocked', 'locationLocked',
  ]);

  protected readonly tagsSettings = computed(() =>
    this.typeaheadSettingsFactory.forTag({id: 'tags', savedData: this.chapter().tags ?? []}));
  protected readonly genreSettings = computed(() =>
    this.typeaheadSettingsFactory.forGenre({id: 'genres', savedData: this.chapter().genres ?? []}));
  protected readonly languageSettings = computed(() =>
    this.typeaheadSettingsFactory.forLanguage({id: 'language', currentSelectedLanguage: this.chapter().language}));

  protected readonly chooserConfig = computed<CoverImageChooserConfig>(() => ({
    ...this.coverChooserConfigFactory.forChapter(this.chapter(), this.libraryType(), this.seriesId()),
    isLocked: this.locks.coverImage()
  }));

  protected readonly tasks = computed(() => this.actionFactoryService.getActionablesForSettingsPage(
    this.actionFactoryService.getChapterActions(this.seriesId(), this.libraryId(), this.libraryType()), blackList));

  protected readonly size = computed(() => this.chapter().files.reduce((sum, v) => sum + v.bytes, 0));

  protected readonly weblinks = computed(() => this.chapter().webLinks.split(',').filter(l => l.length > 0));


  constructor() {
    effect(() => {
      if (!this.accountService.hasAdminRole()) {
        this.activeId.set(Tabs.Info);
      }
    });
  }

  ngOnInit() {
    // Seeded once. A linkedSignal here would wipe in-progress edits whenever chapter() changes
    this.formModel.set({
      titleName: this.chapter().titleName,
      sortOrder: Math.max(0, this.chapter().sortOrder),
      summary: this.chapter().summary || '',
      language: this.chapter().language,
      isbn: this.chapter().isbn,
      ageRating: this.chapter().ageRating.toString(),
      releaseDate: this.chapter().releaseDate !== NULL_DATE ? this.chapter().releaseDate.substring(0, 10) : '',
      genres: this.chapter().genres ?? [],
      tags: this.chapter().tags ?? [],
      aniListId: this.chapter().aniListId,
      malId: this.chapter().malId,
      hardcoverId: this.chapter().hardcoverId,
      metronId: this.chapter().metronId,
      comicVineId: this.chapter().comicVineId,
      mangaBakaId: this.chapter().mangaBakaId,
      cbrId: this.chapter().cbrId,
      coverImage: this.chapter().coverImage,
    });

    this.setupPersonTypeahead();
  }

  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.chapter(), true));
    } else {
      this.modal.dismiss();
    }
  }

  save() {
    const model = this.formModel();

    const payload: Chapter = {
      ...this.chapter(),
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
      apis.push(this.uploadService.updateChapterCoverImage(this.chapter().id, this.selectedCover, true));
    }

    concat(...apis).subscribe(() => {
      this.modal.close(modalSaved(payload, needsCoverUpdate));
    });
  }

  async runTask(action: ActionItem<Chapter>) {
    // TODO: Bug: Not properly implemented
    switch (action.action) {

      case Action.MarkAsRead:
        this.actionService.markChapterAsRead(this.libraryId(), this.seriesId(), this.chapter(), p => {
          this.chapter.update(c => ({...c, pagesRead: p.pagesRead}));
        });
        break;
      case Action.MarkAsUnread:
        this.actionService.markChapterAsUnread(this.libraryId(), this.seriesId(), this.chapter(), () => {
          this.chapter.update(c => ({...c, pagesRead: 0}));
        });
        break;
      case Action.Delete:
        await this.actionService.deleteChapter(this.chapter().id, (b) => {
          if (!b) return;
          this.modal.close(modalDeleted(this.chapter())); // TODO: Validate this
        });
        break;
      case Action.Download:
        this.downloadService.download(DownloadEntityType.Chapter, this.chapter(), this.libraryId(), this.seriesId());
        break;
    }
  }

  setupPersonTypeahead() {
    const roles: ReadonlyArray<[string, PersonRole, Array<Person> | undefined]> = [
      ['writer', PersonRole.Writer, this.chapter().writers],
      ['character', PersonRole.Character, this.chapter().characters],
      ['colorist', PersonRole.Colorist, this.chapter().colorists],
      ['cover-artist', PersonRole.CoverArtist, this.chapter().coverArtists],
      ['editor', PersonRole.Editor, this.chapter().editors],
      ['inker', PersonRole.Inker, this.chapter().inkers],
      ['letterer', PersonRole.Letterer, this.chapter().letterers],
      ['penciller', PersonRole.Penciller, this.chapter().pencillers],
      ['publisher', PersonRole.Publisher, this.chapter().publishers],
      ['imprint', PersonRole.Imprint, this.chapter().imprints],
      ['translator', PersonRole.Translator, this.chapter().translators],
      ['teams', PersonRole.Team, this.chapter().teams],
      ['locations', PersonRole.Location, this.chapter().locations],
    ];

    this.metadataService.getAllPeople().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(people => {
      const settings: Partial<Record<PersonRole, TypeaheadConfig<Person>>> = {};

      for (const [id, role, preset] of roles) {
        const personSettings = this.typeaheadSettingsFactory.forPerson({id, role});

        if (preset && preset.length > 0) {
          const presetIds = preset.map(p => p.id);
          personSettings.savedData = people.filter(person => presetIds.includes(person.id));
          this.metadataService.updatePerson(this.chapter(), personSettings.savedData, role);
        }

        settings[role] = personSettings;
      }

      this.peopleSettings.set(settings);
    });
  }

  updateTags(tags: Tag[]) {
    this.formGroup.tags().value.set(tags);
    this.locks.tags.set(true);
  }

  updateGenres(genres: Genre[]) {
    this.formGroup.genres().value.set(genres);
    this.locks.genres.set(true);
  }

  updatePerson(persons: Person[], role: PersonRole) {
    this.metadataService.updatePerson(this.chapter(), persons, role);
    this.personLocks[personLockByRole[role]].set(true);
  }

  updateLanguage(language: Array<Language>) {
    this.formGroup.language().value.set(language.length === 0 ? '' : language[0].isoCode);
    if (language.length === 0) return;

    this.locks.language.set(true);
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
  }

  handleReset() {
    this.coverImageReset = true;
    this.locks.coverImage.set(false);
  }

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings()[role];
  }

  changeTab(tab?: Tabs) {
    if (tab) {
      this.activeId.set(tab);
    }
  }

  protected readonly Tabs = Tabs;
  protected readonly Action = Action;
  protected readonly PersonRole = PersonRole;
}
