import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter, inject,
  Input,
  Output
} from '@angular/core';
import {APP_BASE_HREF, CommonModule, NgClass} from '@angular/common';
import {ImageComponent} from "../../../shared/image/image.component";
import {MangaFormatIconPipe} from "../../../_pipes/manga-format-icon.pipe";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {NgbProgressbar} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";
import {RouterLink} from "@angular/router";

@Component({
  selector: 'app-dashboard-stream-list-item',
  standalone: true,
  imports: [ImageComponent, MangaFormatIconPipe, MangaFormatPipe, NgbProgressbar, TranslocoDirective, StreamNamePipe, NgClass, RouterLink],
  templateUrl: './dashboard-stream-list-item.component.html',
  styleUrls: ['./dashboard-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardStreamListItemComponent {
  @Input({required: true}) item!: DashboardStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<DashboardStream> = new EventEmitter<DashboardStream>();
  protected readonly baseUrl = inject(APP_BASE_HREF);
}
