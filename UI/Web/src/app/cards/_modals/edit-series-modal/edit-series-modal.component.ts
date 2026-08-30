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
import {NgbActiveModal, NgbCollapse} from '@ng-bootstrap/ng-bootstrap';
import {concat, delay, forkJoin, last, tap} from 'rxjs';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {CoverImageChooserComponent} from "../../cover-image-chooser/cover-image-chooser.component";
import {EditSeriesRelationComponent} from "../../edit-series-relation/edit-series-relation.component";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
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
import {
  addMetadataIdControls,
  EditExternalMetadataFormComponent
} from "../../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";
import {MangaFormat} from "../../../_models/manga-format";
import {LibraryType} from "../../../_models/library/library";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {Volume} from "../../../_models/volume";
import {ConfirmService} from "../../../shared/confirm.service";
import {EditModalShellComponent} from "../../../shared/edit-modal-shell/edit-modal-shell.component";
import {EditTabDirective} from "../../../shared/_directive/edit-tab.directive";
import {MetadataProviderTitlePipe} from "../../../_pipes/metadata-provider-title.pipe";
import {SeriesService} from "../../../_services/series.service";
import {ImageService} from "../../../_services/image.service";
import {LibraryService} from "../../../_services/library.service";
import {UploadService} from "../../../_services/upload.service";
import {MetadataService} from "../../../_services/metadata.service";
import {Person, PersonRole} from "../../../_models/metadata/person";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {Genre} from "../../../_models/metadata/genre";
import {AgeRatingDto} from "../../../_models/metadata/age-rating-dto";
import {PublicationStatusDto} from "../../../_models/metadata/publication-status-dto";
import {SeriesMetadata} from "../../../_models/metadata/series-metadata";
import {Chapter, LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../../../_models/chapter";
import {Language} from "../../../_models/metadata/language";
import {Series} from "../../../_models/series";
import {Tag} from "../../../_models/tag";
import {AllMetadataProviders, MetadataProvider} from "../../../_models/kavitaplus/metadata-provider.enum";
import {TimeDifferencePipe} from "../../../_pipes/time-difference.pipe";
import {TypeaheadSettingsFactoryService} from "../../../typeahead-settings-factory.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";


@Component({
  selector: 'app-edit-series-modal',
  imports: [
    ReactiveFormsModule,
    TypeaheadComponent,
    CoverImageChooserComponent,
    EditSeriesRelationComponent,
    SentenceCasePipe,
    MangaFormatPipe,
    DefaultDatePipe,
    PublicationStatusPipe,
    BytesPipe,
    ImageComponent,
    NgbCollapse,
    DefaultValuePipe,
    TranslocoModule,
    UtcToLocalTimePipe,
    EditListComponent,
    SettingButtonComponent,
    SettingItemComponent,
    NgTemplateOutlet,
    DecimalPipe,
    EditExternalMetadataFormComponent,
    EditModalShellComponent,
    EditTabDirective,
    MetadataProviderTitlePipe,
    TitleCasePipe,
    TimeDifferencePipe,
    FormFieldDirective
  ],
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
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
  private readonly confirmService = inject(ConfirmService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadSettingsFactoryService);

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
  libraryType = signal<LibraryType>(LibraryType.Manga);
  protected readonly allMetadataProviders = AllMetadataProviders;


  // Typeaheads
  tagsSettings = signal<TypeaheadSettings<Tag> | null>(null);
  languageSettings = signal<TypeaheadSettings<Language> | null>(null);
  peopleSettings = signal<Partial<Record<PersonRole, TypeaheadSettings<Person>>>>({});
  genreSettings = signal<TypeaheadSettings<Genre> | null>(null);

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];
  publicationStatuses: Array<PublicationStatusDto> = [];

  metadata!: SeriesMetadata;
  selectedCover: string = '';
  coverImageReset = false;
  coverImageDirty = false;
  chooserConfig = signal<CoverImageChooserConfig>({});

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
    return this.peopleSettings()[role];
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
      metadataProviderOverride: new FormControl<MetadataProvider | null>(this.series.metadataProviderOverride ?? null, []),
    });

    addMetadataIdControls(this.editSeriesForm, this.series);

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
      this.libraryType.set(libraryType);
      this.isLoadingVolumes.set(false);
      this.chooserConfig.set(this.coverChooserConfigFactory.forSeries(this.series, this.seriesVolumes, this.libraryType()));

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

    this.languageSettings.set(this.typeaheadSettingsFactory.forLanguage({id: 'language', currentSelectedLanguage: this.metadata.language}));
    this.tagsSettings.set(this.typeaheadSettingsFactory.forTag({id: 'tags', savedData: this.metadata.tags ?? []}));
    this.genreSettings.set(this.typeaheadSettingsFactory.forGenre({id: 'genres', savedData: this.metadata.genres ?? []}));

    this.setupPersonTypeahead();
  }

  setupPersonTypeahead() {
    const roles: ReadonlyArray<[string, PersonRole, Array<Person> | undefined]> = [
      ['writer', PersonRole.Writer, this.metadata.writers],
      ['character', PersonRole.Character, this.metadata.characters],
      ['colorist', PersonRole.Colorist, this.metadata.colorists],
      ['cover-artist', PersonRole.CoverArtist, this.metadata.coverArtists],
      ['editor', PersonRole.Editor, this.metadata.editors],
      ['inker', PersonRole.Inker, this.metadata.inkers],
      ['letterer', PersonRole.Letterer, this.metadata.letterers],
      ['penciller', PersonRole.Penciller, this.metadata.pencillers],
      ['publisher', PersonRole.Publisher, this.metadata.publishers],
      ['imprint', PersonRole.Imprint, this.metadata.imprints],
      ['translator', PersonRole.Translator, this.metadata.translators],
      ['teams', PersonRole.Team, this.metadata.teams],
      ['locations', PersonRole.Location, this.metadata.locations],
    ];

    this.metadataService.getAllPeople().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(people => {
      const settings: Partial<Record<PersonRole, TypeaheadSettings<Person>>> = {};

      for (const [id, role, preset] of roles) {
        const personSettings = this.typeaheadSettingsFactory.forPerson({id, role});

        if (preset && preset.length > 0) {
          const presetIds = preset.map(p => p.id);
          personSettings.savedData = people.filter(person => presetIds.includes(person.id));
          this.metadataService.updatePerson(this.metadata, personSettings.savedData, role);
        }

        settings[role] = personSettings;
      }

      this.peopleSettings.set(settings);
    });
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


  async save() {
    const model = this.editSeriesForm.getRawValue();

    const nameChanged = this.editSeriesForm.get('name')?.dirty ?? false;

    // If the user renamed the series but has a locked (custom) sort name, offer to align it.
    // When the sort name is unlocked the backend reseeds it from the new name automatically.
    if (nameChanged && this.series.sortNameLocked && model.sortName !== model.name) {
      if (await this.confirmService.confirm(translate('edit-series-modal.align-sort-name'))) {
        model.sortName = model.name;
        this.editSeriesForm.get('sortName')?.patchValue(model.name);
      }
    }

    let updatedSeries: Series | null = null;

    model.nameLocked = this.series.nameLocked;
    model.sortNameLocked = this.series.sortNameLocked;
    model.localizedNameLocked = this.series.localizedNameLocked;
    model.language = this.metadata.language;

    // updateSeries runs first so a name collision (400) short-circuits the chain before metadata is written
    const apis = [
      this.seriesService.updateSeries(model).pipe(tap(result => updatedSeries = result)),
      this.seriesService.updateMetadata(this.metadata)
    ];

    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateSeriesCoverImage(model.id, this.selectedCover, true));
    }

    this.saveNestedComponents.emit();

    // Run api calls sequentially to prevent them from overwriting each other in a race condition
    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe({
      next: () => {
        this.modal.close(modalSaved(updatedSeries ?? model, this.coverImageDirty || this.coverImageReset));
      },
      error: () => {
        // A duplicate name (400) is surfaced by the global error interceptor; keep the modal open
        this.cdRef.markForCheck();
      }
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
    this.chooserConfig.set({ ...this.chooserConfig(), isLocked: false });
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

  changeTab(tab?: Tabs) {
    if (!tab) return;
    this.active = tab;
    this.cdRef.markForCheck();
  }

  protected readonly LooseLeafOrDefaultNumber = LooseLeafOrDefaultNumber;
  protected readonly MangaFormat = MangaFormat;
}
