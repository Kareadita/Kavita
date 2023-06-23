import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin, Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/_models/typeahead-settings';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Genre } from 'src/app/_models/metadata/genre';
import { AgeRatingDto } from 'src/app/_models/metadata/age-rating-dto';
import { Language } from 'src/app/_models/metadata/language';
import { PublicationStatusDto } from 'src/app/_models/metadata/publication-status-dto';
import { Person, PersonRole } from 'src/app/_models/metadata/person';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/metadata/series-metadata';
import { Tag } from 'src/app/_models/tag';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { SeriesService } from 'src/app/_services/series.service';
import { UploadService } from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

enum TabID {
  General = 0,
  Metadata = 1,
  People = 2,
  WebLinks = 3,
  CoverImage = 4,
  Related = 5,
  Info = 6,
}

@Component({
  selector: 'app-edit-series-modal',
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesModalComponent implements OnInit {

  @Input({required: true}) series!: Series;
  seriesVolumes: any[] = [];
  isLoadingVolumes = false;
  /**
   * A copy of the series from init. This is used to compare values for name fields to see if lock was modified
   */
  initSeries!: Series;

  volumeCollapsed: any = {};
  tabs = ['General', 'Metadata', 'People', 'Web Links', 'Cover Image', 'Related', 'Info'];
  active = this.tabs[0];
  editSeriesForm!: FormGroup;
  libraryName: string | undefined = undefined;
  size: number = 0;
  private readonly destroyRef = inject(DestroyRef);

  // Typeaheads
  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<Language> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  collectionTagSettings: TypeaheadSettings<CollectionTag> = new TypeaheadSettings();
  genreSettings: TypeaheadSettings<Genre> = new TypeaheadSettings();

  collectionTags: CollectionTag[] = [];
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

  saveNestedComponents: EventEmitter<void> = new EventEmitter();

  get Breakpoint(): typeof Breakpoint {
    return Breakpoint;
  }

  get PersonRole() {
    return PersonRole;
  }

  get TabID(): typeof TabID {
    return TabID;
  }

  get WebLinks() {
    return this.metadata?.webLinks.split(',') || [''];
  }

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings[role];
  }

  constructor(public modal: NgbActiveModal,
              private seriesService: SeriesService,
              public utilityService: UtilityService,
              private fb: FormBuilder,
              public imageService: ImageService,
              private libraryService: LibraryService,
              private collectionService: CollectionTagService,
              private uploadService: UploadService,
              private metadataService: MetadataService,
              private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.imageUrls.push(this.imageService.getSeriesCoverImage(this.series.id));

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

        this.WebLinks.forEach((link, index) => {
          this.editSeriesForm.addControl('link' + index, new FormControl(link, []));
        });

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
        vol.volumeFiles = vol.chapters?.sort(this.utilityService.sortChapters).map((c: Chapter) => c.files.map((f: any) => {
          f.chapter = c.number;
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
      this.setupCollectionTagsSettings(),
      this.setupTagSettings(),
      this.setupGenreTypeahead(),
      this.setupPersonTypeahead(),
      this.setupLanguageTypeahead()
    ]).subscribe(results => {
      this.collectionTags = this.metadata.collectionTags;
      this.cdRef.markForCheck();
    });
  }

  setupCollectionTagsSettings() {
    this.collectionTagSettings.minCharacters = 0;
    this.collectionTagSettings.multiple = true;
    this.collectionTagSettings.id = 'collections';
    this.collectionTagSettings.unique = true;
    this.collectionTagSettings.addIfNonExisting = true;
    this.collectionTagSettings.fetchFn = (filter: string) => this.fetchCollectionTags(filter).pipe(map(items => this.collectionTagSettings.compareFn(items, filter)));
    this.collectionTagSettings.addTransformFn = ((title: string) => {
      return {id: 0, title: title, promoted: false, coverImage: '', summary: '', coverImageLocked: false };
    });
    this.collectionTagSettings.compareFn = (options: CollectionTag[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    this.collectionTagSettings.compareFnForAdd = (options: CollectionTag[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    }
    this.collectionTagSettings.selectionCompareFn = (a: CollectionTag, b: CollectionTag) => {
      return a.title === b.title;
    }

    if (this.metadata.collectionTags) {
      this.collectionTagSettings.savedData = this.metadata.collectionTags;
    }

    return of(true);
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
      return a.id == b.id;
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
      return a.title == b.title;
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
        const persetIds = presetField.map(p => p.id);
        personSettings.savedData = people.filter(person => persetIds.includes(person.id));
        this.peopleSettings[role] = personSettings;
        this.updatePerson(personSettings.savedData as Person[], role);
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

    if (this.metadata.language === undefined || this.metadata.language === null || this.metadata.language === '') {
      this.metadata.language = 'en';
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
      this.updateFromPreset('translator', this.metadata.translators, PersonRole.Translator)
    ]).pipe(map(results => {
      return of(true);
    }));
  }

  fetchPeople(role: PersonRole, filter: string) {
    return this.metadataService.getAllPeople().pipe(map(people => {
      return people.filter(p => p.role == role && this.utilityService.filter(p.name, filter));
    }));
  }

  createBlankPersonSettings(id: string, role: PersonRole) {
    var personSettings = new TypeaheadSettings<Person>();
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
      return a.name == b.name && a.role == b.role;
    }
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(role, filter).pipe(map(items => personSettings.compareFn(items, filter)));
    };

    personSettings.addTransformFn = ((title: string) => {
      return {id: 0, name: title, role: role };
    });

    return personSettings;
  }

  close() {
    this.modal.close({success: false, series: undefined, coverImageUpdate: this.coverImageReset});
  }

  fetchCollectionTags(filter: string = '') {
    return this.collectionService.search(filter);
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }

  save() {
    const model = this.editSeriesForm.value;
    const selectedIndex = this.editSeriesForm.get('coverImageIndex')?.value || 0;
    this.metadata.webLinks = Object.keys(this.editSeriesForm.controls)
      .filter(key => key.startsWith('link'))
      .map(key => this.editSeriesForm.get(key)?.value.replace(',', '%2C'))
      .filter(v => v !== null && v !== '')
      .join(',');



    const apis = [
      this.seriesService.updateMetadata(this.metadata, this.collectionTags)
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


    if (selectedIndex > 0 && this.selectedCover !== '') {
      apis.push(this.uploadService.updateSeriesCoverImage(model.id, this.selectedCover));
    }

    this.saveNestedComponents.emit();

    forkJoin(apis).subscribe(results => {
      this.modal.close({success: true, series: model, coverImageUpdate: selectedIndex > 0 || this.coverImageReset});
    });
  }

  addWebLink() {
    this.metadata.webLinks += ',';
    this.editSeriesForm.addControl('link' + (this.WebLinks.length - 1), new FormControl('', []));
    this.cdRef.markForCheck();
  }

  removeWebLink(index: number) {
    const tokens = this.metadata.webLinks.split(',');
    const tokenToRemove = tokens[index];

    this.metadata.webLinks = tokens.filter(t => t != tokenToRemove).join(',');
    this.editSeriesForm.removeControl('link' + index, {emitEvent: true});
    this.cdRef.markForCheck();
  }

  updateCollections(tags: CollectionTag[]) {
    this.collectionTags = tags;
    this.cdRef.markForCheck();
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

  updateLanguage(language: Array<Language>) {
    if (language.length === 0) {
      this.metadata.language = '';
      return;
    }
    this.metadata.language = language[0].isoCode;
    this.cdRef.markForCheck();
  }

  updatePerson(persons: Person[], role: PersonRole) {
    switch (role) {
      case PersonRole.CoverArtist:
        this.metadata.coverArtists = persons;
        break;
      case PersonRole.Character:
        this.metadata.characters = persons;
        break;
      case PersonRole.Colorist:
        this.metadata.colorists = persons;
        break;
      case PersonRole.Editor:
        this.metadata.editors = persons;
        break;
      case PersonRole.Inker:
        this.metadata.inkers = persons;
        break;
      case PersonRole.Letterer:
        this.metadata.letterers = persons;
        break;
      case PersonRole.Penciller:
        this.metadata.pencillers = persons;
        break;
      case PersonRole.Publisher:
        this.metadata.publishers = persons;
        break;
      case PersonRole.Writer:
        this.metadata.writers = persons;
        break;
      case PersonRole.Translator:
        this.metadata.translators = persons;
    }
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

}
