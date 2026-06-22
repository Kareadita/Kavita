import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  signal
} from '@angular/core';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {
  NgbActiveModal,
  NgbCollapse,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet
} from '@ng-bootstrap/ng-bootstrap';
import {concat, delay, forkJoin, last, Observable, of, tap} from 'rxjs';
import {map, switchMap} from 'rxjs/operators';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {setupLanguageSettings, TypeaheadSettings} from 'src/app/typeahead/_models/typeahead-settings';
import {Chapter, LooseLeafOrDefaultNumber, SpecialVolumeNumber} from 'src/app/_models/chapter';
import {Genre} from 'src/app/_models/metadata/genre';
import {AgeRatingDto} from 'src/app/_models/metadata/age-rating-dto';
import {Language} from 'src/app/_models/metadata/language';
import {PublicationStatusDto} from 'src/app/_models/metadata/publication-status-dto';
import {Person, PersonRole} from 'src/app/_models/metadata/person';
import {Series} from 'src/app/_models/series';
import {SeriesMetadata} from 'src/app/_models/metadata/series-metadata';
import {Tag} from 'src/app/_models/tag';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {MetadataService} from 'src/app/_services/metadata.service';
import {SeriesService} from 'src/app/_services/series.service';
import {UploadService} from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {CoverImageChooserComponent} from "../../cover-image-chooser/cover-image-chooser.component";
import {EditSeriesRelationComponent} from "../../edit-series-relation/edit-series-relation.component";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {TimeAgoPipe} from "../../../_pipes/time-ago.pipe";
import {PublicationStatusPipe} from "../../../_pipes/publication-status.pipe";
import {BytesPipe} from "../../../_pipes/bytes.pipe";
import {ImageComponent} from "../../../shared/image/image.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {EditListComponent} from "../../../shared/edit-list/edit-list.component";
import {AccountService} from "../../../_services/account.service";
import {SettingButtonComponent} from "../../../settings/_components/setting-button/setting-button.component";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {LicenseService} from "../../../_services/license.service";
import {DecimalPipe, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {ActionFactoryService} from "../../../_services/action-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";
import {
  EditExternalMetadataFormComponent
} from "../../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";
import {MangaFormat} from "../../../_models/manga-format";
import {LibraryType} from "../../../_models/library/library";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {Volume} from "../../../_models/volume";


@Component({
  selector: 'app-edit-series-modal',
  imports: [
    ReactiveFormsModule,
    NgbNav,
    NgbNavContent,
    NgbNavItem,
    NgbNavLink,
    TypeaheadComponent,
    CoverImageChooserComponent,
    EditSeriesRelationComponent,
    SentenceCasePipe,
    MangaFormatPipe,
    DefaultDatePipe,
    TimeAgoPipe,
    PublicationStatusPipe,
    BytesPipe,
    ImageComponent,
    NgbCollapse,
    NgbNavOutlet,
    DefaultValuePipe,
    TranslocoModule,
    UtcToLocalTimePipe,
    EditListComponent,
    SettingButtonComponent,
    SettingItemComponent,
    NgTemplateOutlet,
    DecimalPipe,
    TitleCasePipe,
    TabTitlePipe,
    EditExternalMetadataFormComponent
  ],
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
  protected readonly utilityService = inject(UtilityService);
  private readonly fb = inject(FormBuilder);
  protected readonly imageService = inject(ImageService);
  private readonly libraryService = inject(LibraryService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  protected readonly Tabs = Tabs;
  protected readonly PersonRole = PersonRole;
  protected readonly Action = Action;

  @Input({required: true}) series!: Series;


  seriesVolumes: any[] = [];
  isLoadingVolumes = signal<boolean>(false);
  /**
   * A copy of the series from init. This is used to compare values for name fields to see if lock was modified
   */
  initSeries!: Series;
  tasks = this.actionFactoryService.getActionablesForSettingsPage(
    this.actionFactoryService.getSeriesActions(), this.blacklist);
  volumeCollapsed: any = {};
  active = Tabs.General;
  editSeriesForm!: FormGroup;
  libraryName: string | undefined = undefined;
  size: number = 0;
  libraryType = LibraryType.Manga;


  // Typeaheads
  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<Language> | null = null;
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  genreSettings: TypeaheadSettings<Genre> = new TypeaheadSettings();

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];
  publicationStatuses: Array<PublicationStatusDto> = [];

  metadata!: SeriesMetadata;
  selectedCover: string = '';
  coverImageReset = false;
  coverImageDirty = false;
  chooserConfig: CoverImageChooserConfig = {};

  saveNestedComponents: EventEmitter<void> = new EventEmitter();

  get blacklist() {
    return [Action.Edit, Action.Info, Action.IncognitoRead, Action.Read, Action.SendTo,
      Action.AddToWantToReadList, Action.AddToCollection, Action.AddToReadingList, Action.RemoveFromWantToReadList,
      Action.RemoveFromWantToReadList];
  }

  get WebLinks() {
    return this.metadata?.webLinks.split(',') || [''];
  }

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings[role];
  }

  ngOnInit(): void {

    this.libraryService.getLibraryNames().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(names => {
      this.libraryName = names[this.series.libraryId];
    });



    this.initSeries = Object.assign({}, this.series);

    this.editSeriesForm = this.fb.group({
      id: new FormControl(this.series.id, []),
      summary: new FormControl('', []),
      name: new FormControl(this.series.name, [Validators.required]),
      localizedName: new FormControl(this.series.localizedName, []),
      sortName: new FormControl(this.series.sortName, [Validators.required]),
      rating: new FormControl(this.series.userRating, []),

      coverImageLocked: new FormControl(this.series.coverImageLocked, []),

      ageRating: new FormControl('', []),
      publicationStatus: new FormControl('', []),
      language: new FormControl('', []),
      releaseYear: new FormControl('', [Validators.minLength(4), Validators.maxLength(4), Validators.pattern(/([1-9]\d{3})|[0]{1}/)]),
    });
    this.cdRef.markForCheck();


    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });

    this.metadataService.getAllPublicationStatus().subscribe(statuses => {
      this.publicationStatuses = statuses;
      this.cdRef.markForCheck();
    });



    this.seriesService.getMetadata(this.series.id).subscribe(metadata => {
      if (metadata) {
        this.metadata = metadata;

        this.setupTypeaheads();
        this.editSeriesForm.get('summary')?.patchValue(this.metadata.summary);
        this.editSeriesForm.get('ageRating')?.patchValue(this.metadata.ageRating);
        this.editSeriesForm.get('publicationStatus')?.patchValue(this.metadata.publicationStatus);
        this.editSeriesForm.get('language')?.patchValue(this.metadata.language);
        this.editSeriesForm.get('releaseYear')?.patchValue(this.metadata.releaseYear);

        this.cdRef.markForCheck();

        this.editSeriesForm.get('name')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.series.nameLocked = true;
          this.cdRef.markForCheck();
        });

        this.editSeriesForm.get('sortName')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.series.sortNameLocked = true;
          this.cdRef.markForCheck();
        });

        this.editSeriesForm.get('localizedName')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.series.localizedNameLocked = true;
          this.cdRef.markForCheck();
        });

        this.editSeriesForm.get('summary')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.metadata.summaryLocked = true;
          this.metadata.summary = val;
          this.cdRef.markForCheck();
        });


        this.editSeriesForm.get('ageRating')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.metadata.ageRating = parseInt(val + '', 10);
          this.metadata.ageRatingLocked = true;
          this.cdRef.markForCheck();
        });

        this.editSeriesForm.get('publicationStatus')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.metadata.publicationStatus = parseInt(val + '', 10);
          this.metadata.publicationStatusLocked = true;
          this.cdRef.markForCheck();
        });

        this.editSeriesForm.get('releaseYear')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
          this.metadata.releaseYear = parseInt(val + '', 10);
          this.metadata.releaseYearLocked = true;
          this.cdRef.markForCheck();
        });
      }
    });

    this.isLoadingVolumes.set(true);

    forkJoin({volumes: this.seriesService.getVolumes(this.series.id), libraryType: this.libraryService.getLibraryType(this.series.libraryId)}).subscribe(res => {
      const volumes = res.volumes;
      const libraryType = res.libraryType;

      this.seriesVolumes = volumes;
      this.libraryType = libraryType;
      this.isLoadingVolumes.set(false);
      this.chooserConfig = this.coverChooserConfigFactory.forSeries(this.series, this.seriesVolumes, this.libraryType);

      volumes.forEach(v => {
        this.volumeCollapsed[v.name] = true;
      });

      this.seriesVolumes.forEach(vol => {
        vol.volumeFiles = vol.chapters?.map((c: Chapter) => c.files.map((f: any) => {
          // TODO: Identify how to fix this hack
          f.chapter = c.range;
          return f;
        })).flat();
      });

      if (volumes.length > 0) {
        this.size = volumes.reduce((sum1, volume) => {
          return sum1 + volume.chapters.reduce((sum2, chapter) => {
            return sum2 + chapter.files.reduce((sum3, file) => {
              return sum3 + file.bytes;
            }, 0);
          }, 0);
        }, 0);
      }
      this.cdRef.markForCheck();
    });

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
    this.tagsSettings.trackByIdentityFn = (index, value) => value.title + (value.id + '');

    if (this.metadata.tags) {
      this.tagsSettings.savedData = this.metadata.tags;
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
    this.genreSettings.trackByIdentityFn = (index, value) => value.title + (value.id + '');

    if (this.metadata.genres) {
      this.genreSettings.savedData = this.metadata.genres;
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
        this.metadataService.updatePerson(this.metadata, personSettings.savedData as Person[], role);
        this.cdRef.markForCheck();
        return true;
      }));
    } else {
      this.peopleSettings[role] = personSettings;
      return of(true);
    }
  }

  setupLanguageTypeahead() {


    return this.metadataService.getAllValidLanguages()
      .pipe(
        tap(validLanguages => {
          this.languageSettings = setupLanguageSettings(true, this.utilityService, validLanguages, this.metadata.language);
          this.cdRef.markForCheck();
        }),
        switchMap(_ => of(true))
    );
  }

  setupPersonTypeahead() {
    this.peopleSettings = {};

    return forkJoin([
      this.updateFromPreset('writer', this.metadata.writers, PersonRole.Writer),
      this.updateFromPreset('character', this.metadata.characters, PersonRole.Character),
      this.updateFromPreset('colorist', this.metadata.colorists, PersonRole.Colorist),
      this.updateFromPreset('cover-artist', this.metadata.coverArtists, PersonRole.CoverArtist),
      this.updateFromPreset('editor', this.metadata.editors, PersonRole.Editor),
      this.updateFromPreset('inker', this.metadata.inkers, PersonRole.Inker),
      this.updateFromPreset('letterer', this.metadata.letterers, PersonRole.Letterer),
      this.updateFromPreset('penciller', this.metadata.pencillers, PersonRole.Penciller),
      this.updateFromPreset('publisher', this.metadata.publishers, PersonRole.Publisher),
      this.updateFromPreset('imprint', this.metadata.imprints, PersonRole.Imprint),
      this.updateFromPreset('translator', this.metadata.translators, PersonRole.Translator),
      this.updateFromPreset('teams', this.metadata.teams, PersonRole.Team),
      this.updateFromPreset('locations', this.metadata.locations, PersonRole.Location),
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
    const personSettings = new TypeaheadSettings<Person>();
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
      return {id: 0, name: title, aliases: [], description: '', coverImageLocked: false, primaryColor: '', secondaryColor: '' };
    });
    personSettings.trackByIdentityFn = (index, value) => value.name + (value.id + '');

    return personSettings;
  }

  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.series, true));
    } else {
      this.modal.dismiss();
    }
  }

  updateWeblinks(items: Array<string>) {
    this.metadata.webLinks = items.map(s => s.replaceAll(',', '%2C')).join(',');
  }


  save() {
    const model = this.editSeriesForm.getRawValue();

    const apis = [
      this.seriesService.updateMetadata(this.metadata)
    ];

    // We only need to call updateSeries if we changed name, sort name, or localized name or reset a cover image
    const nameFieldsDirty = this.editSeriesForm.get('name')?.dirty || this.editSeriesForm.get('sortName')?.dirty || this.editSeriesForm.get('localizedName')?.dirty;
    const nameFieldLockChanged = this.series.nameLocked !== this.initSeries.nameLocked || this.series.sortNameLocked !== this.initSeries.sortNameLocked || this.series.localizedNameLocked !== this.initSeries.localizedNameLocked;

    let updatedSeries: Series | null = null;

    if (nameFieldsDirty || nameFieldLockChanged) {
      model.nameLocked = this.series.nameLocked;
      model.sortNameLocked = this.series.sortNameLocked;
      model.localizedNameLocked = this.series.localizedNameLocked;
      model.language = this.metadata.language;
    }

    apis.push(this.seriesService.updateSeries(model).pipe(
      tap(result => updatedSeries = result)
    ));

    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateSeriesCoverImage(model.id, this.selectedCover, true));
    }

    this.saveNestedComponents.emit();

    // Run api calls sequentially to prevent them from overwriting each other in a race condition
    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe(() => {
      this.modal.close(modalSaved(updatedSeries ?? model, this.coverImageDirty || this.coverImageReset));
    });
  }


  updateTags(tags: Tag[]) {
    this.tags = tags;
    this.metadata.tags = tags;
    this.cdRef.markForCheck();
  }

  updateGenres(genres: Genre[]) {
    this.genres = genres;
    this.metadata.genres = genres;
    this.cdRef.markForCheck();
  }

  updatePerson(persons: Person[], role: PersonRole) {
    this.metadataService.updatePerson(this.metadata, persons, role);
    this.cdRef.markForCheck();
  }

  updateLanguage(language: Array<Language>) {
    if (language.length === 0) {
      this.metadata.language = '';
      return;
    }
    this.metadata.language = language[0].isoCode;
    this.cdRef.markForCheck();
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
  }

  handleReset() {
    this.coverImageReset = true;
    this.editSeriesForm.patchValue({ coverImageLocked: false });
    this.chooserConfig = { ...this.chooserConfig, isLocked: false };
    this.cdRef.markForCheck();
  }

  unlock(b: any, field: string) {
    if (b) {
      b[field] = !b[field];
    }
    this.cdRef.markForCheck();
  }

  async runTask(action: ActionItem<Series>) {
    action.callback(action,  this.series);
  }

  formatVolumeName(volume: Volume) {
    if (volume.minNumber === LooseLeafOrDefaultNumber) {
      return translate('edit-series-modal.loose-leaf-volume');
    } else if (volume.minNumber === SpecialVolumeNumber) {
      return translate('edit-series-modal.specials-volume');
    }
    return translate('edit-series-modal.volume-num', {num: volume.name});
  }

  protected readonly LooseLeafOrDefaultNumber = LooseLeafOrDefaultNumber;
  protected readonly MangaFormat = MangaFormat;
}
