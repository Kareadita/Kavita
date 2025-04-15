import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';
import {BookChapterItem} from '../../_models/book-chapter-item';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-table-of-contents',
  templateUrl: './table-of-contents.component.html',
  styleUrls: ['./table-of-contents.component.scss'],
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.Default,
})
export class TableOfContentsComponent implements OnChanges {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNum!: number;
  @Input({required: true}) currentPageAnchor!: string;
  @Input() chapters:Array<BookChapterItem> = [];

  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();

  ngOnChanges(changes: SimpleChanges) {
    console.log('Current Page: ', this.pageNum, this.currentPageAnchor);
    this.cdRef.markForCheck();

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
