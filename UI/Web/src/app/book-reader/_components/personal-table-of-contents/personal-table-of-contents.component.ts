import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal} from '@angular/core';
import {ReaderService} from "../../../_services/reader.service";
import {PersonalToC} from "../../../_models/readers/personal-toc";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {TextBookmarkItemComponent} from "../text-bookmark-item/text-bookmark-item.component";
import {ConfirmService} from "../../../shared/confirm.service";
import {FilterFieldComponent} from "../../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../../_helpers/filtered";

export interface PersonalToCEvent {
  pageNum: number;
  scrollPart: string | undefined;
}

@Component({
  selector: 'app-personal-table-of-contents',
  imports: [TranslocoDirective, TextBookmarkItemComponent, FilterFieldComponent],
  templateUrl: './personal-table-of-contents.component.html',
  styleUrls: ['./personal-table-of-contents.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonalTableOfContentsComponent implements OnInit {

  private readonly readerService = inject(ReaderService);
  private readonly confirmService = inject(ConfirmService);

  chapterId = input.required<number>();
  readonly loadChapter = output<PersonalToCEvent>();


  ptocBookmarks = signal<PersonalToC[]>([]);
  query = signal('');
  filteredBookmarks = filteredBy(this.ptocBookmarks, this.query, 'title', 'pageNumber', 'chapterTitle');
  showFilter = computed(() => {
    return this.ptocBookmarks().length >= 10;
  });

  ngOnInit() {
    this.load();
  }

  load() {
    this.readerService.getPersonalToC(this.chapterId()).subscribe(res => {
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
