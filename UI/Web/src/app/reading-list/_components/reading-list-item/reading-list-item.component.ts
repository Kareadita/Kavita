import {ChangeDetectionStrategy, Component, EventEmitter, inject, Input, Output} from '@angular/core';
import { LibraryType } from 'src/app/_models/library/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { ReadingListItem } from 'src/app/_models/reading-list';
import { ImageService } from 'src/app/_services/image.service';
import { MangaFormatIconPipe } from '../../../_pipes/manga-format-icon.pipe';
import { MangaFormatPipe } from '../../../_pipes/manga-format.pipe';
import { NgbProgressbar } from '@ng-bootstrap/ng-bootstrap';
import { DatePipe } from '@angular/common';
import { ImageComponent } from '../../../shared/image/image.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {SeriesFormatComponent} from "../../../shared/series-format/series-format.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";

@Component({
    selector: 'app-reading-list-item',
    templateUrl: './reading-list-item.component.html',
    styleUrls: ['./reading-list-item.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [ImageComponent, NgbProgressbar, DatePipe, MangaFormatPipe, MangaFormatIconPipe, TranslocoDirective, SeriesFormatComponent, ReadMoreComponent]
})
export class ReadingListItemComponent {

  protected readonly imageService = inject(ImageService);
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) item!: ReadingListItem;
  @Input() position: number = 0;
  @Input() libraryTypes: {[key: number]: LibraryType} = {};
  /**
   * If the Reading List is promoted or not
   */
  @Input() promoted: boolean = false;

  @Output() read: EventEmitter<ReadingListItem> = new EventEmitter();
  @Output() remove: EventEmitter<ReadingListItem> = new EventEmitter();

  readChapter(item: ReadingListItem) {
    this.read.emit(item);
  }
}
