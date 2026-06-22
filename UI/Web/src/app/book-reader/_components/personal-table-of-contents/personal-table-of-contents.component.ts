import {
  ChangeDetectionStrategy,
  Component, computed,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  output,
  signal
} from '@angular/core';
import {ReaderService} from "../../../_services/reader.service";
import {PersonalToC} from "../../../_models/readers/personal-toc";
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {TextBookmarkItemComponent} from "../text-bookmark-item/text-bookmark-item.component";
import {ConfirmService} from "../../../shared/confirm.service";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";

export interface PersonalToCEvent {
  pageNum: number;
  scrollPart: string | undefined;
}

@Component({
  selector: 'app-personal-table-of-contents',
  imports: [TranslocoDirective, TextBookmarkItemComponent, FormsModule, ReactiveFormsModule],
  templateUrl: './personal-table-of-contents.component.html',
  styleUrls: ['./personal-table-of-contents.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonalTableOfContentsComponent implements OnInit {

  private readonly readerService = inject(ReaderService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);

  protected readonly ShowFilterAfterItems = 10;

  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNum: number = 0;
  @Input({required: true}) tocRefresh!: EventEmitter<void>;
  readonly loadChapter = output<PersonalToCEvent>();


  ptocBookmarks = signal<PersonalToC[]>([]);
  visibleBookmarks = computed(() => {
    const query = this.query()?.toLowerCase() ?? '';

    return this.ptocBookmarks().filter(bookMark => {
      return bookMark.title.toLowerCase().indexOf(query) >= 0
        || bookMark.pageNumber.toString().indexOf(query) >= 0
        || (bookMark.chapterTitle ?? '').toLowerCase().indexOf(query) >= 0;
    });
  });

  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });
  query = toSignal(this.formGroup.get('filter')!.valueChanges, {initialValue: ''});

  ngOnInit() {
    this.tocRefresh.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.load();
    });

    this.load();
  }

  load() {
    this.readerService.getPersonalToC(this.chapterId).subscribe(res => {
      this.ptocBookmarks.set(res);
    });
  }

  loadChapterPage(pageNum: number, scrollPart: string | undefined) {
    this.loadChapter.emit({pageNum, scrollPart});
  }

  async removeBookmark(bookmark: PersonalToC) {

    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-bookmark'))) return;

    this.readerService.removePersonalToc(bookmark.chapterId, bookmark.pageNumber, bookmark.title).subscribe(() => {
      this.ptocBookmarks.set(this.ptocBookmarks().filter(t => t.title !== bookmark.title));
    });
  }

}
