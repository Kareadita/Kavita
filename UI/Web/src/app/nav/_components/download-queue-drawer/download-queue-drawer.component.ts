import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {DownloadService} from '../../../shared/_services/download.service';
import {TranslocoDirective} from "@jsverse/transloco";
import {
  OffCanvasResizeComponent,
  ResizeMode
} from "../../../shared/_components/off-canvas-resize/off-canvas-resize.component";
import {DownloadQueueItemComponent} from "../download-queue-item/download-queue-item.component";

@Component({
  selector: 'app-download-queue-drawer',
  templateUrl: './download-queue-drawer.component.html',
  styleUrls: ['./download-queue-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, OffCanvasResizeComponent, DownloadQueueItemComponent]
})
export class DownloadQueueDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  readonly downloadService = inject(DownloadService);

  close() {
    this.activeOffcanvas.close();
  }

  protected readonly ResizeMode = ResizeMode;
  protected readonly window = window;
}
