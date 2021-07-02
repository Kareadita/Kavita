import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/typeahead-settings';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/series-metadata';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';

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
  tabs = ['General', 'Fix Match', 'Cover Image', 'Info'];
  active = this.tabs[0];
  editSeriesForm!: FormGroup;
  libraryName: string | undefined = undefined;
  private readonly onDestroy = new Subject<void>();

  settings: TypeaheadSettings<CollectionTag> = new TypeaheadSettings();
  tags: CollectionTag[] = [];
  metadata!: SeriesMetadata;

  constructor(public modal: NgbActiveModal,
              private seriesService: SeriesService,
              public utilityService: UtilityService,
              private fb: FormBuilder,
              public imageService: ImageService, 
              private libraryService: LibraryService,
              private collectionService: CollectionTagService) { }

  ngOnInit(): void {

    this.libraryService.getLibraryNames().pipe(takeUntil(this.onDestroy)).subscribe(names => {
      this.libraryName = names[this.series.libraryId];
    });

    this.setupTypeaheadSettings();

    

    this.editSeriesForm = this.fb.group({
      id: new FormControl(this.series.id, []),
      summary: new FormControl(this.series.summary, []),
      name: new FormControl(this.series.name, []),
      localizedName: new FormControl(this.series.localizedName, []),
      sortName: new FormControl(this.series.sortName, []),
      rating: new FormControl(this.series.userRating, []),

      genres: new FormControl('', []),
      author: new FormControl('', []),
      artist: new FormControl('', []),

      coverImageIndex: new FormControl(0, [])
    });

    this.seriesService.getMetadata(this.series.id).subscribe(metadata => {
      if (metadata) {
        this.metadata = metadata;
        this.settings.savedData = metadata.tags;
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
  }

  setupTypeaheadSettings() {
    this.settings.minCharacters = 0;
    this.settings.multiple = true;
    this.settings.id = 'collections';
    this.settings.unique = true;
    this.settings.addIfNonExisting = true;
    this.settings.fetchFn = (filter: string) => this.fetchCollectionTags(filter);
    this.settings.addTransformFn = ((title: string) => {
      return {id: 0, title: title, promoted: false, coverImage: '', summary: '' };
    });
    this.settings.compareFn = (options: CollectionTag[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
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
    // TODO: In future (once locking or metadata implemented), do a converstion to updateSeriesDto

    forkJoin([
      this.seriesService.updateSeries(this.editSeriesForm.value),
      this.seriesService.updateMetadata(this.metadata, this.tags)
    ]).subscribe(results => {
      this.modal.close({success: true, series: this.editSeriesForm.value});
    });
  }

  updateCollections(tags: CollectionTag[]) {
    this.tags = tags;
  }

}
