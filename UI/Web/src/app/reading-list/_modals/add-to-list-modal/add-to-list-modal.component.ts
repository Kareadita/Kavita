import { Component, OnInit } from '@angular/core';
import { ReadingList } from 'src/app/_models/reading-list';
import { ReadingListService } from 'src/app/_services/reading-list.service';

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
  loading: boolean = false;

  constructor(private readingListService: ReadingListService) { }

  ngOnInit(): void {
    this.loading = true;
    this.readingListService.getReadingLists(false).subscribe(lists => {
      this.lists = lists;
      this.loading = false;
    });
  }

  close() {

  }

  addToList(readingList: ReadingList) {

  }

}
