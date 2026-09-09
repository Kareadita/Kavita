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
import {TitleCasePipe} from "@angular/common";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {Chapter} from "../../_models/chapter";
import {LibraryType} from "../../_models/library/library";
import {TypeaheadConfig} from "../../typeahead/_models/typeahead-config";
import {Tag} from "../../_models/tag";
import {Language} from "../../_models/metadata/language";
import {allPeopleRoles, Person, PersonRole} from "../../_models/metadata/person";
import {Genre} from "../../_models/metadata/genre";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {ImageService} from "../../_services/image.service";
import {UploadService} from "../../_services/upload.service";
import {MetadataService} from "../../_services/metadata.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from '../../shared/_services/download.service';
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {map, of, switchMap} from "rxjs";
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
import {personFields, PersonFields, personFieldsFrom} from "../../_helpers/person-fields";
import {LockableFieldComponent} from "../../shared/_components/lockable-field/lockable-field.component";
import {SettingEnumSelectComponent} from "../../settings/_components/setting-enum-select/setting-enum-select.component";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";


interface FormModel extends IHasMetadataIds, PersonFields {
  titleName: string;
  sortOrder: number;
  summary: string;
  language: string;
  isbn: string;
  ageRating: AgeRating;
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



@Component({
  selector: 'app-edit-chapter-modal',
  imports: [
    TranslocoDirective,
    SettingItemComponent,
    TypeaheadComponent,
    EntityTitleComponent,
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
    LockableFieldComponent,
    SettingEnumSelectComponent,
    AgeRatingPipe,
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
    ageRating: AgeRating.Unknown,
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
    titleName: '',
    ...personFieldsFrom({})
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
  protected readonly personLocks = standaloneLocks(() => this.chapter(),
    Object.values(personFields).map(f => f.lock));

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
      ageRating: this.chapter().ageRating,
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
      ...personFieldsFrom(this.chapter()),
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
      releaseDate: model.releaseDate === '' ? NULL_DATE : model.releaseDate + 'T00:00:00',
    };

    writeFieldLocks(payload, this.locks);
    writeNamedLocks(payload, this.personLocks);

    this.chapterService.updateChapter(payload).pipe(
      switchMap(vol => this.coverImageDirty
        ? this.uploadService.updateChapterCoverImage(this.chapter().id, this.selectedCover, true).pipe(map(() => vol))
        : of(vol))
    ).subscribe((c) => {
      this.chapter.set(c);
      const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
      this.modal.close(modalSaved(this.chapter(), needsCoverUpdate));
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
    this.metadataService.getAllPeople().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(people => {
      const settings: Partial<Record<PersonRole, TypeaheadConfig<Person>>> = {};

      for (const role of allPeopleRoles) {
        const field = personFields[role];
        const personSettings = this.typeaheadSettingsFactory.forPerson({id: field.id, role});
        const preset = this.formGroup[field.model]().value();

        if (preset.length > 0) {
          const presetIds = preset.map(p => p.id);
          personSettings.savedData = people.filter(person => presetIds.includes(person.id));
          this.formGroup[field.model]().value.set(personSettings.savedData);
        }

        settings[role] = personSettings;
      }

      this.peopleSettings.set(settings);
    });
  }

  updateTags(tags: Tag[]) {
    this.formGroup.tags().value.set(tags);
  }

  updateGenres(genres: Genre[]) {
    this.formGroup.genres().value.set(genres);
  }

  updatePerson(persons: Person[], role: PersonRole) {
    const field = personFields[role];
    this.formGroup[field.model]().value.set(persons);
    this.personLocks[field.lock].set(true);
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
