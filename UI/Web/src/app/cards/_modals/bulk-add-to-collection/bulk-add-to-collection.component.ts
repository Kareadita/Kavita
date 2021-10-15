import { Component, ElementRef, Input, OnInit, ViewChild } from '@angular/core';
import { FormGroup, FormControl } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { ReadingList } from 'src/app/_models/reading-list';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';

@Component({
  selector: 'app-bulk-add-to-collection',
  templateUrl: './bulk-add-to-collection.component.html',
  styleUrls: ['./bulk-add-to-collection.component.scss']
})
export class BulkAddToCollectionComponent implements OnInit {

  @Input() title!: string;
  /**
   * Series Ids to add to Collection Tag
   */
  @Input() seriesIds: Array<number> = [];

  /**
   * All existing collections sorted by recent use date
   */
  lists: Array<CollectionTag> = [];
  loading: boolean = false;
  listForm: FormGroup = new FormGroup({});

  @ViewChild('title') inputElem!: ElementRef<HTMLInputElement>;


  constructor(private modal: NgbActiveModal, private collectionService: CollectionTagService, private toastr: ToastrService) { }

  ngOnInit(): void {

    this.listForm.addControl('title', new FormControl(this.title, []));
    this.listForm.addControl('filterQuery', new FormControl('', []));
    
    this.loading = true;
    this.collectionService.allTags().subscribe(tags => {
      this.lists = tags;
      this.loading = false;
    });
  }

  ngAfterViewInit() {
    // Shift focus to input
    if (this.inputElem) {
      this.inputElem.nativeElement.select();
    }
  }

  close() {
    this.modal.close();
  }

  create() {
    const tagName = this.listForm.value.title;
    this.collectionService.addByMultiple(0, this.seriesIds, tagName).subscribe(() => {
      this.toastr.success('Series added to ' + tagName + ' collection');
      this.modal.close();
    });
  }

  addToCollection(tag: CollectionTag) {
    if (this.seriesIds.length === 0) return;

    this.collectionService.addByMultiple(tag.id, this.seriesIds, '').subscribe(() => {
      this.toastr.success('Series added to ' + tag.title + ' collection');
      this.modal.close();
    });
    
  }

  filterList = (listItem: ReadingList) => {
    return listItem.title.toLowerCase().indexOf((this.listForm.value.filterQuery || '').toLowerCase()) >= 0;
  }

}
