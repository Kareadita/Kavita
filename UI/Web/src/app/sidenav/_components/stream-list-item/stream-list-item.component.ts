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

@Component({
  selector: 'app-stream-list-item',
  standalone: true,
  imports: [CommonModule, ImageComponent, MangaFormatIconPipe, MangaFormatPipe, NgbProgressbar, TranslocoDirective],
  templateUrl: './stream-list-item.component.html',
  styleUrls: ['./stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StreamListItemComponent {
  @Input({required: true}) item!: DashboardStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<DashboardStream> = new EventEmitter<DashboardStream>();


  private readonly cdRef = inject(ChangeDetectorRef);


}
