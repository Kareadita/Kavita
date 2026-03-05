import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {DownloadQueueItem} from '../../../shared/_models/download-queue-item';
import {BytesPipe} from '../../../_pipes/bytes.pipe';
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from '../../../shared/image/image.component';
import {ImageService} from '../../../_services/image.service';
import {DownloadQueueStatusPipe} from "../../../_pipes/download-queue-status.pipe";
import {TimeAgoPipe} from "../../../_pipes/time-ago.pipe";
import {CompactEtaPipe} from "../../../_pipes/compact-eta.pipe";

@Component({
  selector: 'app-download-queue-item',
  templateUrl: './download-queue-item.component.html',
  styleUrls: ['./download-queue-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BytesPipe, TranslocoDirective, ImageComponent, DownloadQueueStatusPipe, TimeAgoPipe, CompactEtaPipe]
})
export class DownloadQueueItemComponent {
  private readonly imageService = inject(ImageService);

  readonly item = input.required<DownloadQueueItem>();

  readonly cancelled = output<number>();
  readonly retry = output<number>();
  readonly remove = output<number>();

  readonly imageUrl = computed(() => {
    const item = this.item();

    return item.entityType === 'volume'
      ? this.imageService.getVolumeCoverImage(item.entityId)
      : this.imageService.getChapterCoverImage(item.entityId)
  });

}
