import {ChangeDetectionStrategy, Component, inject, input, output} from '@angular/core';
import {DownloadQueueItem} from '../../../shared/_models/download-queue-item';
import {BytesPipe} from '../../../_pipes/bytes.pipe';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgClass} from "@angular/common";
import {ImageComponent} from '../../../shared/image/image.component';
import {ImageService} from '../../../_services/image.service';

@Component({
  selector: 'app-queue-item',
  templateUrl: './queue-item.component.html',
  styleUrls: ['./queue-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BytesPipe, TranslocoDirective, NgClass, ImageComponent]
})
export class QueueItemComponent {
  readonly item = input.required<DownloadQueueItem>();

  readonly cancel = output<number>();
  readonly retry = output<number>();
  readonly remove = output<number>();

  protected readonly imageService = inject(ImageService);
}
