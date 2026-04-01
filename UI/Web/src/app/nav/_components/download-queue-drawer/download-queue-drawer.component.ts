import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {NgbActiveOffcanvas, NgbCollapse, NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {Router} from "@angular/router";
import {DownloadService} from '../../../shared/_services/download.service';
import {TranslocoDirective} from "@jsverse/transloco";
import {
  OffCanvasResizeComponent,
  ResizeMode
} from "../../../shared/_components/off-canvas-resize/off-canvas-resize.component";
import {DownloadQueueItemComponent} from "../download-queue-item/download-queue-item.component";
import {DownloadQueueItem} from "../../../shared/_models/download-queue-item";
import {UtilityService} from "../../../shared/_services/utility.service";
import {BreakpointService} from "../../../_services/breakpoint.service";

@Component({
  selector: 'app-download-queue-drawer',
  templateUrl: './download-queue-drawer.component.html',
  styleUrls: ['./download-queue-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, OffCanvasResizeComponent, DownloadQueueItemComponent, NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, NgbNavOutlet, NgbCollapse]
})
export class DownloadQueueDrawerComponent {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly router = inject(Router);
  readonly downloadService = inject(DownloadService);
  readonly breakpointService = inject(BreakpointService);

  readonly activeTabId = signal<'downloading' | 'completed'>('downloading');
  olderCollapsed = true;

  readonly downloadingTabCount = computed(() =>
    (this.downloadService.activeItem() ? 1 : 0) + this.downloadService.queuedItems().length + this.downloadService.failedItems().length
  );

  readonly completedTabCount = computed(() =>
    this.downloadService.completedTodayCount() + this.downloadService.olderCompletedCount()
  );

  readonly completedToday = this.downloadService.completedItems;
  readonly olderItems = this.downloadService.olderCompletedItems;
  readonly olderCount = this.downloadService.olderCompletedCount;

  close() {
    this.activeOffcanvas.close();
  }

  navigateToItem(item: DownloadQueueItem) {
    if (item.entityType === 'volume') {
      this.router.navigate(['/library', item.libraryId, 'series', item.seriesId, 'volume', item.entityId]);
    } else if (item.entityType === 'chapter') {
      this.router.navigate(['/library', item.libraryId, 'series', item.seriesId, 'chapter', item.entityId]);
    } else {
      this.router.navigate(['/library', item.libraryId, 'series', item.seriesId]);
    }
    this.close();
  }

  clearCompletedToday() {
    const ids = this.completedToday().map(i => i.id);
    this.downloadService.clearCompletedByIds(ids);
  }

  clearCompletedOlder() {
    this.downloadService.clearOlderCompleted();
  }

  expandOlder() {
    this.olderCollapsed = !this.olderCollapsed;
    if (!this.olderCollapsed) {
      this.downloadService.loadOlderCompleted();
    }
  }

  protected readonly ResizeMode = ResizeMode;
  protected readonly window = window;
}
