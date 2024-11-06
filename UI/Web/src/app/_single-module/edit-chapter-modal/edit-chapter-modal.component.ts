import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {
  AsyncPipe,
  DatePipe,
  DecimalPipe,
  NgClass,
  NgTemplateOutlet,
  TitleCasePipe
} from "@angular/common";
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
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from "../../shared/_services/download.service";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {forkJoin, Observable, of, tap} from "rxjs";
import {map} from "rxjs/operators";
import {EntityTitleComponent} from "../../cards/entity-title/entity-title.component";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {CoverImageChooserComponent} from "../../cards/cover-image-chooser/cover-image-chooser.component";
import {EditChapterProgressComponent} from "../../cards/edit-chapter-progress/edit-chapter-progress.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {IconAndTitleComponent} from "../../shared/icon-and-title/icon-and-title.component";
import {MangaFormat} from "../../_models/manga-format";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {TranslocoDatePipe} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {ImageComponent} from "../../shared/image/image.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {ChapterService} from "../../_services/chapter.service";
import {AgeRating} from "../../_models/metadata/age-rating";
import {User} from "../../_models/user";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";

enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
  Info = 'info-tab',
  People = 'people-tab',
  Tasks = 'tasks-tab',
  Progress = 'progress-tab',
  Tags = 'tags-tab'
}

export interface EditChapterModalCloseResult {
  success: boolean;
  chapter: Chapter;
  coverImageUpdate: boolean;
  needsReload: boolean;
  isDeleted: boolean;
}

const blackList = [Action.Edit, Action.IncognitoRead, Action.AddToReadingList];

@Component({
  selector: 'app-edit-chapter-modal',
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
    ReadTimePipe,
    SettingTitleComponent
  ],
  templateUrl: './edit-chapter-modal.component.html',
  styleUrl: './edit-chapter-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditChapterModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  public readonly utilityService = inject(UtilityService);
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

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;
  protected readonly Action = Action;
  protected readonly PersonRole = PersonRole;
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) chapter!: Chapter;
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
  initChapter!: Chapter;
  imageUrls: Array<string> = [];
  size: number = 0;
  user!: User;

  get WebLinks() {
    if (this.chapter.webLinks === '') return [];
    return this.chapter.webLinks.split(',');
  }



  ngOnInit() {
    this.initChapter = Object.assign({}, this.chapter);
    this.imageUrls.push(this.imageService.getChapterCoverImage(this.chapter.id));

    this.size = this.utilityService.asChapter(this.chapter).files.reduce((sum, v) => sum + v.bytes, 0);
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), tap(u => {
      if (!u) return;
      this.user = u;

      if (!this.accountService.hasAdminRole(this.user)) {
        this.activeId = TabID.Info;
      }
      this.cdRef.markForCheck();

    })).subscribe();

    this.editForm.addControl('titleName', new FormControl(this.chapter.titleName, []));
    this.editForm.addControl('sortOrder', new FormControl(Math.max(0, this.chapter.sortOrder), [Validators.required, Validators.min(0)]));
    this.editForm.addControl('summary', new FormControl(this.chapter.summary || '', []));
    this.editForm.addControl('language', new FormControl(this.chapter.language, []));
    this.editForm.addControl('isbn', new FormControl(this.chapter.isbn, []));
    this.editForm.addControl('ageRating', new FormControl(this.chapter.ageRating, []));
    this.editForm.addControl('releaseDate', new FormControl(this.chapter.releaseDate, []));


    this.editForm.addControl('genres', new FormControl(this.chapter.genres, []));
    this.editForm.addControl('tags', new FormControl(this.chapter.tags, []));


    this.editForm.addControl('coverImageIndex', new FormControl(0, []));
    this.editForm.addControl('coverImageLocked', new FormControl(this.chapter.coverImageLocked, []));

    this.metadataService.getAllValidLanguages().subscribe(validLanguages => {
      this.validLanguages = validLanguages;
      this.setupLanguageTypeahead();
      this.cdRef.markForCheck();
    });

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
    this.modal.dismiss();
  }

  save() {
    const model = this.editForm.value;
    const selectedIndex = this.editForm.get('coverImageIndex')?.value || 0;

    this.chapter.releaseDate = model.releaseDate;
    this.chapter.ageRating = parseInt(model.ageRating + '', 10) as AgeRating;
    this.chapter.genres = model.genres;
    this.chapter.tags = model.tags;
    this.chapter.sortOrder = model.sortOrder;
    this.chapter.language = model.language;
    this.chapter.titleName = model.titleName;
    this.chapter.summary = model.summary;
    this.chapter.isbn = model.isbn;


    const apis = [
      this.chapterService.updateChapter(this.chapter)
    ];

    // We only need to call updateSeries if we changed name, sort name, or localized name or reset a cover image
    const needsReload = this.editForm.get('titleName')?.dirty || this.editForm.get('sortOrder')?.dirty;


    if (selectedIndex > 0 || this.coverImageReset) {
      apis.push(this.uploadService.updateChapterCoverImage(this.chapter.id, this.selectedCover, !this.coverImageReset));
    }

    forkJoin(apis).subscribe(results => {
      this.modal.close({success: true, chapter: model, coverImageUpdate: selectedIndex > 0 || this.coverImageReset, needsReload: needsReload, isDeleted: false} as EditChapterModalCloseResult);
    });
  }

  unlock(b: any, field: string) {
    if (b) {
      b[field] = !b[field];
    }
    this.cdRef.markForCheck();
  }

  async runTask(action: ActionItem<Chapter>) {
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
          this.modal.close({success: b, chapter: this.chapter, coverImageUpdate: false, needsReload: true, isDeleted: b} as EditChapterModalCloseResult);
        });
        break;
      case Action.Download:
        this.downloadService.download('chapter', this.chapter);
        break;
    }
  }

  setupTypeaheads() {
    forkJoin([
      this.setupTagSettings(),
      this.setupGenreTypeahead(),
      this.setupPersonTypeahead(),
      this.setupLanguageTypeahead()
    ]).subscribe(results => {
      this.cdRef.markForCheck();
    });
  }

  setupTagSettings() {
    this.tagsSettings.minCharacters = 0;
    this.tagsSettings.multiple = true;
    this.tagsSettings.id = 'tags';
    this.tagsSettings.unique = true;
    this.tagsSettings.showLocked = true;
    this.tagsSettings.addIfNonExisting = true;


    this.tagsSettings.compareFn = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    this.tagsSettings.fetchFn = (filter: string) => this.metadataService.getAllTags()
      .pipe(map(items => this.tagsSettings.compareFn(items, filter)));

    this.tagsSettings.addTransformFn = ((title: string) => {
      return {id: 0, title: title };
    });
    this.tagsSettings.selectionCompareFn = (a: Tag, b: Tag) => {
      return a.title.toLowerCase() == b.title.toLowerCase();
    }
    this.tagsSettings.compareFnForAdd = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    }

    if (this.chapter.tags) {
      this.tagsSettings.savedData = this.chapter.tags;
    }
    return of(true);
  }

  setupGenreTypeahead() {
    this.genreSettings.minCharacters = 0;
    this.genreSettings.multiple = true;
    this.genreSettings.id = 'genres';
    this.genreSettings.unique = true;
    this.genreSettings.showLocked = true;
    this.genreSettings.addIfNonExisting = true;
    this.genreSettings.fetchFn = (filter: string) => {
      return this.metadataService.getAllGenres()
        .pipe(map(items => this.genreSettings.compareFn(items, filter)));
    };
    this.genreSettings.compareFn = (options: Genre[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    this.genreSettings.compareFnForAdd = (options: Genre[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    }
    this.genreSettings.selectionCompareFn = (a: Genre, b: Genre) => {
      return a.title.toLowerCase() == b.title.toLowerCase();
    }

    this.genreSettings.addTransformFn = ((title: string) => {
      return {id: 0, title: title };
    });

    if (this.chapter.genres) {
      this.genreSettings.savedData = this.chapter.genres;
    }
    return of(true);
  }

  setupLanguageTypeahead() {
    this.languageSettings.minCharacters = 0;
    this.languageSettings.multiple = false;
    this.languageSettings.id = 'language';
    this.languageSettings.unique = true;
    this.languageSettings.showLocked = true;
    this.languageSettings.addIfNonExisting = false;
    this.languageSettings.compareFn = (options: Language[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    this.languageSettings.compareFnForAdd = (options: Language[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    }
    this.languageSettings.fetchFn = (filter: string) => of(this.validLanguages)
      .pipe(map(items => this.languageSettings.compareFn(items, filter)));

    this.languageSettings.selectionCompareFn = (a: Language, b: Language) => {
      return a.isoCode == b.isoCode;
    }

    const l = this.validLanguages.find(l => l.isoCode === this.chapter.language);
    if (l !== undefined) {
      this.languageSettings.savedData = l;
    }
    return of(true);
  }


  updateFromPreset(id: string, presetField: Array<Person> | undefined, role: PersonRole) {
    const personSettings = this.createBlankPersonSettings(id, role)
    if (presetField && presetField.length > 0) {
      const fetch = personSettings.fetchFn as ((filter: string) => Observable<Person[]>);
      return fetch('').pipe(map(people => {
        const presetIds = presetField.map(p => p.id);
        personSettings.savedData = people.filter(person => presetIds.includes(person.id));
        this.peopleSettings[role] = personSettings;
        this.metadataService.updatePerson(this.chapter, personSettings.savedData as Person[], role);
        this.cdRef.markForCheck();
        return true;
      }));
    } else {
      this.peopleSettings[role] = personSettings;
      return of(true);
    }
  }

  setupPersonTypeahead() {
    this.peopleSettings = {};

    return forkJoin([
      this.updateFromPreset('writer', this.chapter.writers, PersonRole.Writer),
      this.updateFromPreset('character', this.chapter.characters, PersonRole.Character),
      this.updateFromPreset('colorist', this.chapter.colorists, PersonRole.Colorist),
      this.updateFromPreset('cover-artist', this.chapter.coverArtists, PersonRole.CoverArtist),
      this.updateFromPreset('editor', this.chapter.editors, PersonRole.Editor),
      this.updateFromPreset('inker', this.chapter.inkers, PersonRole.Inker),
      this.updateFromPreset('letterer', this.chapter.letterers, PersonRole.Letterer),
      this.updateFromPreset('penciller', this.chapter.pencillers, PersonRole.Penciller),
      this.updateFromPreset('publisher', this.chapter.publishers, PersonRole.Publisher),
      this.updateFromPreset('imprint', this.chapter.imprints, PersonRole.Imprint),
      this.updateFromPreset('translator', this.chapter.translators, PersonRole.Translator),
      this.updateFromPreset('teams', this.chapter.teams, PersonRole.Team),
      this.updateFromPreset('locations', this.chapter.locations, PersonRole.Location),
    ]).pipe(map(results => {
      return of(true);
    }));
  }

  fetchPeople(role: PersonRole, filter: string) {
    return this.metadataService.getAllPeople().pipe(map(people => {
      return people.filter(p => this.utilityService.filter(p.name, filter));
    }));
  }

  createBlankPersonSettings(id: string, role: PersonRole) {
    let personSettings = new TypeaheadSettings<Person>();
    personSettings.minCharacters = 0;
    personSettings.multiple = true;
    personSettings.showLocked = true;
    personSettings.unique = true;
    personSettings.addIfNonExisting = true;
    personSettings.id = id;
    personSettings.compareFn = (options: Person[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.name, filter));
    }
    personSettings.compareFnForAdd = (options: Person[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.name, filter));
    }

    personSettings.selectionCompareFn = (a: Person, b: Person) => {
      return a.name == b.name;
    }
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(role, filter).pipe(map(items => personSettings.compareFn(items, filter)));
    };

    personSettings.addTransformFn = ((title: string) => {
      return {id: 0, name: title, role: role, description: '', coverImage: '', coverImageLocked: false, primaryColor: '', secondaryColor: '' };
    });

    return personSettings;
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

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings[role];
  }
}
