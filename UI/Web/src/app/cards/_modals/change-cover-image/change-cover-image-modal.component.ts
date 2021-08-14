import { Component, Input, OnInit } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';

@Component({
  selector: 'app-change-cover-image-modal',
  templateUrl: './change-cover-image-modal.component.html',
  styleUrls: ['./change-cover-image-modal.component.scss']
})
export class ChangeCoverImageModalComponent implements OnInit {

  @Input() chapter!: Chapter;
  @Input() title: string = '';
  
  constructor() { }

  ngOnInit(): void {
  }

  cancel() {

  }
  save() {

  }

}
