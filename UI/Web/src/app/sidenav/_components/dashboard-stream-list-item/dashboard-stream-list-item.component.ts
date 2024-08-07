import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output
} from '@angular/core';
import {CommonModule, NgClass} from '@angular/common';
import {ImageComponent} from "../../../shared/image/image.component";
import {MangaFormatIconPipe} from "../../../_pipes/manga-format-icon.pipe";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {NgbProgressbar} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@ngneat/transloco";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";

@Component({
  selector: 'app-dashboard-stream-list-item',
  standalone: true,
  imports: [ImageComponent, MangaFormatIconPipe, MangaFormatPipe, NgbProgressbar, TranslocoDirective, StreamNamePipe, NgClass],
  templateUrl: './dashboard-stream-list-item.component.html',
  styleUrls: ['./dashboard-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardStreamListItemComponent {
  @Input({required: true}) item!: DashboardStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<DashboardStream> = new EventEmitter<DashboardStream>();
}
