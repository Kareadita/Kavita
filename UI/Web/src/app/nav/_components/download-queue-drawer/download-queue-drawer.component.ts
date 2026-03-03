import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {DownloadService} from '../../../shared/_services/download.service';
import {QueueItemComponent} from '../queue-item/queue-item.component';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-download-queue-drawer',
  templateUrl: './download-queue-drawer.component.html',
  styleUrls: ['./download-queue-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [QueueItemComponent, TranslocoDirective]
})
export class DownloadQueueDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  readonly downloadService = inject(DownloadService);

  close() {
    this.activeOffcanvas.close();
  }
}
