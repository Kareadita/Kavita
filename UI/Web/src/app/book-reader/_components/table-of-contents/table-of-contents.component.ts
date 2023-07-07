import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnDestroy, Output } from '@angular/core';
import { Subject } from 'rxjs';
import { BookChapterItem } from '../../_models/book-chapter-item';
import { NgIf, NgFor } from '@angular/common';

@Component({
    selector: 'app-table-of-contents',
    templateUrl: './table-of-contents.component.html',
    styleUrls: ['./table-of-contents.component.scss'],
    changeDetection: ChangeDetectionStrategy.Default,
    standalone: true,
    imports: [NgIf, NgFor]
})
export class TableOfContentsComponent implements OnDestroy {

  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNum!: number;
  @Input({required: true}) currentPageAnchor!: string;
  @Input() chapters:Array<BookChapterItem> = [];

  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();

  private onDestroy: Subject<void> = new Subject();

  pageAnchors: {[n: string]: number } = {};

  constructor() {}

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
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
  }
}
