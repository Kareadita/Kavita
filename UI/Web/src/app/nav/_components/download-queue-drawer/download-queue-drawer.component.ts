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

  readonly activeTabId = signal<'downloading' | 'completed'>('downloading');
  olderCollapsed = true;

  readonly downloadingTabCount = computed(() =>
    (this.downloadService.activeItem() ? 1 : 0) + this.downloadService.queuedItems().length + this.downloadService.failedItems().length
  );

  readonly completedTabCount = computed(() => this.downloadService.completedItems().length);

  readonly completedToday = computed(() => {
    const now = new Date();
    const startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    return this.downloadService.completedItems().filter(i => (i.completedAt ?? 0) >= startOfDay);
  });

  readonly completedOlder = computed(() => {
    const now = new Date();
    const startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    return this.downloadService.completedItems().filter(i => (i.completedAt ?? 0) < startOfDay);
  });

  close() {
    this.activeOffcanvas.close();
  }

  navigateToItem(item: DownloadQueueItem) {
    this.router.navigate(['/library', item.libraryId, 'series', item.seriesId]);
    this.close();
  }

  clearCompletedToday() {
    const ids = this.completedToday().map(i => i.id);
    this.downloadService.clearCompletedByIds(ids);
  }

  clearCompletedOlder() {
    const ids = this.completedOlder().map(i => i.id);
    this.downloadService.clearCompletedByIds(ids);
  }

  protected readonly ResizeMode = ResizeMode;
  protected readonly window = window;
}
