import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin, of, Subject } from 'rxjs';
import { map, takeUntil } from 'rxjs/operators';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/typeahead-settings';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { AgeRatingDto } from 'src/app/_models/metadata/age-rating-dto';
import { Language } from 'src/app/_models/metadata/language';
import { PublicationStatusDto } from 'src/app/_models/metadata/publication-status-dto';
import { Person } from 'src/app/_models/person';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/series-metadata';
import { Tag } from 'src/app/_models/tag';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { SeriesService } from 'src/app/_services/series.service';
import { UploadService } from 'src/app/_services/upload.service';

@Component({
  selector: 'app-edit-series-modal',
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss']
})
export class EditSeriesModalComponent implements OnInit, OnDestroy {

  @Input() series!: Series;
  seriesVolumes: any[] = [];
  isLoadingVolumes = false;

  isCollapsed = true;
  volumeCollapsed: any = {};
  tabs = ['General', 'Metadata', 'People', 'Cover Image', 'Info'];
  active = this.tabs[0];
  editSeriesForm!: FormGroup;
  libraryName: string | undefined = undefined;
  private readonly onDestroy = new Subject<void>();


  // Typeaheads
  ageRatingSettings: TypeaheadSettings<AgeRatingDto> = new TypeaheadSettings();
  publicationStatusSettings: TypeaheadSettings<PublicationStatusDto> = new TypeaheadSettings();
  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<Language> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  collectionTagSettings: TypeaheadSettings<CollectionTag> = new TypeaheadSettings();
  tagSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();


  collectionTags: CollectionTag[] = [];
  tags: Tag[] = [];
  metadata!: SeriesMetadata;
  imageUrls: Array<string> = [];
  /**
   * Selected Cover for uploading
   */
  selectedCover: string = '';

  ageRatings: Array<AgeRatingDto> = [];
  publicationStatuses: Array<PublicationStatusDto> = [];

  get Breakpoint(): typeof Breakpoint {
    return Breakpoint;
  }

  constructor(public modal: NgbActiveModal,
              private seriesService: SeriesService,
              public utilityService: UtilityService,
              private fb: FormBuilder,
              public imageService: ImageService, 
              private libraryService: LibraryService,
              private collectionService: CollectionTagService,
              private uploadService: UploadService,
              private metadataService: MetadataService) { }

  ngOnInit(): void {
    this.imageUrls.push(this.imageService.getSeriesCoverImage(this.series.id));

    this.libraryService.getLibraryNames().pipe(takeUntil(this.onDestroy)).subscribe(names => {
      this.libraryName = names[this.series.libraryId];
    });


    this.editSeriesForm = this.fb.group({
      id: new FormControl(this.series.id, []),
      summary: new FormControl('', []), 
      name: new FormControl(this.series.name, []),
      localizedName: new FormControl(this.series.localizedName, []),
      sortName: new FormControl(this.series.sortName, []),
      rating: new FormControl(this.series.userRating, []),

      genres: new FormControl('', []),
      author: new FormControl('', []),
      artist: new FormControl('', []),

      coverImageIndex: new FormControl(0, []),
      coverImageLocked: new FormControl(this.series.coverImageLocked, []),

      ageRating: new FormControl('', []),
      publicationStatus: new FormControl('', []),
    });

    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
    });
    
    this.metadataService.getAllPublicationStatus().subscribe(statuses => {
      this.publicationStatuses = statuses;
    })

    this.seriesService.getMetadata(this.series.id).subscribe(metadata => {
      if (metadata) {
        this.metadata = metadata;
        this.setupTypeaheads();

        
        // this.collectionTagSettings.savedData = metadata.collectionTags;
        // this.collectionTags = metadata.collectionTags;
        this.editSeriesForm.get('summary')?.setValue(this.metadata.summary);
        this.editSeriesForm.get('ageRating')?.setValue(this.metadata.ageRating);
        this.editSeriesForm.get('publicationStatus')?.setValue(this.metadata.publicationStatus);


      }
    });

    this.isLoadingVolumes = true;
    this.seriesService.getVolumes(this.series.id).subscribe(volumes => {
      this.seriesVolumes = volumes;
      this.isLoadingVolumes = false;

      volumes.forEach(v => {
        this.volumeCollapsed[v.name] = true;
      });
      this.seriesVolumes.forEach(vol => {
        vol.volumeFiles = vol.chapters?.sort(this.utilityService.sortChapters).map((c: Chapter) => c.files.map((f: any) => {
          f.chapter = c.number;
          return f;
        })).flat();
      });
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  setupTypeaheads() {
    forkJoin([
      this.setupCollectionTagsSettings(),
      this.setupTagSettings(),
      // this.setupLanguageSettings(),
      // this.setupGenreTypeahead(),
      // this.setupPersonTypeahead(),
    ]).subscribe(results => {
      //this.resetTypeaheads.next(true);
      //this.collectionTagSettings.savedData = this.metadata.collectionTags;
      this.collectionTags = this.metadata.collectionTags;
      this.editSeriesForm.get('summary')?.setValue(this.metadata.summary);
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
    this.collectionTagSettings.singleCompareFn = (a: CollectionTag, b: CollectionTag) => {
      return a.id == b.id;
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
    this.tagsSettings.addIfNonExisting = true;


    this.tagsSettings.compareFn = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    this.tagsSettings.fetchFn = (filter: string) => this.metadataService.getAllTags()
      .pipe(map(items => this.tagsSettings.compareFn(items, filter))); 
    
    this.tagsSettings.addTransformFn = ((title: string) => {
      return {id: 0, title: title };
    });
    this.tagsSettings.singleCompareFn = (a: Tag, b: Tag) => {
      return a.id == b.id;
    }

    if (this.metadata.tags) {
      this.tagsSettings.savedData = this.metadata.tags;
    }
    return of(true);
  }

  close() {
    this.modal.close({success: false, series: undefined});
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
    const apis = [
      this.seriesService.updateSeries(model),
      this.seriesService.updateMetadata(this.metadata, this.collectionTags)
    ];


    if (selectedIndex > 0) {
      apis.push(this.uploadService.updateSeriesCoverImage(model.id, this.selectedCover));
    }

    forkJoin(apis).subscribe(results => {
      this.modal.close({success: true, series: model, coverImageUpdate: selectedIndex > 0});
    });
  }

  updateCollections(tags: CollectionTag[]) {
    this.collectionTags = tags;
  }

  updateTags(tags: Tag[]) {
    this.tags = tags;
  }

  updateSelectedIndex(index: number) {
    this.editSeriesForm.patchValue({
      coverImageIndex: index
    });
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
  }

  handleReset() {
    this.editSeriesForm.patchValue({
      coverImageLocked: false
    });
  }

}
