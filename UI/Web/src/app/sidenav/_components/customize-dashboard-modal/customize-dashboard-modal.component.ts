import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SafeHtmlPipe} from "../../../pipe/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {
  ReadingListItemComponent
} from "../../../reading-list/_components/reading-list-item/reading-list-item.component";
import {forkJoin} from "rxjs";
import {FilterService} from "../../../_services/filter.service";
import {StreamListItemComponent} from "../stream-list-item/stream-list-item.component";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {DashboardService} from "../../../_services/dashboard.service";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";

@Component({
  selector: 'app-customize-dashboard-modal',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, TranslocoDirective, DraggableOrderedListComponent, ReadingListItemComponent, StreamListItemComponent],
  templateUrl: './customize-dashboard-modal.component.html',
  styleUrls: ['./customize-dashboard-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeDashboardModalComponent {

  items: DashboardStream[] = [];
  smartFilters: SmartFilter[] = [];
  accessibilityMode: boolean = false;

  private readonly dashboardService = inject(DashboardService);
  private readonly filterService = inject(FilterService);
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor(public modal: NgbActiveModal) {
    forkJoin([this.dashboardService.getDashboardStreams(false), this.filterService.getAllFilters()]).subscribe(results => {
      this.items = results[0];
      const smartFilterStreams = new Set(results[0].filter(d => !d.isProvided).map(d => d.name));
      this.smartFilters = results[1].filter(d => !smartFilterStreams.has(d.name));
      this.cdRef.markForCheck();
    });
  }

  addFilterToStream(filter: SmartFilter) {
    this.dashboardService.createDashboardStream(filter.id).subscribe(stream => {
      this.smartFilters = this.smartFilters.filter(d => d.name !== filter.name);
      this.items.push(stream);
      this.cdRef.detectChanges();
    });
  }


  orderUpdated(event: IndexUpdateEvent) {
    this.dashboardService.updateDashboardStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe();
  }

  updateVisibility(item: DashboardStream, position: number) {
    this.items[position].visible = !this.items[position].visible;
    this.dashboardService.updateDashboardStream(this.items[position]).subscribe();
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close();
  }


}
