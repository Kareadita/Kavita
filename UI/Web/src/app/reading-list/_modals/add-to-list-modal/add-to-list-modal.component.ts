import { AfterViewInit, Component, ElementRef, Input, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ReadingList } from 'src/app/_models/reading-list';
import { ReadingListService } from 'src/app/_services/reading-list.service';

export enum ADD_FLOW {
  Series = 0,
  Volume = 1,
  Chapter = 2
}

@Component({
  selector: 'app-add-to-list-modal',
  templateUrl: './add-to-list-modal.component.html',
  styleUrls: ['./add-to-list-modal.component.scss']
})
export class AddToListModalComponent implements OnInit, AfterViewInit {

  @Input() title!: string;
  @Input() seriesId?: number;
  @Input() volumeId?: number;
  @Input() chapterId?: number;
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


  constructor(private modal: NgbActiveModal, private readingListService: ReadingListService) { }

  ngOnInit(): void {

    this.listForm.addControl('title', new FormControl(this.title, []));
    
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
    if (this.type === ADD_FLOW.Series && this.seriesId != undefined) {
      this.readingListService.updateBySeries(readingList.id, this.seriesId).subscribe(() => {
        this.modal.close();
      });
    }
    
  }

}
