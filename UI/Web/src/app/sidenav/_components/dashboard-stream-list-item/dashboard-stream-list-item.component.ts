import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  Output
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ImageComponent} from "../../../shared/image/image.component";
import {MangaFormatIconPipe} from "../../../pipe/manga-format-icon.pipe";
import {MangaFormatPipe} from "../../../pipe/manga-format.pipe";
import {NgbProgressbar} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@ngneat/transloco";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {CommonStream} from "../../../_models/common-stream";
import {StreamNamePipe} from "../../../pipe/stream-name.pipe";

@Component({
  selector: 'app-dashboard-stream-list-item',
  standalone: true,
  imports: [CommonModule, ImageComponent, MangaFormatIconPipe, MangaFormatPipe, NgbProgressbar, TranslocoDirective, StreamNamePipe],
  templateUrl: './dashboard-stream-list-item.component.html',
  styleUrls: ['./dashboard-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardStreamListItemComponent {
  @Input({required: true}) item!: DashboardStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<DashboardStream> = new EventEmitter<DashboardStream>();


}
