import { Component, Input, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ReadingList } from 'src/app/_models/reading-list';

@Component({
  selector: 'app-edit-reading-list-modal',
  templateUrl: './edit-reading-list-modal.component.html',
  styleUrls: ['./edit-reading-list-modal.component.scss']
})
export class EditReadingListModalComponent implements OnInit {

  @Input() readingList!: ReadingList;
  reviewGroup!: FormGroup;

  constructor(private ngModal: NgbActiveModal) { }

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
    
    this.ngModal.close(this.readingList);
  }

}
