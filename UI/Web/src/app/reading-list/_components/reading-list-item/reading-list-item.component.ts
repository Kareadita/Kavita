import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { ReadingListItem } from 'src/app/_models/reading-list';
import { ImageService } from 'src/app/_services/image.service';

@Component({
  selector: 'app-reading-list-item',
  templateUrl: './reading-list-item.component.html',
  styleUrls: ['./reading-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadingListItemComponent {

  @Input() item!: ReadingListItem;
  @Input() position: number = 0;
  @Input() libraryTypes: {[key: number]: LibraryType} = {};
  /**
   * If the Reading List is promoted or not
   */
  @Input() promoted: boolean = false;

  @Output() read: EventEmitter<ReadingListItem> = new EventEmitter();
  @Output() remove: EventEmitter<ReadingListItem> = new EventEmitter();

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  constructor(public imageService: ImageService) { }

  readChapter(item: ReadingListItem) {
    this.read.emit(item);
  }


}
