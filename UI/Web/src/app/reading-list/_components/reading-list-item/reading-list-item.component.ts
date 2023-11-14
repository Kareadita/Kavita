import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { LibraryType } from 'src/app/_models/library/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { ReadingListItem } from 'src/app/_models/reading-list';
import { ImageService } from 'src/app/_services/image.service';
import { MangaFormatIconPipe } from '../../../_pipes/manga-format-icon.pipe';
import { MangaFormatPipe } from '../../../_pipes/manga-format.pipe';
import { NgbProgressbar } from '@ng-bootstrap/ng-bootstrap';
import { NgIf, DatePipe } from '@angular/common';
import { ImageComponent } from '../../../shared/image/image.component';
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
    selector: 'app-reading-list-item',
    templateUrl: './reading-list-item.component.html',
    styleUrls: ['./reading-list-item.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [ImageComponent, NgIf, NgbProgressbar, DatePipe, MangaFormatPipe, MangaFormatIconPipe, TranslocoDirective]
})
export class ReadingListItemComponent {

  @Input({required: true}) item!: ReadingListItem;
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
