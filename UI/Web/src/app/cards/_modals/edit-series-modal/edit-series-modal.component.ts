import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  model,
  OnInit,
  signal
} from '@angular/core';
import {NgbActiveModal, NgbCollapse} from '@ng-bootstrap/ng-bootstrap';
import {forkJoin, map, of, switchMap} from 'rxjs';
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
import {DecimalPipe, TitleCasePipe} from "@angular/common";
import {ActionFactoryService} from "../../../_services/action-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {
  applyExternalMetadataIdRules,
  EditExternalMetadataFormComponent
} from "../../../shared/_components/edit-external-metadata-form/edit-external-metadata-form.component";
import {form, FormField, pattern, required} from "@angular/forms/signals";
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
import {allPeopleRoles, Person, PersonRole} from "../../../_models/metadata/person";
import {TypeaheadConfig} from "../../../typeahead/_models/typeahead-config";
import {Genre} from "../../../_models/metadata/genre";
import {AgeRatingDto} from "../../../_models/metadata/age-rating-dto";
import {PublicationStatusDto} from "../../../_models/metadata/publication-status-dto";
import {SeriesMetadata} from "../../../_models/metadata/series-metadata";
import {LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../../../_models/chapter";
import {Language} from "../../../_models/metadata/language";
import {Series} from "../../../_models/series";
import {Tag} from "../../../_models/tag";
import {AllMetadataProviders, MetadataProvider} from "../../../_models/kavitaplus/metadata-provider.enum";
import {TimeDifferencePipe} from "../../../_pipes/time-difference.pipe";
import {TypeaheadConfigFactoryService} from "../../../typeahead-config-factory.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {lockGroup, standaloneLocks, writeFieldLocks, writeNamedLocks} from "../../../_helpers/field-lock";
import {LockableFieldComponent} from "../../../shared/_components/lockable-field/lockable-field.component";
import {personFields, PersonFields, personFieldsFrom} from "../../../_helpers/person-fields";
import {IHasMetadataIds} from "../../../_models/common/i-has-metadata-ids";
import {AgeRating} from "../../../_models/metadata/age-rating";
import {PublicationStatus} from "../../../_models/metadata/publication-status";

interface MetadataFormModel extends PersonFields {
  summary: string;
  ageRating: string;
  publicationStatus: string;
  language: string;
  releaseYear: string;
  genres: Genre[];
  tags: Tag[];
  webLinks: string;
}

interface FormModel extends IHasMetadataIds {
  id: number;
  name: string;
  localizedName: string;
  sortName: string;
  rating: number;
  coverImage: string;
  metadataProviderOverride: string;
  metadata: MetadataFormModel;
}

const blacklist = [Action.Edit, Action.Info, Action.IncognitoRead, Action.Read, Action.SendTo,
  Action.AddToWantToReadList, Action.AddToCollection, Action.AddToReadingList, Action.RemoveFromWantToReadList,
  Action.RemoveFromWantToReadList];

@Component({
  selector: 'app-edit-series-modal',
  imports: [
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
    DecimalPipe,
    EditExternalMetadataFormComponent,
    EditModalShellComponent,
    EditTabDirective,
    MetadataProviderTitlePipe,
    TitleCasePipe,
    TimeDifferencePipe,
    FormFieldDirective,
    FormField,
    LockableFieldComponent
  ],
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
  protected readonly imageService = inject(ImageService);
  private readonly libraryService = inject(LibraryService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  protected readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);
  private readonly confirmService = inject(ConfirmService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadConfigFactoryService);

  series = model.required<Series>();

  private readonly formModel = signal<FormModel>({
    coverImage: '',
    id: 0,
    name: '',
    localizedName: '',
    sortName: '',
    rating: 0,
    metadataProviderOverride: '',
    aniListId: 0,
    malId: 0,
    hardcoverId: 0,
    metronId: 0,
    comicVineId: null,
    mangaBakaId: 0,
    cbrId: 0,
    metadata: {
      summary: '',
      ageRating: AgeRating.Unknown.toString(),
      publicationStatus: PublicationStatus.OnGoing.toString(),
      language: '',
      releaseYear: '',
      genres: [],
      tags: [],
      webLinks: '',
      ...personFieldsFrom({}),
    }
  });
  protected readonly formGroup = form(this.formModel, p => {
    required(p.name);
    required(p.sortName);
    pattern(p.metadata.releaseYear, /^[1-9]\d{3}$/);
    applyExternalMetadataIdRules(p);
  });

  protected readonly locks = lockGroup(this.formGroup, () => this.series(), [
    'name', 'sortName', 'localizedName', 'coverImage',
  ]);
  protected readonly metadataLocks = lockGroup(this.formGroup.metadata, () => this.metadata()!, [
    'summary', 'ageRating', 'publicationStatus', 'language', 'releaseYear', 'genres', 'tags',
  ]);
  protected readonly personLocks = standaloneLocks(() => this.metadata()!,
    Object.values(personFields).map(f => f.lock));


  protected readonly isLoadingVolumes = signal<boolean>(false);
  protected readonly tasks = computed(() => this.actionFactoryService.getActionablesForSettingsPage(
    this.actionFactoryService.getSeriesActions(), blacklist));
  protected readonly activeTabId = signal<Tabs>(Tabs.General);
  protected readonly libraryName = signal<string>('');
  protected readonly libraryType = signal<LibraryType>(LibraryType.Manga);

  protected readonly expandedVolumes = signal<ReadonlySet<number>>(new Set());
  protected readonly seriesVolumes = signal<Volume[]>([]);
  protected readonly size = computed(() => {
    return this.seriesVolumes().reduce((sum1, volume) => {
      return sum1 + volume.chapters.reduce((sum2, chapter) => {
        return sum2 + chapter.files.reduce((sum3, file) => {
          return sum3 + file.bytes;
        }, 0);
      }, 0);
    }, 0);
  });
  protected readonly volumeRows = computed(() => this.seriesVolumes().map(v => ({
    volume: v,
    files: v.chapters.flatMap(c => c.files.map(f => ({...f, chapter: c.range}))),
  })));



  // Typeaheads
  protected readonly tagsTypeaheadSettings = computed(() => {
    return this.typeaheadSettingsFactory.forTag({id: 'tags', savedData: this.formModel().metadata.tags ?? []});
  });
  protected readonly languageTypeaheadSettings = computed(() => {
    return this.typeaheadSettingsFactory.forLanguage({id: 'language', currentSelectedLanguage: this.formModel().metadata.language})
  });
  protected readonly genreTypeaheadSettings = computed(() => {
    return this.typeaheadSettingsFactory.forGenre({id: 'genres', savedData: this.formModel().metadata.genres ?? []});
  });
  private readonly peopleSettings = signal<Partial<Record<PersonRole, TypeaheadConfig<Person>>>>({});

  protected readonly ageRatings = signal<AgeRatingDto[]>([]);
  protected readonly publicationStatuses = signal<PublicationStatusDto[]>([]);
  protected readonly metadata = signal<SeriesMetadata | null>(null);
  private selectedCover = '';
  private coverImageReset = false;
  private coverImageDirty = false;

  protected readonly saveNestedComponents = new EventEmitter<void>();

  protected readonly chooserConfig = computed<CoverImageChooserConfig>(() => ({
    ...this.coverChooserConfigFactory.forSeries(this.series(), this.seriesVolumes(), this.libraryType()),
    isLocked: this.locks.coverImage()
  }));

  protected readonly weblinks = computed(() => {
    const webLinks = this.formGroup.metadata.webLinks().value();
    if (!webLinks || webLinks.length === 0) return [];
    return webLinks.split(',');
  });

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings()[role];
  }

  ngOnInit(): void {
    const series = this.series();

    this.formModel.update(m => ({
      ...m,
      id: series.id,
      name: series.name,
      localizedName: series.localizedName,
      sortName: series.sortName,
      rating: series.userRating,
      metadataProviderOverride: series.metadataProviderOverride?.toString() ?? '',
      aniListId: series.aniListId,
      malId: series.malId,
      hardcoverId: series.hardcoverId,
      metronId: series.metronId,
      comicVineId: series.comicVineId,
      mangaBakaId: series.mangaBakaId,
      cbrId: series.cbrId,
    }));

    this.libraryService.getLibraryNames().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(names => {
      this.libraryName.set(names[this.series().libraryId]);
    });

    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings.set(ratings);
    });

    this.metadataService.getAllPublicationStatus().subscribe(statuses => {
      this.publicationStatuses.set(statuses);
    });

    this.seriesService.getMetadata(this.series().id).subscribe(metadata => {
      if (metadata) {
        this.metadata.set(metadata);

        this.setupPersonTypeahead();

        this.formModel.update(m => ({
          ...m,
          metadata: {
            ...m.metadata,
            summary: metadata.summary || '',
            ageRating: metadata.ageRating.toString(),
            publicationStatus: metadata.publicationStatus.toString(),
            language: metadata.language,
            releaseYear: metadata.releaseYear > 0 ? metadata.releaseYear.toString() : '',
            genres: metadata.genres ?? [],
            tags: metadata.tags ?? [],
            webLinks: metadata.webLinks,
            ...personFieldsFrom(metadata),
          },
        }));
      }
    });

    this.isLoadingVolumes.set(true);

    forkJoin({volumes: this.seriesService.getVolumes(this.series().id), libraryType: this.libraryService.getLibraryType(this.series().libraryId)}).subscribe(res => {
      const volumes = res.volumes;
      const libraryType = res.libraryType;

      this.libraryType.set(libraryType);
      this.isLoadingVolumes.set(false);
      this.seriesVolumes.set(volumes);
    });

  }




  setupPersonTypeahead() {
    this.metadataService.getAllPeople().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(people => {
      const settings: Partial<Record<PersonRole, TypeaheadConfig<Person>>> = {};

      for (const role of allPeopleRoles) {
        const field = personFields[role];
        const personSettings = this.typeaheadSettingsFactory.forPerson({id: field.id, role});
        const preset = this.formGroup.metadata[field.model]().value();

        if (preset.length > 0) {
          personSettings.savedData = people.filter(p => preset.some(x => x.id === p.id));
          this.formGroup.metadata[field.model]().value.set(personSettings.savedData);
        }

        settings[role] = personSettings;
      }

      this.peopleSettings.set(settings);
    });
  }

  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.series(), true));
    } else {
      this.modal.dismiss();
    }
  }

  toggleVolume(id: number) {
    this.expandedVolumes.update(s => {
      const next = new Set(s);
      if (!next.delete(id)) {
        next.add(id);
      }
      return next;
    });
  }

  updateWeblinks(items: Array<string>) {
    this.formGroup.metadata.webLinks().value.set(items.map(s => s.replaceAll(',', '%2C')).join(','));
  }


  async save() {
    const metadata = this.metadata();
    if (metadata === null) return;

    const model = this.formModel();

    // If the user renamed the series but has a locked (custom) sort name, offer to align it.
    // When the sort name is unlocked the backend reseeds it from the new name automatically.
    const nameChanged = this.formGroup.name().dirty() ?? false;
    if (nameChanged && this.series().sortNameLocked && model.sortName !== model.name) {
      if (await this.confirmService.confirm(translate('edit-series-modal.align-sort-name'))) {
        model.sortName = model.name;
        this.formGroup.sortName().value.set(model.name);
      }
    }

    const seriesPayload = {
      ...this.series(),
      ...model,
      metadataProviderOverride: model.metadataProviderOverride === '' ? null : parseInt(model.metadataProviderOverride, 10) as MetadataProvider
    };
    writeFieldLocks(seriesPayload, this.locks);

    const metadataPayload: SeriesMetadata = {
      ...metadata,
      ...model.metadata,
      ageRating: parseInt(model.metadata.ageRating, 10) as AgeRating,
      publicationStatus: parseInt(model.metadata.publicationStatus, 10) as PublicationStatus,
      releaseYear: parseInt(model.metadata.releaseYear, 10) || 0,
    };
    writeFieldLocks(metadataPayload, this.metadataLocks);
    writeNamedLocks(metadataPayload, this.personLocks);

    this.saveNestedComponents.emit();

    // updateSeries runs first so a name collision (400) short-circuits the chain before metadata is written
    this.seriesService.updateSeries(seriesPayload).pipe(
      switchMap(series => this.seriesService.updateMetadata(metadataPayload).pipe(map(() => series))),
      switchMap(series => this.coverImageDirty
        ? this.uploadService.updateSeriesCoverImage(this.series().id, this.selectedCover, true).pipe(map(() => series))
        : of(series))
    ).subscribe((c) => {
      this.series.set(c);
      const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
      this.modal.close(modalSaved(this.series(), needsCoverUpdate));
    });
  }


  updateTags(tags: Tag[]) {
    this.formGroup.metadata.tags().value.set(tags);
  }

  updateGenres(genres: Genre[]) {
    this.formGroup.metadata.genres().value.set(genres);
  }

  updatePerson(persons: Person[], role: PersonRole) {
    const field = personFields[role];
    this.formGroup.metadata[field.model]().value.set(persons);
    this.personLocks[field.lock].set(true);
  }

  updateLanguage(language: Array<Language>) {
    if (language.length === 0) {
      this.formGroup.metadata.language().value.set('');
      return;
    }
    this.formGroup.metadata.language().value.set(language[0].isoCode);
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
  }

  handleReset() {
    this.coverImageReset = true;
    this.locks.coverImage.set(false);
  }

  async runTask(action: ActionItem<Series>) {
    action.callback(action,  this.series());
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
    this.activeTabId.set(tab);
  }

  protected readonly LooseLeafOrDefaultNumber = LooseLeafOrDefaultNumber;
  protected readonly MangaFormat = MangaFormat;
  protected readonly allMetadataProviders = AllMetadataProviders;
  protected readonly Tabs = Tabs;
  protected readonly PersonRole = PersonRole;
  protected readonly Action = Action;
  protected readonly parseInt = parseInt;
}
