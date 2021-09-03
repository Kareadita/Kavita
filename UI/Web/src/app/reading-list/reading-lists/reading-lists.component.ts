import { Component, OnInit } from '@angular/core';
import { take } from 'rxjs/operators';
import { Pagination } from 'src/app/_models/pagination';
import { ReadingList } from 'src/app/_models/reading-list';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';

@Component({
  selector: 'app-reading-lists',
  templateUrl: './reading-lists.component.html',
  styleUrls: ['./reading-lists.component.scss']
})
export class ReadingListsComponent implements OnInit {

  lists: ReadingList[] = [];
  loadingLists = false;
  pagination!: Pagination;
  actions: ActionItem<ReadingList>[] = [];

  constructor(private readingListService: ReadingListService) { }

  ngOnInit(): void {
    this.loadPage();
  }

  getPage() {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get('page');
  }

  loadPage() {
    const page = this.getPage();
    if (page != null) {
      this.pagination.currentPage = parseInt(page, 10);
    }
    this.loadingLists = true;

    this.readingListService.getReadingLists(true, this.pagination?.currentPage, this.pagination?.itemsPerPage).pipe(take(1)).subscribe(readingLists => {
      this.lists = readingLists.result;
      this.pagination = readingLists.pagination;
      this.loadingLists = false;
      window.scrollTo(0, 0);
    });
  }

  onPageChange(pagination: Pagination) {
    window.history.replaceState(window.location.href, '', window.location.href.split('?')[0] + '?page=' + this.pagination.currentPage);
    this.loadPage();
  }

}
