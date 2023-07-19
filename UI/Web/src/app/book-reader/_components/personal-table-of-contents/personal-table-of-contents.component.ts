import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef, EventEmitter,
  Inject,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {CommonModule, DOCUMENT} from '@angular/common';
import {ReaderService} from "../../../_services/reader.service";
import {PersonalToC} from "../../../_models/readers/personal-toc";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

export interface PersonalToCEvent {
  pageNum: number;
  scrollPart: string | undefined;
}

@Component({
  selector: 'app-personal-table-of-contents',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './personal-table-of-contents.component.html',
  styleUrls: ['./personal-table-of-contents.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonalTableOfContentsComponent implements OnInit {

  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNum: number = 0;
  @Input({required: true}) tocRefresh!: EventEmitter<void>;
  @Output() loadChapter: EventEmitter<PersonalToCEvent> = new EventEmitter();

  private readonly readerService = inject(ReaderService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);


  bookmarks: {[key: number]: Array<PersonalToC>} = [];

  get Pages() {
    return Object.keys(this.bookmarks).map(p => parseInt(p, 10));
  }

  constructor(@Inject(DOCUMENT) private document: Document) {}

  ngOnInit() {

    this.tocRefresh.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.load();
    });

    this.load();
  }

  load() {
    this.readerService.getPersonalToC(this.chapterId).subscribe(res => {
      res.forEach(t => {
        if (!this.bookmarks.hasOwnProperty(t.pageNumber)) {
          this.bookmarks[t.pageNumber] = [];
        }
        this.bookmarks[t.pageNumber].push(t);
      })
      this.cdRef.markForCheck();
    });
  }

  loadChapterPage(pageNum: number, scrollPart: string | undefined) {
    this.loadChapter.emit({pageNum, scrollPart});
  }

}
