import {Component, EventEmitter, inject, input, Output} from '@angular/core';
import {PersonalToC} from "../../../_models/readers/personal-toc";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ReaderService} from "../../../_services/reader.service";

@Component({
  selector: 'app-text-bookmark-item',
  imports: [
    NgbTooltip,
    TranslocoDirective
  ],
  templateUrl: './text-bookmark-item.component.html',
  styleUrl: './text-bookmark-item.component.scss'
})
export class TextBookmarkItemComponent {
  bookmark = input.required<PersonalToC>();

  @Output() loadBookmark =  new EventEmitter<PersonalToC>();
  @Output() removeBookmark =  new EventEmitter<PersonalToC>();

  private readonly readerService = inject(ReaderService);


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
