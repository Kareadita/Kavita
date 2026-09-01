import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {DownloadQueueItem} from "../../shared/_models/download-queue-item";

@Component({
  selector: 'app-download-indicator',
  imports: [TranslocoDirective],
  templateUrl: './download-indicator.component.html',
  styleUrls: ['./download-indicator.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadIndicatorComponent {
  download = input.required<DownloadQueueItem | null>();

  isCompleted = computed(() => this.download()?.status === 'completed');
  isQueued = computed(() => this.download()?.status === 'queued');
  isActive = computed(() => !this.isCompleted() && !this.isQueued() && this.download() != null);
}
