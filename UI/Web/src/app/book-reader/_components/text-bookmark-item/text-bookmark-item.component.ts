import {ChangeDetectionStrategy, Component, inject, input, output} from '@angular/core';
import {PersonalToC} from "../../../_models/readers/personal-toc";
import {TranslocoDirective} from "@jsverse/transloco";
import {ReaderService} from "../../../_services/reader.service";

@Component({
  selector: 'app-text-bookmark-item',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './text-bookmark-item.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './text-bookmark-item.component.scss'
})
export class TextBookmarkItemComponent {
  private readonly readerService = inject(ReaderService);

  bookmark = input.required<PersonalToC>();

  readonly loadBookmark = output<PersonalToC>();
  readonly removeBookmark = output<PersonalToC>();

  remove(evt: Event) {
    evt.stopPropagation();
    evt.preventDefault();

    this.removeBookmark.emit(this.bookmark());
  }

  goTo(evt: Event) {
    evt.stopPropagation();
    evt.preventDefault();

    const bookmark = {...this.bookmark()};
    bookmark.bookScrollId = this.readerService.scopeBookReaderXpath(bookmark.bookScrollId ?? '');

    this.loadBookmark.emit(bookmark);
  }

}
