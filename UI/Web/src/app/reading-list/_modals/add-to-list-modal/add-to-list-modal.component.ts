import { Component, OnInit } from '@angular/core';

@Component({
  selector: 'app-add-to-list-modal',
  templateUrl: './add-to-list-modal.component.html',
  styleUrls: ['./add-to-list-modal.component.scss']
})
export class AddToListModalComponent implements OnInit {

  /**
   * All existing reading lists sorted by recent use date
   */
  lists: Array<any> = [];

  constructor() { }

  ngOnInit(): void {
  }

  close() {

  }

}
