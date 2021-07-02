import { Component, Input, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { forkJoin } from 'rxjs';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { SelectionModel } from 'src/app/typeahead/typeahead.component';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { LibraryService } from 'src/app/_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-edit-collection-tags',
  templateUrl: './edit-collection-tags.component.html',
  styleUrls: ['./edit-collection-tags.component.scss']
})
export class EditCollectionTagsComponent implements OnInit {

  @Input() tag!: CollectionTag;
  series: Array<Series> = [];
  selections!: SelectionModel<Series>;
  isLoading: boolean = true;

  pagination!: Pagination;
  selectAll: boolean = true;
  libraryNames!: any;
  collectionTagForm!: FormGroup;


  constructor(public modal: NgbActiveModal, private seriesService: SeriesService, 
    private collectionService: CollectionTagService, private toastr: ToastrService,
    private confirmSerivce: ConfirmService, private libraryService: LibraryService) { }

  ngOnInit(): void {
    if (this.pagination == undefined) {
      this.pagination = {totalPages: 1, totalItems: 200, itemsPerPage: 200, currentPage: 0};
    }
    this.collectionTagForm = new FormGroup({
      summary: new FormControl(this.tag.summary, []),
    });
    this.loadSeries();
  }

  onPageChange(pageNum: number) {
    this.pagination.currentPage = pageNum;
    this.loadSeries();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.series.forEach(s => this.selections.toggle(s, this.selectAll));
  }

  loadSeries() {
    forkJoin([
      this.seriesService.getSeriesForTag(this.tag.id, this.pagination.currentPage, this.pagination.itemsPerPage),
      this.libraryService.getLibraryNames()
    ]).subscribe(results => {
      const series = results[0];

      this.pagination = series.pagination;
      this.series = series.result;
      this.selections = new SelectionModel<Series>(true, this.series);
      this.isLoading = false;

      this.libraryNames = results[1];
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
  }

  togglePromotion() {
    const originalPromotion = this.tag.promoted;
    this.tag.promoted = !this.tag.promoted;
    this.collectionService.updateTag(this.tag).subscribe(res => {
      this.toastr.success('Tag updated successfully');
    }, err => {
      this.tag.promoted = originalPromotion;
    });
  }

  libraryName(libraryId: number) {
    return this.libraryNames[libraryId];
  }

  close() {
    this.modal.close(false);
  }

  async save() {
    const unselectedIds = this.selections.unselected().map(s => s.id);
    const tag: CollectionTag = {...this.tag};
    tag.summary = this.collectionTagForm.get('summary')?.value;
    
    if (unselectedIds.length == this.series.length && !await this.confirmSerivce.confirm('Warning! No series are selected, saving will delete the tag. Are you sure you want to continue?')) {
      return;
    }

    this.collectionService.updateSeriesForTag(tag, this.selections.unselected().map(s => s.id)).subscribe(() => {
      this.toastr.success('Tag updated');
      this.modal.close(true);
    });
  }

  get someSelected() {
    const selected = this.selections.selected();
    return (selected.length != this.series.length && selected.length != 0);
  }

}
