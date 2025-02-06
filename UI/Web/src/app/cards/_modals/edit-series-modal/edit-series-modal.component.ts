import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit
} from '@angular/core';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {
  NgbActiveModal, NgbCollapse,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {forkJoin, Observable, of, tap} from 'rxjs';
import { map } from 'rxjs/operators';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/_models/typeahead-settings';
import {Chapter, LooseLeafOrDefaultNumber, SpecialVolumeNumber} from 'src/app/_models/chapter';
import { Genre } from 'src/app/_models/metadata/genre';
import { AgeRatingDto } from 'src/app/_models/metadata/age-rating-dto';
import { Language } from 'src/app/_models/metadata/language';
import { PublicationStatusDto } from 'src/app/_models/metadata/publication-status-dto';
import { Person, PersonRole } from 'src/app/_models/metadata/person';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/metadata/series-metadata';
import { Tag } from 'src/app/_models/tag';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { SeriesService } from 'src/app/_services/series.service';
import { UploadService } from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CommonModule} from "@angular/common";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {CoverImageChooserComponent} from "../../cover-image-chooser/cover-image-chooser.component";
import {EditSeriesRelationComponent} from "../../edit-series-relation/edit-series-relation.component";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {TimeAgoPipe} from "../../../_pipes/time-ago.pipe";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";
import {PublicationStatusPipe} from "../../../_pipes/publication-status.pipe";
import {BytesPipe} from "../../../_pipes/bytes.pipe";
import {ImageComponent} from "../../../shared/image/image.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {TranslocoDatePipe} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {EditListComponent} from "../../../shared/edit-list/edit-list.component";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {Volume} from "../../../_models/volume";
import {Action, ActionFactoryService, ActionItem} from "../../../_services/action-factory.service";
import {SettingButtonComponent} from "../../../settings/_components/setting-button/setting-button.component";
import {ActionService} from "../../../_services/action.service";
import {DownloadService} from "../../../shared/_services/download.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {ReadTimePipe} from "../../../_pipes/read-time.pipe";
import {LicenseService} from "../../../_services/license.service";

enum TabID {
  General = 0,
  Metadata = 1,
  People = 2,
  WebLinks = 3,
  CoverImage = 4,
  Related = 5,
  Info = 6,
  Tasks = 7
}

export interface EditSeriesModalCloseResult {
  success: boolean;
  series: Series;
  coverImageUpdate: boolean;
  updateExternal: boolean
}

const blackList = [Action.Edit, Action.Info, Action.IncognitoRead, Action.Read, Action.SendTo,
  Action.AddToWantToReadList, Action.AddToCollection, Action.AddToReadingList, Action.RemoveFromWantToReadList,
  Action.RemoveFromWantToReadList];

@Component({
  selector: 'app-edit-series-modal',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    NgbNav,
    NgbNavContent,
    NgbNavItem,
    NgbNavLink,
    CommonModule,
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
    TranslocoDatePipe,
    UtcToLocalTimePipe,
    EditListComponent,
    SettingButtonComponent,
    SettingItemComponent,
  ],
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesModalComponent implements OnInit {

  public readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
  public readonly utilityService = inject(UtilityService);
  private readonly fb = inject(FormBuilder);
  public readonly imageService = inject(ImageService);
  private readonly libraryService = inject(LibraryService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly downloadService = inject(DownloadService);

  protected readonly TabID = TabID;
  protected readonly PersonRole = PersonRole;
  protected readonly Breakpoint = Breakpoint;
  protected readonly Action = Action;

  @Input({required: true}) series!: Series;


  seriesVolumes: any[] = [];
  isLoadingVolumes = false;
  /**
   * A copy of the series from init. This is used to compare values for name fields to see if lock was modified
   */
  initSeries!: Series;
  tasks = this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getSeriesActions(this.runTask.bind(this)), blackList);
  volumeCollapsed: any = {};
  tabs = ['general-tab', 'metadata-tab', 'people-tab', 'web-links-tab', 'cover-image-tab', 'related-tab', 'info-tab', 'tasks-tab'];
  active = this.tabs[0];
  editSeriesForm!: FormGroup;
  libraryName: string | undefined = undefined;
  size: number = 0;
  hasForcedKPlus = false;
  forceIsLoading = false;


  // Typeaheads
  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<Language> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  genreSettings: TypeaheadSettings<Genre> = new TypeaheadSettings();

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];
  publicationStatuses: Array<PublicationStatusDto> = [];
  validLanguages: Array<Language> = [];

  metadata!: SeriesMetadata;
  imageUrls: Array<string> = [];
  /**
   * Selected Cover for uploading
   */
  selectedCover: string = '';
  coverImageReset = false;
  isAdmin: boolean = false;

  saveNestedComponents: EventEmitter<void> = new EventEmitter();

  get WebLinks() {
    return this.metadata?.webLinks.split(',') || [''];
  }

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings[role];
  }

  ngOnInit(): void {
    this.imageUrls.push(this.imageService.getSeriesCoverImage(this.series.id));

    this.libraryService.getLibraryNames().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(names => {
      this.libraryName = names[this.series.libraryId];
    });

    this.accountService.isAdmin$.pipe(takeUntilDestroyed(this.destroyRef), tap(isAdmin => {
      this.isAdmin = isAdmin;
      this.cdRef.markForCheck();
    })).subscribe();

    this.initSeries = Object.assign({}, this.series);

    this.editSeriesForm = this.fb.group({
      id: new FormControl(this.series.id, []),
      summary: new FormControl('', []),
      name: new FormControl(this.series.name, [Validators.required]),
      localizedName: new FormControl(this.series.localizedName, []),
      sortName: new FormControl(this.series.sortName, [Validators.required]),
      rating: new FormControl(this.series.userRating, []),

      coverImageIndex: new FormControl(0, []),
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

    this.metadataService.getAllValidLanguages().subscribe(validLanguages => {
      this.validLanguages = validLanguages;
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

    this.isLoadingVolumes = true;
    this.cdRef.markForCheck();
    this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
      this.seriesVolumes = volumes;
      this.isLoadingVolumes = false;

      if (this.seriesVolumes.length === 1) {
        this.imageUrls.push(...this.seriesVolumes[0].chapters.map((c: Chapter) => this.imageService.getChapterCoverImage(c.id)));
      } else {
        this.imageUrls.push(...this.seriesVolumes.map(v => this.imageService.getVolumeCoverImage(v.id)));
      }

      volumes.forEach(v => {
        this.volumeCollapsed[v.name] = true;
      });
      this.seriesVolumes.forEach(vol => {
        //.sort(this.utilityService.sortChapters) (no longer needed, all data is sorted on the backend)
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

  formatVolumeName(volume: Volume) {
    if (volume.minNumber === LooseLeafOrDefaultNumber) {
      return translate('edit-series-modal.loose-leaf-volume');
    } else if (volume.minNumber === SpecialVolumeNumber) {
      return translate('edit-series-modal.specials-volume');
    }
    return translate('edit-series-modal.volume-num') + ' ' + volume.name;
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

    const l = this.validLanguages.find(l => l.isoCode === this.metadata.language);
    if (l !== undefined) {
      this.languageSettings.savedData = l;
    }
    return of(true);
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
      return {id: 0, name: title, description: '', coverImageLocked: false, primaryColor: '', secondaryColor: '' };
    });

    return personSettings;
  }

  close() {
    this.modal.close({success: false, series: undefined, coverImageUpdate: this.coverImageReset, updateExternal: this.hasForcedKPlus});
  }

  forceScan() {
    this.forceIsLoading = true;
    this.metadataService.forceRefreshFromPlus(this.series.id).subscribe(() => {
      this.hasForcedKPlus = true;
      this.forceIsLoading = false;
      this.toastr.info(translate('toasts.force-kavita+-refresh-success'));
      this.cdRef.markForCheck();
    });
  }

  updateWeblinks(items: Array<string>) {
    this.metadata.webLinks = items.map(s => s.replaceAll(',', '%2C')).join(',');
  }


  save() {
    const model = this.editSeriesForm.value;
    const selectedIndex = this.editSeriesForm.get('coverImageIndex')?.value || 0;

    const apis = [
      this.seriesService.updateMetadata(this.metadata)
    ];

    // We only need to call updateSeries if we changed name, sort name, or localized name or reset a cover image
    const nameFieldsDirty = this.editSeriesForm.get('name')?.dirty || this.editSeriesForm.get('sortName')?.dirty || this.editSeriesForm.get('localizedName')?.dirty;
    const nameFieldLockChanged = this.series.nameLocked !== this.initSeries.nameLocked || this.series.sortNameLocked !== this.initSeries.sortNameLocked || this.series.localizedNameLocked !== this.initSeries.localizedNameLocked;

    if (nameFieldsDirty || nameFieldLockChanged || this.coverImageReset) {
      model.nameLocked = this.series.nameLocked;
      model.sortNameLocked = this.series.sortNameLocked;
      model.localizedNameLocked = this.series.localizedNameLocked;
      model.language = this.metadata.language;
      apis.push(this.seriesService.updateSeries(model));
    }


    if (selectedIndex > 0 || this.coverImageReset) {
      apis.push(this.uploadService.updateSeriesCoverImage(model.id, this.selectedCover, !this.coverImageReset));
    }

    this.saveNestedComponents.emit();

    forkJoin(apis).subscribe(results => {
      this.modal.close({success: true, series: model, coverImageUpdate: selectedIndex > 0 || this.coverImageReset, updateExternal: this.hasForcedKPlus});
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
    this.metadata.locationLocked = true;
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

  updateSelectedIndex(index: number) {
    this.editSeriesForm.patchValue({
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
    this.editSeriesForm.patchValue({
      coverImageLocked: false
    });
    this.cdRef.markForCheck();
  }

  unlock(b: any, field: string) {
    if (b) {
      b[field] = !b[field];
    }
    this.cdRef.markForCheck();
  }

  async runTask(action: ActionItem<Series>) {
    switch (action.action) {
      case Action.Scan:
        await this.actionService.scanSeries(this.series);
        break;
      case Action.RefreshMetadata:
        await this.actionService.refreshSeriesMetadata(this.series);
        break;
      case Action.GenerateColorScape:
        await this.actionService.refreshSeriesMetadata(this.series, undefined, false, true);
        break;
      case Action.AnalyzeFiles:
        this.actionService.analyzeFilesForSeries(this.series);
        break;
      case Action.MarkAsRead:
        this.actionService.markSeriesAsRead(this.series);
        break;
      case Action.MarkAsUnread:
        this.actionService.markSeriesAsUnread(this.series);
        break;
      case Action.Delete:
        await this.actionService.deleteSeries(this.series);
        break;
      case Action.Download:
        this.downloadService.download('series', this.series);
        break;
      case Action.Match:
        this.actionService.matchSeries(this.series, _ => {
          this.modal.close({success: true, series: this.series, coverImageUpdate: false, updateExternal: true});
        });
        break;
    }
  }
}
