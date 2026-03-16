import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {DownloadEntityType, DownloadQueueItem} from '../../../shared/_models/download-queue-item';
import {BytesPipe} from '../../../_pipes/bytes.pipe';
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from '../../../shared/image/image.component';
import {ImageService} from '../../../_services/image.service';
import {DownloadQueueStatusPipe} from "../../../_pipes/download-queue-status.pipe";
import {TimeAgoPipe} from "../../../_pipes/time-ago.pipe";
import {CompactEtaPipe} from "../../../_pipes/compact-eta.pipe";
import {TagBadgeColor, TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";

@Component({
  selector: 'app-download-queue-item',
  templateUrl: './download-queue-item.component.html',
  styleUrls: ['./download-queue-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BytesPipe, TranslocoDirective, ImageComponent, DownloadQueueStatusPipe, TimeAgoPipe, CompactEtaPipe, TagBadgeComponent]
})
export class DownloadQueueItemComponent {
  private readonly imageService = inject(ImageService);

  readonly item = input.required<DownloadQueueItem>();

  readonly cancelled = output<number>();
  readonly retry = output<number>();
  readonly remove = output<number>();
  readonly navigate = output<DownloadQueueItem>();

  readonly imageUrl = computed(() => {
    const item = this.item();

    switch (item.entityType) {
      case DownloadEntityType.Volume: return this.imageService.getVolumeCoverImage(item.entityId);
      case DownloadEntityType.Chapter: return this.imageService.getChapterCoverImage(item.entityId);
      case DownloadEntityType.ReadingListItem: return this.imageService.getChapterCoverImage(item.chapterId!);
    }
    return '';
  });

  readonly statusBadgeColor = computed<TagBadgeColor>(() => {
    switch (this.item().status) {
      case 'completed': return 'primary';
      case 'failed': return 'error';
      case 'downloading':
      case 'preparing': return 'primary';
      case 'queued':
      default: return 'secondary';
    }
  });

  readonly timeDisplay = computed(() => {
    const i = this.item();
    if (i.completedAt && (i.status === 'completed' || i.status === 'failed')) {
      return i.completedAt;
    }
    return i.queuedAt;
  });

  readonly showSpeedInfo = computed(() => {
    const i = this.item();
    return (i.status === 'preparing' || i.status === 'downloading')
      && (i.speedBps ?? 0) > 0;
  });

  readonly isActive = computed(() => {
    const s = this.item().status;
    return s === 'preparing' || s === 'downloading';
  });

  readonly showSize = computed(() => this.item().estimatedSize > 0);

  protected readonly TagBadgeCursor = TagBadgeCursor;
}
