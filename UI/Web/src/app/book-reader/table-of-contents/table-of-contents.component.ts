import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { BookService } from '../book.service';
import { BookChapterItem } from '../_models/book-chapter-item';

@Component({
  selector: 'app-table-of-contents',
  templateUrl: './table-of-contents.component.html',
  styleUrls: ['./table-of-contents.component.scss']
})
export class TableOfContentsComponent implements OnInit {

  @Input() chapterId!: number;
  @Input() pageNum!: number;
  @Input() currentPageAnchor!: string;

  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();


  chapters: Array<BookChapterItem> = [];

  constructor(private bookService: BookService) {}

  ngOnInit(): void {
    this.bookService.getBookChapters(this.chapterId).subscribe(chapters => {
      this.chapters = chapters;
    });
  }

  cleanIdSelector(id: string) {
    const tokens = id.split('/');
    if (tokens.length > 0) {
      return tokens[0];
    }
    return id;
  }

  loadChapterPage(pageNum: number, part: string) {
    this.loadChapter.emit({pageNum, part});
    // this.setPageNum(pageNum);
    // this.loadPage('id("' + part + '")');
  }

}
