import { Component, Input, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ReadingList } from 'src/app/_models/reading-list';
import { ReadingListService } from 'src/app/_services/reading-list.service';

@Component({
  selector: 'app-edit-reading-list-modal',
  templateUrl: './edit-reading-list-modal.component.html',
  styleUrls: ['./edit-reading-list-modal.component.scss']
})
export class EditReadingListModalComponent implements OnInit {

  @Input() readingList!: ReadingList;
  reviewGroup!: FormGroup;

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService) { }

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      title: new FormControl(this.readingList.title, [Validators.required]),
      summary: new FormControl(this.readingList.summary, [])
    });
  }

  close() {
    this.ngModal.dismiss(undefined);
  }

  save() {
    if (this.reviewGroup.value.title.trim() === '') return;
    const model = {...this.reviewGroup.value, readingListId: this.readingList.id, promoted: this.readingList.promoted};

    this.readingListService.update(model).subscribe(() => {
      this.readingList.title = model.title;
      this.readingList.summary = model.summary;
      this.ngModal.close(this.readingList);
    });
  }

  togglePromotion() {
    const originalPromotion = this.readingList.promoted;
    this.readingList.promoted = !this.readingList.promoted;
    const model = {readingListId: this.readingList.id, promoted: this.readingList.promoted};
    this.readingListService.update(model).subscribe(res => {
      /* No Operation */
    }, err => {
      this.readingList.promoted = originalPromotion;
    });
  }

}
