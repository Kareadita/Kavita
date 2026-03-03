import {ChangeDetectionStrategy, Component, input, output} from '@angular/core';
import {DownloadQueueItem} from '../../../shared/_models/download-queue-item';
import {BytesPipe} from '../../../_pipes/bytes.pipe';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgClass} from "@angular/common";

@Component({
  selector: 'app-queue-item',
  templateUrl: './queue-item.component.html',
  styleUrls: ['./queue-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BytesPipe, TranslocoDirective, NgClass]
})
export class QueueItemComponent {
  readonly item = input.required<DownloadQueueItem>();

  readonly cancel = output<number>();
  readonly retry = output<number>();
  readonly remove = output<number>();
}
