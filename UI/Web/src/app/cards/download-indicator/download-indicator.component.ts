import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {DownloadQueueItem} from 'src/app/shared/_models/download-queue-item';
import {CircularLoaderComponent} from "../../shared/circular-loader/circular-loader.component";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-download-indicator',
  imports: [CircularLoaderComponent, TranslocoDirective],
  templateUrl: './download-indicator.component.html',
  styleUrls: ['./download-indicator.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadIndicatorComponent {
  download = input.required<DownloadQueueItem | null>();
}
