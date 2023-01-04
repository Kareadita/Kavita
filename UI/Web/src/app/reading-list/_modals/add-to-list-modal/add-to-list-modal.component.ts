import { AfterViewInit, Component, ElementRef, Input, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { ReadingList } from 'src/app/_models/reading-list';
import { ReadingListService } from 'src/app/_services/reading-list.service';

export enum ADD_FLOW {
  Series = 0,
  Volume = 1,
  Chapter = 2,
  Multiple = 3,
  Multiple_Series
}

@Component({
  selector: 'app-add-to-list-modal',
  templateUrl: './add-to-list-modal.component.html',
  styleUrls: ['./add-to-list-modal.component.scss']
})
export class AddToListModalComponent implements OnInit, AfterViewInit {

  @Input() title!: string;
  /**
   * Only used in Series flow
   */
  @Input() seriesId?: number;
  /**
   * Only used in Volume flow
   */
  @Input() volumeId?: number;
  /**
   * Only used in Chapter flow
   */
  @Input() chapterId?: number;
  /**
   * Only used in Multiple flow
   */
  @Input() volumeIds?: Array<number>;
  /**
   * Only used in Multiple flow
   */
  @Input() chapterIds?: Array<number>;
  /**
   * Only used in Multiple_Series flow
   */
  @Input() seriesIds?: Array<number>;

  /**
   * Determines which Input is required and which API is used to associate to the Reading List
   */
  @Input() type!: ADD_FLOW;

  /**
   * All existing reading lists sorted by recent use date
   */
  lists: Array<any> = [];
  loading: boolean = false;
  listForm: FormGroup = new FormGroup({});

  @ViewChild('title') inputElem!: ElementRef<HTMLInputElement>;

  filterList = (listItem: ReadingList) => {
    return listItem.title.toLowerCase().indexOf((this.listForm.value.filterQuery || '').toLowerCase()) >= 0;
  }


  constructor(private modal: NgbActiveModal, private readingListService: ReadingListService, private toastr: ToastrService) { }

  ngOnInit(): void {

    this.listForm.addControl('title', new FormControl(this.title, []));
    this.listForm.addControl('filterQuery', new FormControl('', []));
    
    this.loading = true;
    this.readingListService.getReadingLists(false).subscribe(lists => {
      this.lists = lists.result;
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
    this.readingListService.createList(this.listForm.value.title).subscribe(list => {
      this.addToList(list);
    });
  }

  addToList(readingList: ReadingList) {

    if (this.type === ADD_FLOW.Multiple_Series && this.seriesIds !== undefined) {
      this.readingListService.updateByMultipleSeries(readingList.id, this.seriesIds).subscribe(() => {
        this.toastr.success('Series added to reading list');
        this.modal.close();
      });
    }

    if (this.seriesId === undefined) return;

    if (this.type === ADD_FLOW.Series && this.seriesId !== undefined) {
      this.readingListService.updateBySeries(readingList.id, this.seriesId).subscribe(() => {
        this.toastr.success('Series added to reading list');
        this.modal.close();
      });
    } else if (this.type === ADD_FLOW.Volume && this.volumeId !== undefined) {
      this.readingListService.updateByVolume(readingList.id, this.seriesId, this.volumeId).subscribe(() => {
        this.toastr.success('Volumes added to reading list');
        this.modal.close();
      });
    } else if (this.type === ADD_FLOW.Chapter && this.chapterId !== undefined) {
      this.readingListService.updateByChapter(readingList.id, this.seriesId, this.chapterId).subscribe(() => {
        this.toastr.success('Chapter added to reading list');
        this.modal.close();
      });
    } else if (this.type === ADD_FLOW.Multiple && this.volumeIds !== undefined && this.chapterIds !== undefined) {
      this.readingListService.updateByMultiple(readingList.id, this.seriesId, this.volumeIds, this.chapterIds).subscribe(() => {
        this.toastr.success('Chapters and Volumes added to reading list');
        this.modal.close();
      });
    }
    
  }
}
