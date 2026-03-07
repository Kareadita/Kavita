import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {DownloadService} from "../../../shared/_services/download.service";
import {DrawerService} from "../../../_services/drawer.service";
import {DownloadQueueDrawerComponent} from "../download-queue-drawer/download-queue-drawer.component";

@Component({
  selector: 'app-download-queue-widget',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './download-queue-widget.component.html',
  styleUrl: './download-queue-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DownloadQueueWidgetComponent {

  public readonly downloadService = inject(DownloadService);
  private readonly drawerService = inject(DrawerService);

  openDownloadQueue() {
    this.drawerService.open(DownloadQueueDrawerComponent, { position: 'end' });
  }

}
