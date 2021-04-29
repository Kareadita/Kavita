import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { Series } from 'src/app/_models/series';
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


  constructor(public modal: NgbActiveModal,
              private seriesService: SeriesService,
              public utilityService: UtilityService,
              private fb: FormBuilder,
              public imageService: ImageService, 
              private libraryService: LibraryService) { }

  ngOnInit(): void {

    this.libraryService.getLibraryNames().pipe(takeUntil(this.onDestroy)).subscribe(names => {
      this.libraryName = names[this.series.libraryId];
    });

    this.editSeriesForm = this.fb.group({
      id: new FormControl(this.series.id, []),
      summary: new FormControl(this.series.summary, []),
      name: new FormControl(this.series.name, []),
      localizedName: new FormControl(this.series.localizedName, []),
      sortName: new FormControl(this.series.sortName, []),
      rating: new FormControl(this.series.userRating, []),
      genres: new FormControl([''], []),
      author: new FormControl('', []),
      collections: new FormControl([''], []),
      artist: new FormControl('', []),
      coverImageIndex: new FormControl(0, [])
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

  close() {
    this.modal.close({success: true, series: undefined});
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }

  save() {
    // TODO: In future (once locking or metadata implemented), do a converstion to updateSeriesDto
    this.seriesService.updateSeries(this.editSeriesForm.value).subscribe(() => {
      this.modal.close({success: true, series: this.editSeriesForm.value});
    });
  }

}
