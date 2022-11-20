import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { debounceTime, distinctUntilChanged, forkJoin, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { SelectionModel } from 'src/app/typeahead/typeahead.component';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';
import { UploadService } from 'src/app/_services/upload.service';


enum TabID {
  General = 'General',
  CoverImage = 'Cover Image',
  Series = 'Series'
}

@Component({
  selector: 'app-edit-collection-tags',
  templateUrl: './edit-collection-tags.component.html',
  styleUrls: ['./edit-collection-tags.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditCollectionTagsComponent implements OnInit, OnDestroy {

  @Input() tag!: CollectionTag;
  series: Array<Series> = [];
  selections!: SelectionModel<Series>;
  isLoading: boolean = true;

  pagination!: Pagination;
  selectAll: boolean = true;
  libraryNames!: any;
  collectionTagForm!: FormGroup;
  active = TabID.General;
  imageUrls: Array<string> = [];
  selectedCover: string = '';

  private readonly onDestroy = new Subject<void>();

  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }

  get Breakpoint() {
    return Breakpoint;
  }

  get TabID() {
    return TabID;
  }

  constructor(public modal: NgbActiveModal, private seriesService: SeriesService, 
    private collectionService: CollectionTagService, private toastr: ToastrService,
    private confirmSerivce: ConfirmService, private libraryService: LibraryService,
    private imageService: ImageService, private uploadService: UploadService,
    public utilityService: UtilityService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    if (this.pagination == undefined) {
      this.pagination = {totalPages: 1, totalItems: 200, itemsPerPage: 200, currentPage: 0};
    }
    this.collectionTagForm = new FormGroup({
      title: new FormControl(this.tag.title, { nonNullable: true, validators: [Validators.required] }),
      summary: new FormControl(this.tag.summary, { nonNullable: true, validators: [] }),
      coverImageLocked: new FormControl(this.tag.coverImageLocked, { nonNullable: true, validators: [] }),
      coverImageIndex: new FormControl(0, { nonNullable: true, validators: [] }),
      promoted: new FormControl(this.tag.promoted, { nonNullable: true, validators: [] }),
    });

    this.collectionTagForm.get('title')?.valueChanges.pipe(
      debounceTime(100), 
      distinctUntilChanged(),
      switchMap(name => this.collectionService.tagNameExists(name)),
      tap(exists => {
        const isExistingName = this.collectionTagForm.get('title')?.value === this.tag.title;
        if (!exists || isExistingName) {
          this.collectionTagForm.get('title')?.setErrors(null);
        } else {
          this.collectionTagForm.get('title')?.setErrors({duplicateName: true})  
        }
        this.cdRef.markForCheck();
      }),
      takeUntil(this.onDestroy)
      ).subscribe();

    this.imageUrls.push(this.imageService.randomize(this.imageService.getCollectionCoverImage(this.tag.id)));
    this.loadSeries();
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  onPageChange(pageNum: number) {
    this.pagination.currentPage = pageNum;
    this.loadSeries();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.series.forEach(s => this.selections.toggle(s, this.selectAll));
    this.cdRef.markForCheck();
  }

  loadSeries() {
    forkJoin([
      this.seriesService.getSeriesForTag(this.tag.id, this.pagination.currentPage, this.pagination.itemsPerPage),
      this.libraryService.getLibraryNames()
    ]).subscribe(results => {
      const series = results[0];
      this.pagination = series.pagination;
      this.series = series.result;

      this.imageUrls.push(...this.series.map(s => this.imageService.getSeriesCoverImage(s.id)));
      this.selections = new SelectionModel<Series>(true, this.series);
      this.isLoading = false;

      this.libraryNames = results[1];
      this.cdRef.markForCheck();
    });
  }

  handleSelection(item: Series) {
    this.selections.toggle(item);
    const numberOfSelected = this.selections.selected().length;
    if (numberOfSelected == 0) {
      this.selectAll = false;
    } else if (numberOfSelected == this.series.length) {
      this.selectAll = true;
    }
    this.cdRef.markForCheck();
  }

  libraryName(libraryId: number) {
    return this.libraryNames[libraryId];
  }

  close() {
    this.modal.dismiss();
  }

  async save() {
    const selectedIndex = this.collectionTagForm.get('coverImageIndex')?.value || 0;
    const unselectedIds = this.selections.unselected().map(s => s.id);
    const tag: CollectionTag = {...this.tag};
    tag.summary = this.collectionTagForm.get('summary')?.value;
    tag.coverImageLocked = this.collectionTagForm.get('coverImageLocked')?.value;
    tag.promoted  = this.collectionTagForm.get('promoted')?.value;
    
    if (unselectedIds.length == this.series.length && !await this.confirmSerivce.confirm('Warning! No series are selected, saving will delete the tag. Are you sure you want to continue?')) {
      return;
    }

    const apis = [this.collectionService.updateTag(tag),
      this.collectionService.updateSeriesForTag(tag, this.selections.unselected().map(s => s.id))
    ];
    
    if (selectedIndex > 0) {
      apis.push(this.uploadService.updateCollectionCoverImage(this.tag.id, this.selectedCover));
    }
  
    forkJoin(apis).subscribe(() => {
      this.modal.close({success: true, coverImageUpdated: selectedIndex > 0});
      this.toastr.success('Tag updated');
    });
  }

  updateSelectedIndex(index: number) {
    this.collectionTagForm.patchValue({
      coverImageIndex: index
    });
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.collectionTagForm.patchValue({
      coverImageLocked: false
    });
    this.cdRef.markForCheck();
  }

}
